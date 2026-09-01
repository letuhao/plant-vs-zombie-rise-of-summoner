"""Tests for seedsmith.pipeline.llm_caller (tasks/seedsmith-todo.md, S0).

    python -m pytest tools/seedsmith/tests/test_llm_caller.py -v
    python tools/seedsmith/tests/test_llm_caller.py

Runs fully offline: every network-touching test talks only to a stdlib `http.server` spun up
in-process on `127.0.0.1`. Nothing here ever reaches `llm_caller.DEFAULT_CONFIG`'s real endpoint.
"""
from __future__ import annotations

import http.server
import json
import re
import socket
import sys
import tempfile
import threading
import tomllib
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.pipeline.llm_caller import (  # noqa: E402
    DEFAULT_CONFIG,
    LlmCallerConfig,
    call_model,
    call_with_self_heal,
    extract_json,
    load_config,
)

LLM_CALLER_SRC = Path(__file__).resolve().parent.parent / "seedsmith" / "pipeline" / "llm_caller.py"


class _Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):  # silence stdlib per-request logging
        pass

    def do_POST(self):
        length = int(self.headers.get("Content-Length", 0))
        body = json.loads(self.rfile.read(length))
        self.server.requests.append(body)  # type: ignore[attr-defined]
        queued = self.server.responses  # type: ignore[attr-defined]
        content = queued.pop(0) if queued else "{}"
        payload = json.dumps({"choices": [{"message": {"content": content}}]}).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.end_headers()
        self.wfile.write(payload)


class MockModelServer:
    """A fake OpenAI-compatible chat endpoint: records every request body, replies with a
    scripted queue of `content` strings in order."""

    def __init__(self) -> None:
        self.httpd = http.server.HTTPServer(("127.0.0.1", 0), _Handler)
        self.httpd.requests = []  # type: ignore[attr-defined]
        self.httpd.responses = []  # type: ignore[attr-defined]
        self.thread = threading.Thread(target=self.httpd.serve_forever, daemon=True)
        self.thread.start()

    @property
    def url(self) -> str:
        return f"http://127.0.0.1:{self.httpd.server_port}/v1/chat/completions"

    @property
    def requests(self) -> list[dict]:
        return self.httpd.requests  # type: ignore[attr-defined]

    def queue(self, *contents: str) -> None:
        self.httpd.responses.extend(contents)  # type: ignore[attr-defined]

    def close(self) -> None:
        self.httpd.shutdown()
        self.httpd.server_close()
        self.thread.join(timeout=2)


class ReasoningDisabledTests(unittest.TestCase):
    """Every call must carry both reasoning-disable fields — different local servers/chat
    templates read different keys (spec-pipeline.md §5.1), so both are sent unconditionally."""

    def setUp(self) -> None:
        self.server = MockModelServer()
        self.config = LlmCallerConfig(endpoint=self.server.url, attempts=1, retry_delay=0)

    def tearDown(self) -> None:
        self.server.close()

    def test_reasoning_disable_fields_sent_on_every_call(self) -> None:
        self.server.queue('{"a": "1"}', '{"a": "2"}')
        call_model("sys", "user one", config=self.config)
        call_model("sys", "user two", config=self.config)

        self.assertEqual(len(self.server.requests), 2)
        for req in self.server.requests:
            self.assertEqual(req["reasoning_effort"], "none")
            self.assertEqual(
                req["chat_template_kwargs"],
                {"enable_thinking": False, "thinking": False},
            )

    def test_call_model_returns_message_content(self) -> None:
        self.server.queue('{"hello": "world"}')
        result = call_model("sys", "user", config=self.config)
        self.assertEqual(result, '{"hello": "world"}')


class RetryExhaustionTests(unittest.TestCase):
    """No mock server here on purpose: an unreachable endpoint proves `call_model` actually
    retries `attempts` times (not just once) and then raises distinctly, rather than hanging
    or silently returning an empty result."""

    def test_raises_after_exhausting_attempts_against_an_unreachable_endpoint(self) -> None:
        probe = socket.socket()
        probe.bind(("127.0.0.1", 0))
        dead_port = probe.getsockname()[1]
        probe.close()  # nothing listens here now; connecting refuses/times out fast

        config = LlmCallerConfig(
            endpoint=f"http://127.0.0.1:{dead_port}/v1/chat/completions",
            attempts=2, retry_delay=0, timeout=0.3,
        )
        with self.assertRaises(RuntimeError) as ctx:
            call_model("sys", "user", config=config)
        self.assertIn("2 attempts", str(ctx.exception))


class ExtractJsonTests(unittest.TestCase):
    def test_clean_json(self) -> None:
        self.assertEqual(extract_json('{"a": 1}'), {"a": 1})

    def test_fenced_json(self) -> None:
        text = '```json\n{"a": 1, "b": "two"}\n```'
        self.assertEqual(extract_json(text), {"a": 1, "b": "two"})

    def test_prose_wrapped_json(self) -> None:
        text = 'Sure, here is the result:\n{"a": 1}\nLet me know if you need anything else!'
        self.assertEqual(extract_json(text), {"a": 1})

    def test_unescaped_quote_inside_value_falls_back_to_regex(self) -> None:
        # The exact defect the docstring names: translating «the "bible"» leaves a raw
        # unescaped `"` inside a value, breaking strict json.loads.
        text = '{"a": "the "bible" thing"}'
        self.assertEqual(extract_json(text), {"a": 'the "bible" thing'})

    def test_no_json_object_raises(self) -> None:
        with self.assertRaises(ValueError):
            extract_json("no braces here at all")


class SelfHealTests(unittest.TestCase):
    def setUp(self) -> None:
        self.server = MockModelServer()
        self.config = LlmCallerConfig(endpoint=self.server.url, attempts=1, retry_delay=0,
                                      max_heal=2)

    def tearDown(self) -> None:
        self.server.close()

    @staticmethod
    def _verify_k_equals_good(items: dict, out: dict) -> tuple[dict, dict]:
        if out.get("k") == "GOOD":
            return {}, {}
        return {"k": "must equal GOOD"}, {}

    def test_retry_reprompts_with_named_defect_and_accepts_the_fix(self) -> None:
        self.server.queue('{"k": "bad"}', '{"k": "GOOD"}')

        out, soft = call_with_self_heal(
            items={"k": "orig"},
            system="sys",
            build_user=lambda items: json.dumps(items),
            verify_fn=self._verify_k_equals_good,
            config=self.config,
        )

        self.assertEqual(out, {"k": "GOOD"})
        self.assertEqual(soft, {})
        self.assertEqual(len(self.server.requests), 2)
        # the second call's re-prompt must name the exact defect, not a generic retry
        heal_prompt = self.server.requests[1]["messages"][1]["content"]
        self.assertIn("must equal GOOD", heal_prompt)

    def test_exhausted_heal_falls_back_to_default_and_reports_failure(self) -> None:
        # max_heal=2 -> 3 total attempts; none of these ever satisfy verify_fn
        self.server.queue('{"k": "x1"}', '{"k": "x2"}', '{"k": "x3"}')

        out, soft = call_with_self_heal(
            items={"k": "orig"},
            system="sys",
            build_user=lambda items: json.dumps(items),
            verify_fn=lambda items, out: ({"k": "never satisfied"}, {}),
            config=self.config,
        )

        self.assertEqual(len(self.server.requests), 3)
        self.assertEqual(out["k"], "orig")  # no-silent-drop: falls back to the source value
        self.assertTrue(soft["k"].startswith("FAILED:"))

    def test_custom_default_for_is_honored(self) -> None:
        self.server.queue('{"k": "x1"}', '{"k": "x2"}', '{"k": "x3"}')

        out, soft = call_with_self_heal(
            items={"k": "orig"},
            system="sys",
            build_user=lambda items: json.dumps(items),
            verify_fn=lambda items, out: ({"k": "never satisfied"}, {}),
            config=self.config,
            default_for=lambda key, original: "SENTINEL",
        )

        self.assertEqual(out["k"], "SENTINEL")
        self.assertTrue(soft["k"].startswith("FAILED:"))

    def test_unparseable_output_is_treated_as_a_named_defect_not_a_crash(self) -> None:
        self.server.queue("not json at all", '{"k": "GOOD"}')

        out, soft = call_with_self_heal(
            items={"k": "orig"},
            system="sys",
            build_user=lambda items: json.dumps(items),
            verify_fn=self._verify_k_equals_good,
            config=self.config,
        )

        self.assertEqual(out, {"k": "GOOD"})
        self.assertEqual(len(self.server.requests), 2)


class LoadConfigTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp())

    def test_defaults_when_file_absent(self) -> None:
        missing = self.tmp / "does-not-exist.toml"
        self.assertEqual(load_config(missing), DEFAULT_CONFIG)

    def test_defaults_when_section_absent(self) -> None:
        path = self.tmp / "seedsmith.toml"
        path.write_text('[adapter]\nname = "items"\n', encoding="utf-8")
        self.assertEqual(load_config(path), DEFAULT_CONFIG)

    def test_overrides_only_the_keys_present(self) -> None:
        path = self.tmp / "seedsmith.toml"
        path.write_text(
            '[pipeline.llm_caller]\n'
            'endpoint = "http://127.0.0.1:9999/v1/chat/completions"\n'
            'max_heal = 5\n',
            encoding="utf-8",
        )
        cfg = load_config(path)
        self.assertEqual(cfg.endpoint, "http://127.0.0.1:9999/v1/chat/completions")
        self.assertEqual(cfg.max_heal, 5)
        # everything NOT set in the file falls back to the default, not to zero/None
        self.assertEqual(cfg.model, DEFAULT_CONFIG.model)
        self.assertEqual(cfg.attempts, DEFAULT_CONFIG.attempts)

    def test_malformed_toml_raises_rather_than_silently_defaulting(self) -> None:
        path = self.tmp / "seedsmith.toml"
        path.write_text("this is not [valid toml", encoding="utf-8")
        with self.assertRaises(tomllib.TOMLDecodeError):
            load_config(path)


class DependencyIsolationTests(unittest.TestCase):
    """S0's whole reason for being a Phase-0, dependency-free slice: it must not import
    anything from the corpus/adapter/metrics chain. Enforced mechanically, not by prose,
    mirroring this repo's guard-secondary-no-unity.ps1 discipline for the C# side."""

    def test_no_import_from_corpus_adapters_or_metrics(self) -> None:
        source = LLM_CALLER_SRC.read_text(encoding="utf-8")
        forbidden = re.compile(
            r"^\s*(from\s+seedsmith\.(corpus|adapters|metrics)\b|"
            r"import\s+seedsmith\.(corpus|adapters|metrics)\b)",
            re.MULTILINE,
        )
        match = forbidden.search(source)
        self.assertIsNone(
            match,
            f"llm_caller.py must not depend on corpus/adapters/metrics, found: {match}",
        )


if __name__ == "__main__":
    unittest.main()


# ---- G0.3: constrained decoding (spec-dependency-baseline.md §2.4) -----------------------------


class ConstrainedDecodingTests(unittest.TestCase):
    """`schema` is optional and must be INERT when unused — the acceptance criterion that matters
    most, because every existing caller relies on today's body shape."""

    def setUp(self) -> None:
        self.server = MockModelServer()
        self.config = LlmCallerConfig(endpoint=self.server.url, attempts=1, retry_delay=0)

    def tearDown(self) -> None:
        self.server.close()

    def test_schema_none_produces_a_body_with_no_response_format_key(self) -> None:
        """Provably inert: omitting `schema` must not add anything to the request."""
        self.server.queue('{"a": "1"}')
        call_model("sys", "user", config=self.config)
        self.assertNotIn("response_format", self.server.requests[0])

    def test_schema_none_body_is_byte_identical_to_explicitly_passing_none(self) -> None:
        self.server.queue('{"a": "1"}', '{"a": "2"}')
        call_model("sys", "user", config=self.config)
        call_model("sys", "user", config=self.config, schema=None)
        self.assertEqual(self.server.requests[0], self.server.requests[1])

    def test_a_schema_is_sent_as_openai_style_json_schema_response_format(self) -> None:
        schema = {"type": "object", "properties": {"label": {"type": "string"}},
                  "required": ["label"], "additionalProperties": False}
        self.server.queue('{"label": "nut"}')
        call_model("sys", "user", config=self.config, schema=schema)

        rf = self.server.requests[0]["response_format"]
        self.assertEqual(rf["type"], "json_schema")
        self.assertTrue(rf["json_schema"]["strict"])
        self.assertEqual(rf["json_schema"]["schema"], schema)

    def test_reasoning_disable_fields_survive_alongside_a_schema(self) -> None:
        """Constrained decoding must not silently drop the reasoning-disable contract."""
        self.server.queue('{"label": "x"}')
        call_model("sys", "u", config=self.config, schema={"type": "object"})
        req = self.server.requests[0]
        self.assertEqual(req["reasoning_effort"], "none")
        self.assertEqual(req["chat_template_kwargs"], {"enable_thinking": False, "thinking": False})

    def test_extract_json_is_still_available_as_defense_in_depth(self) -> None:
        """Constrained decoding does not replace the fallback — schema behaviour is not guaranteed
        portable across serving implementations (JSON Schema does not specify whitespace)."""
        self.assertEqual(extract_json('```json\n{"a": "1"}\n```'), {"a": "1"})
