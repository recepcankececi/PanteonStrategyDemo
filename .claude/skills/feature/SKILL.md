---
name: feature
description: Implement one milestone end-to-end (plan, code, Unity refresh, tests, verify, docs, commit, report). Use for every milestone, e.g. /feature M2 Grid.
---

# /feature — milestone workflow

Argument: milestone ID and name (e.g. `M2 Grid`). Read the milestone section in `../case-devdocs/00-Overview.md` and relevant ADRs first.

Run all steps in one go. Do not stop for confirmation unless CLAUDE.md lists the case as a stop condition.

1. **Scope** — list the requirement IDs (`.claude/requirements.md`) this milestone covers and its done criteria.
2. **Implement** — C# under `Assets/_Project/Scripts/<Module>`. Scene/prefab/SO changes only via MCP for Unity or an Editor script.
3. **Compile check** — refresh Unity via MCP, read console. Fix until zero errors and zero new warnings.
4. **Tests** — add/extend EditMode tests incl. edge cases; run via MCP. If tests can't be run via MCP, say so in the report (do not claim green).
5. **Verify** — run the `/verify` checklist for the covered IDs and spawn the `reviewer` agent on the diff (`git diff`). Fix real findings.
6. **Docs (Turkish)** — `../case-devdocs/Systems/<system>.md` (what, how it works, key classes, data flow, edge cases), append `Log.md`, `/adr` for each critical choice.
7. **Commit** — game repo and devdocs repo separately, Conventional Commits in English (`feat(grid): ...`). No push.
8. **Report** — the short format in CLAUDE.md, including the numbered "Can must check in-game" list.
