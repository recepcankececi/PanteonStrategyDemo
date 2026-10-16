---
name: feature
description: Implement one milestone end-to-end (baseline, code, Unity refresh, tests, verify, review, docs, commit, report). Use for every milestone, e.g. /feature M2 Grid.
---

# /feature — milestone workflow

Argument: milestone ID and name (e.g. `M2 Grid`). First read the milestone section in `../case-devdocs/00-Overview.md` and the ADRs it references. If a needed decision has no accepted ADR → `/adr` as `Önerildi` and stop for approval.

Run all steps in one go; stop only for CLAUDE.md stop conditions.

1. **Scope** — requirement IDs covered and done criteria.
2. **Baseline** — refresh Unity via MCP, record console errors/warnings.
3. **Implement** — C# under `Assets/_Project/Scripts/<Module>`. Scenes/prefabs/SOs/settings only via MCP or an Editor script.
4. **Compile check** — refresh + console after each change set; nothing beyond baseline.
5. **Tests** — EditMode tests incl. edge cases; run via MCP. Not runnable → milestone is **INCOMPLETE**.
6. **Verify** — `/verify <IDs>`; fix FAILs.
7. **Review** — spawn the `reviewer` agent on `git diff`; fix blocker/major findings, list the rest.
8. **Docs (Turkish)** — `Systems/<system>.md`, `Log.md`, `/adr` for each critical choice.
9. **Commit** — stage explicit paths; game repo first, then devdocs. Conventional Commits, English. No push.
10. **Report** — the format in CLAUDE.md.
