"""seedsmith.pipeline.llm_caller — local-model transport with reasoning disabled.

Ported from `D:\\Works\\source\\lore-weave\\scripts\\i18n_translate.py`, proven in production on
that project's translate pipeline (spec-pipeline.md §5.1). Two pieces are copied close to as-is
(`call_model`, `extract_json`); the self-heal loop is generalized — `translate_chunk()`'s
translation-specific verify is replaced by a `verify_fn` parameter, so any future seedsmith
pipeline (flavour text, set headers, …) supplies its own hard/soft rule and reuses this loop
unchanged.

No import from `seedsmith.corpus`, `seedsmith.adapters`, or `seedsmith.metrics`, in either
direction. This module has no position in that dependency chain (tasks/seedsmith-plan.md, S0) —
it is buildable and testable before any of those exist, and must stay that way.
"""
from __future__ import annotations

import json
import re
import time
import tomllib
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

VerifyFn = Callable[[dict, dict], tuple[dict, dict]]
BuildUserFn = Callable[[dict], str]
BuildHealUserFn = Callable[[dict, dict, dict], str]
DefaultForFn = Callable[[str, object], object]


@dataclass(frozen=True)
class LlmCallerConfig:
    """Every number here is adjustable per call — none is a hidden constant.

    Defaults match the ported script's LM Studio setup; a different local server, model, or
    heal budget is a config change, not a code change.
    """

    endpoint: str = "http://localhost:1234/v1/chat/completions"
    model: str = "google/gemma-4-26b-a4b-qat"
    timeout: float = 420.0          # a 26B model at high context is slow; be patient
    attempts: int = 2               # hammering a wedged local queue with retries makes it worse
    retry_delay: float = 3.0
    max_heal: int = 3


DEFAULT_CONFIG = LlmCallerConfig()


def load_config(toml_path: Path | None = None) -> LlmCallerConfig:
    """Read the `[pipeline.llm_caller]` table from `seedsmith.toml`, falling back to
    `DEFAULT_CONFIG` for any key the file doesn't set — including every key, if the file
    itself doesn't exist yet (spec-foundation §7.3: every seedsmith flag has a config
    equivalent, but nothing in seedsmith requires a `seedsmith.toml` to exist today).

    A missing file or a missing table is a legitimate default. A *malformed* file is not — it
    is not distinguished from a healthy default here on purpose, matching this program's
    running rule that a wrong value silently treated as absent is the defect class this whole
    audit exists to catch; `tomllib.TOMLDecodeError` propagates uncaught.
    """
    path = toml_path or Path("seedsmith.toml")
    section: dict = {}
    if path.exists():
        with path.open("rb") as f:
            data = tomllib.load(f)
        section = data.get("pipeline", {}).get("llm_caller", {})
        if not isinstance(section, dict):
            section = {}
    base = DEFAULT_CONFIG
    return LlmCallerConfig(
        endpoint=section.get("endpoint", base.endpoint),
        model=section.get("model", base.model),
        timeout=section.get("timeout", base.timeout),
        attempts=section.get("attempts", base.attempts),
        retry_delay=section.get("retry_delay", base.retry_delay),
        max_heal=section.get("max_heal", base.max_heal),
    )


def call_model(system: str, user: str, *, config: LlmCallerConfig = DEFAULT_CONFIG,
               temperature: float = 0.2, schema: "dict | None" = None) -> str:
    """Call a local OpenAI-compatible chat endpoint with reasoning disabled.

    Two redundant fields are sent on every call because different servers/templates read
    different keys: `reasoning_effort` is the OpenAI-style field some servers honor directly;
    `chat_template_kwargs` is passed straight through to the model's own Jinja chat template,
    where a reasoning-capable model usually gates a <think> block on a variable named
    `enable_thinking`/`thinking`. Sending both is harmless — an OpenAI-compatible server or
    template ignores whichever key it doesn't recognize.

    `schema` (optional, spec-dependency-baseline.md §2.4) turns on CONSTRAINED DECODING: LM Studio
    enforces a JSON Schema at decode time via llama.cpp's GBNF grammar sampling for GGUF models, so
    a token that would break the schema is never sampleable. Measured 2026-09-01 against
    `google/gemma-4-26b-a4b-qat` with a hostile prompt (demanding prose, ```json fences, and an
    out-of-enum value): unconstrained returned a prose paragraph and `json.loads` FAILED;
    constrained returned clean conforming JSON with the illegal enum value unreachable — at no
    latency cost (3.7s vs 3.9s; mean 3.2s over 8 further calls).

    **Optional on purpose.** `schema=None` produces a byte-identical request body to before this
    parameter existed, so every existing caller is unaffected — asserted by test. `extract_json`
    stays as defense-in-depth: JSON Schema does not specify whitespace handling, so schema
    behaviour is not guaranteed portable across serving implementations.
    """
    payload = {
        "model": config.model, "temperature": temperature,
        "reasoning_effort": "none",
        "chat_template_kwargs": {"enable_thinking": False, "thinking": False},
        "messages": [{"role": "system", "content": system},
                     {"role": "user", "content": user}],
    }
    if schema is not None:
        payload["response_format"] = {
            "type": "json_schema",
            "json_schema": {"name": "seedsmith_response", "strict": True, "schema": schema},
        }
    body = json.dumps(payload).encode("utf-8")
    last_err: Exception | None = None
    for attempt in range(config.attempts):
        try:
            req = urllib.request.Request(config.endpoint, data=body,
                                         headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=config.timeout) as resp:
                data = json.loads(resp.read())
            return data["choices"][0]["message"]["content"]
        except (urllib.error.URLError, TimeoutError, KeyError) as e:
            last_err = e
            if attempt + 1 < config.attempts:
                time.sleep(config.retry_delay)
    raise RuntimeError(f"model call failed after {config.attempts} attempts: {last_err}")


def extract_json(text: str) -> dict:
    """Strip ```json fences / prose and parse the first {...} object.

    Tolerates the single most common LLM-JSON slip: an unescaped double-quote inside a value.
    Callers are expected to use quote-free dotted/plain identifiers as keys, so a flat regex
    extraction (value = everything up to a `"` followed by `,` or `}`) recovers the pairs when
    strict `json.loads` chokes.
    """
    t = text.strip()
    if "```" in t:
        t = re.sub(r"^```[a-zA-Z]*\n?|```$", "", t.strip("`\n ")).strip()
    start, end = t.find("{"), t.rfind("}")
    if start == -1 or end == -1:
        raise ValueError("no JSON object in model output")
    blob = t[start:end + 1]
    try:
        return json.loads(blob)
    except json.JSONDecodeError:
        pairs = re.findall(r'"([^"\n]+)"\s*:\s*"(.*?)"(?=\s*[,}])', blob, re.DOTALL)
        if pairs:
            return {k: v for k, v in pairs}
        raise


def _default_heal_user(items: dict, out: dict, hard: dict) -> str:
    defects = "\n".join(f"- {k}: {r}" for k, r in list(hard.items())[:40])
    need = {k: items[k] for k in hard if k in items}
    return (f"Your output had these problems:\n{defects}\n\n"
            f"Fix them. Return the COMPLETE corrected JSON object (all keys). "
            f"Source for the affected keys:\n{json.dumps(need, ensure_ascii=False)}\n\n"
            f"Full source:\n{json.dumps(items, ensure_ascii=False)}")


def call_with_self_heal(
    items: dict,
    system: str,
    build_user: BuildUserFn,
    verify_fn: VerifyFn,
    *,
    config: LlmCallerConfig = DEFAULT_CONFIG,
    max_heal: int | None = None,
    build_heal_user: BuildHealUserFn | None = None,
    default_for: DefaultForFn | None = None,
) -> tuple[dict, dict]:
    """Call the model, verify the parsed output, and self-heal on named defects.

    Generalized from `translate_chunk()`: that function hardcoded a translation-specific
    `verify_chunk`; here `verify_fn(items, out) -> (hard, soft)` is supplied by the caller, so
    a flavour-text pipeline, a set-header pipeline, or any future one reuses this exact loop
    with its own rule.

    `hard` failures block acceptance and trigger a re-prompt naming the exact defect per key —
    a bare retry teaches the model nothing; naming the reason is what fixes it (spec-pipeline.md
    §3.6). `soft` failures are reported but never retried automatically.

    On exhausted heal rounds, `default_for(key, original_value)` supplies the no-silent-drop
    fallback (default: the original item's own value) and the returned `soft` dict records
    every such key as `"FAILED:<reason>"` — never blank, never silently dropped.

    Never raises on a model or parse failure — a dead attempt re-prompts for valid JSON and is
    caught uniformly by the next round's `verify_fn`, exactly like any other named defect.
    """
    heal_budget = config.max_heal if max_heal is None else max_heal
    build_heal_user = build_heal_user or _default_heal_user
    default_for = default_for or (lambda key, original: original)

    user = build_user(items)
    out: dict = {}
    for _ in range(heal_budget + 1):
        try:
            out = extract_json(call_model(system, user, config=config))
        except (ValueError, json.JSONDecodeError, RuntimeError) as e:
            user = (f"Your previous output could not be parsed as valid JSON ({e}). "
                    f"Re-emit ONLY a strictly-valid JSON object for:\n"
                    f"{json.dumps(items, ensure_ascii=False)}")
            continue
        hard, soft = verify_fn(items, out)
        if not hard:
            return out, soft
        user = build_heal_user(items, out, hard)

    hard, soft = verify_fn(items, out)
    for key in hard:
        out[key] = default_for(key, items.get(key, out.get(key)))
    return out, {**soft, **{k: f"FAILED:{r}" for k, r in hard.items()}}
