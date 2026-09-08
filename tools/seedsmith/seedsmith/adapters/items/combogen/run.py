"""seedsmith.adapters.items.combogen.run — the run plan and the dry run.

⚠ **What this module does and does not do.** It assembles the whole run *deterministically*: the
grid cells, the id each entry will take, the brief for each, and the reports the run is judged
against. The **model call itself is not made here** — the graph that makes it is the same `workflow`
package `effects generate` and `demons generate` use. A `--dry-run` therefore exercises everything
except the call, which is what makes the run inspectable before a token is spent.

⚠ **No resume ledger, deliberately.** `spec-strain-splice-gen.md`'s own Commands block says 102
entries is small enough not to need the `demons run` resume harness, and module 13 built one because
it faced ~1,800. A ledger here would be machinery with no failure to survive; re-running the whole
grid is cheap, and `plan_run` is byte-identical across runs, which is the property that makes
re-running safe.

⛔ **The gem-supply precheck runs BEFORE the plan is returned**, not after — the spec's own code-style
block is explicit that a 102-entry run minting unsupplied families is 102 wasted calls plus a red
gate. Here it cannot happen at all: the supplied set becomes the schema's closed enum.
"""
from __future__ import annotations

from dataclasses import dataclass

from . import brief as brief_mod
from . import catalogue as catalogue_mod
from . import emit, grid, schema as schema_mod, supply as supply_mod
from .grid import Cell
from .tuning import ComboTuning

SHAPES: "tuple[str, ...]" = ("strain", "splice")


@dataclass(frozen=True)
class Subject:
    """One unit of work: one grid cell, one id already decided."""

    subject_id: str
    shape: str
    entry_id: str
    name_key: str
    theme_key: "str | None"
    aptitudes: "tuple[str, ...]"
    archetype: "str | None"
    brief: str

    def to_dict(self) -> dict:
        return {"subjectId": self.subject_id, "shape": self.shape, "entryId": self.entry_id,
                "nameKey": self.name_key, "themeKey": self.theme_key,
                "aptitudes": list(self.aptitudes), "archetype": self.archetype}


@dataclass(frozen=True)
class RunPlan:
    subjects: "tuple[Subject, ...]"
    shape: str
    supply: supply_mod.SupplyReport
    host_roles: "tuple[str, ...]"
    catalogue: catalogue_mod.CatalogueReport

    @property
    def complete(self) -> bool:
        """Every cell in the grid has a subject. There is no holdback here — unlike module 13's
        species population, the grid is closed and fully known, so an incomplete plan means a
        derivation broke rather than that content is missing."""
        return bool(self.subjects)

    def summary(self) -> dict:
        return {
            "shape": self.shape,
            "toGenerate": len(self.subjects),
            "complete": self.complete,
            "ingredientFamiliesSupplied": self.supply.family_count,
            "gemsShipped": self.supply.gem_count,
            "hostRoles": list(self.host_roles),
            "geometricCombosPerActor": len(self.host_roles),
            "catalogue": self.catalogue.to_dict(),
        }


def granted_family_vocabulary(supply: supply_mod.SupplyReport) -> "tuple[str, ...]":
    """What a combination may GRANT.

    Deliberately the same closed family vocabulary the ingredients are drawn from, and this is a
    judgement worth stating: an atom family that no gem supplies is one no insert can carry, so
    granting it would put a combination's payoff outside the layer's own vocabulary. Narrowing the
    grant pool to the supplied set keeps the whole combination inside one closed list, which is what
    makes `Registration/IngredientUnsatisfiable` sufficient rather than half a check.
    """
    return supply.families


def cells_for(shape: str) -> "tuple[Cell, ...]":
    if shape == "strain":
        return grid.strain_cells()
    if shape == "splice":
        return grid.splice_cells()
    raise ValueError(f"shape must be one of {SHAPES}, got {shape!r}")


def plan_run(*, shape: str, tuning: ComboTuning,
             supply: "supply_mod.SupplyReport | None" = None) -> RunPlan:
    if shape not in SHAPES:
        raise ValueError(f"shape must be one of {SHAPES}, got {shape!r}")

    report = supply or supply_mod.build()
    host_roles = tuning.host_roles()
    granted = granted_family_vocabulary(report)

    # PRECHECK, before the plan exists: building the schema refuses an empty supplied set and an
    # empty host-role set, both of which would produce a plan for content nothing can host.
    schema_mod.combination_schema(tuning, supplied_families=report.families,
                                  host_roles=host_roles, granted_families=granted)

    subjects: "list[Subject]" = []
    for cell in cells_for(shape):
        entry_id = emit.combo_id(cell)
        subjects.append(Subject(
            subject_id=f"combination-{cell.combination_kind}-{cell.key}",
            shape=cell.combination_kind,
            entry_id=entry_id,
            name_key=emit.name_key(cell),
            theme_key=cell.theme_key,
            aptitudes=tuple(a.id for a in cell.aptitudes),
            archetype=cell.archetype,
            brief=brief_mod.build_brief(cell, tuning, report,
                                        granted_families=granted, host_roles=host_roles),
        ))

    ids = [s.entry_id for s in subjects]
    if len(set(ids)) != len(ids):
        duplicated = sorted({i for i in ids if ids.count(i) > 1})
        raise ValueError(
            f"the grid minted the duplicate ids {duplicated} — for a Splice this would mean the "
            f"ordinal sort failed and C(n,2) is no longer one id per pair")

    strains = len(grid.strain_cells())
    splices = len(grid.splice_cells())
    return RunPlan(
        subjects=tuple(subjects), shape=shape, supply=report, host_roles=host_roles,
        catalogue=catalogue_mod.report(tuning, strains=strains, splices=splices),
    )
