"""T7.1 (`affix-authoring`) — the real-run CLI entrypoint and its own eligible-atom-pool loader.
Closes the "no CLI surface exists yet" / "input source unspecified" gaps T7.1's own todo evidence
block named: the pool is now concretely "every atom id the real shipped seed tree carries," proven
against the REAL `data/seed/atoms/**.json` files, not a synthetic fixture.
"""
from __future__ import annotations

import pytest

from seedsmith.adapters.effects.affix.generate_affixes import (
    ATOMS_ROOT,
    derive_atom_id,
    load_eligible_atoms,
    main,
)
from seedsmith.report.cli import build_parser, cmd_effects


def test_derive_atom_id_matches_AtomRow_DeriveId_with_no_variant():
    assert derive_atom_id({"family": "atom.fx-passive-atk-flat", "tier": 1}) == "atom.fx-passive-atk-flat.t1"


def test_derive_atom_id_matches_AtomRow_DeriveId_with_a_variant():
    assert derive_atom_id({"family": "atom.fx-shield-grant", "tier": 1, "variant": "a"}) == "atom.fx-shield-grant.a.t1"


def test_the_real_shipped_atom_tree_yields_at_least_the_atoms_this_session_already_verified_exist():
    # Same four real atoms T6.1's own evidence block already confirmed exist in the shipped tree —
    # cross-checked here from the Python side, not re-assumed.
    atoms = load_eligible_atoms(ATOMS_ROOT)
    for expected in (
        "atom.fx-passive-atk-flat.t1",
        "atom.fx-butter-on-hit.t1",
        "atom.fx-shield-grant.a.t1",
        "atom.fx-cold-on-hit.t1",
    ):
        assert expected in atoms, f"{expected} missing from the real eligible pool"


def test_has_trigger_reads_the_atoms_own_real_when_clause_not_a_kind_default():
    atoms = load_eligible_atoms(ATOMS_ROOT)
    # fx-passive-atk-flat is a permanent stat.modify with no "when" block -> prefix-shaped.
    assert atoms["atom.fx-passive-atk-flat.t1"] is False
    # fx-butter-on-hit fires on OnDamageDealt -> suffix-shaped.
    assert atoms["atom.fx-butter-on-hit.t1"] is True


def test_only_narrows_the_pool_to_exactly_the_named_atoms():
    atoms = load_eligible_atoms(ATOMS_ROOT, only=["atom.fx-passive-atk-flat.t1", "atom.fx-cold-on-hit.t1"])
    assert set(atoms) == {"atom.fx-passive-atk-flat.t1", "atom.fx-cold-on-hit.t1"}


def test_dry_run_makes_no_model_calls_and_exits_clean(capsys):
    exit_code = main(["--dry-run"])
    assert exit_code == 0
    out = capsys.readouterr().out
    assert "no model calls made" in out
    assert "atom.fx-passive-atk-flat.t1" in out


def test_narrowing_below_two_eligible_atoms_refuses_rather_than_running():
    with pytest.raises(SystemExit):
        main(["--dry-run", "--only", "atom.fx-passive-atk-flat.t1"])


# ---- the real `seedsmith effects generate` CLI dispatch, not just the module directly -------------


def test_cli_parses_effects_generate_affix_with_every_flag():
    parser = build_parser()
    args = parser.parse_args([
        "effects", "generate", "--kind", "affix", "--dry-run",
        "--only", "atom.a,atom.b", "--theme", "elemental duality", "--count", "3",
    ])
    assert args.func is cmd_effects
    assert args.effects_command == "generate"
    assert args.kind == "affix"
    assert args.only == "atom.a,atom.b"
    assert args.theme == "elemental duality"
    assert args.count == 3


def test_cmd_effects_dry_run_end_to_end_through_the_real_cli(capsys):
    parser = build_parser()
    args = parser.parse_args(["effects", "generate", "--kind", "affix", "--dry-run"])
    exit_code = args.func(args)
    assert exit_code == 0
    assert "no model calls made" in capsys.readouterr().out


def test_cmd_effects_refuses_an_unknown_kind_naming_it(capsys):
    parser = build_parser()
    args = parser.parse_args(["effects", "generate", "--kind", "not-a-real-kind", "--dry-run"])
    exit_code = args.func(args)
    assert exit_code == 2  # EXIT_CANNOT_RUN
    assert "not-a-real-kind" in capsys.readouterr().out
