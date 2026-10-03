#!/usr/bin/env python3
"""Rank Timetracker production types by uncovered lines.

Usage: python3 rank_coverage.py <cobertura.xml> [--all]

Two things the raw Cobertura file hides:

* third-party code bundled by the test runner (SolutionPersistence, FakeItEasy,
  ...) reports as 0% and would dominate any ranking, so only ``Timetracker.*``
  classes outside test assemblies are counted;
* an async method and its local functions become separate classes
  (``Confirmations.<ConfirmAsync>d__1``), which splits one type's real gap
  across rows, so classes are aggregated by their owning type.

Branch points are Cobertura ``line/@branch="True"`` with
``@condition-coverage="N% (missed/total)"``; uncovered branch points are counted
per type and shown as ``+N branches`` when any exist.

Prints total coverage and the types with the largest number of uncovered lines.
With ``--all``, also prints the fully covered types.
"""
import sys
import xml.etree.ElementTree as ET


def producer_type(name: str) -> str:
    """The type a Cobertura class belongs to, compiler-generated parts stripped."""
    return name.split("<", 1)[0].rstrip(".")


def is_production(name: str, filename: str) -> bool:
    if not name.startswith("Timetracker."):
        return False
    if ".Tests." in name or "Tests" in filename:
        return False
    return True


def main() -> int:
    print_all = "--all" in sys.argv
    args = [arg for arg in sys.argv[1:] if arg != "--all"]
    if len(args) != 1:
        print(__doc__)
        return 2

    root = ET.parse(args[0]).getroot()
    totals: dict[str, list[int]] = {}
    covered = uncovered = 0
    for cls in root.iter("class"):
        name = cls.get("name")
        filename = cls.get("filename") or ""
        if not is_production(name, filename):
            continue
        lines = cls.find("lines")
        if lines is None:
            continue
        total = len(lines)
        missed = sum(1 for line in lines if line.get("hits") == "0")
        branch_points = sum(
            1 for line in lines
            if line.get("branch") == "True"
            and (line.get("condition-coverage") or "").startswith("0%"))
        covered += total - missed
        uncovered += missed
        owner = producer_type(name)
        entry = totals.setdefault(owner, [0, 0, 0])
        entry[0] += missed
        entry[1] += total
        entry[2] += branch_points

    total = covered + uncovered
    percent = (covered / total * 100) if total else 0.0
    print(f"Timetracker production: {percent:.1f}% line coverage "
          f"({covered}/{total} lines, {uncovered} uncovered)")
    rows = sorted(
        ((missed, lines_total, branches, owner)
         for owner, (missed, lines_total, branches) in totals.items()),
        key=lambda row: (-row[0], row[3]))
    gaps = 0
    for missed, lines_total, branches, owner in rows:
        if not missed and not print_all:
            continue
        if missed:
            gaps += 1
        suffix = f", {branches} uncovered branches" if branches else ""
        print(f"  {missed:4d} uncovered of {lines_total:4d}{suffix}  {owner}")
    print(f"types with gaps: {gaps}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except BrokenPipeError:
        sys.exit(0)
