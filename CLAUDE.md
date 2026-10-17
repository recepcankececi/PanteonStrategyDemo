# PanteonStrategyDemo — Agent Instructions

Unity **6.3 LTS**, 2D, Universal 2D (URP) template. Windows standalone build.
Panteon "Strategy Game Demo" case. Requirement checklist: `.claude/requirements.md` (source of truth).
Reviewers will read code, scene hierarchy, object names and folder layout. Quality > speed.
Language: code, comments, commits, README → English. `../case-devdocs` → Turkish (private repo; reviewers never see it, so never link to it from the game repo).

## Roles
- **Can** (developer) owns every decision and the only visual/gameplay PASS. You never declare a feature "works" visually; you provide evidence (tests, console) and list what Can must check in-game.
- **You** implement one milestone per session, end-to-end, without unnecessary stops. The rules below are invariants, not checkpoints. Report once, at the end.
- **Stop and ask ONLY for:**
  - an irreversible action (deleting assets/folders, rewriting git history, changing `Packages/manifest.json` or ProjectSettings in ways that affect other systems);
  - a requirement contradiction;
  - an architecture choice not covered by an accepted ADR in `../case-devdocs/ADR` → write it with `/adr` as `Önerildi`, then stop for approval.

## Invariants
1. **Never text-edit Unity-serialized files.** The protected extension list lives in `.claude/hooks/guard-serialized.ps1` (single source). Change them only through MCP for Unity tools or an Editor script.
2. **Never delete, move or hand-create `.meta` files**, never recursive-delete under `Assets/`. Move/rename/delete assets only through Unity (MCP).
3. **Console baseline:** at milestone start, refresh Unity and record the console (errors/warnings). After every C# change: refresh + read console. **Zero errors and no warnings beyond the baseline** before continuing.
4. **Tests:** every logic system (grid, placement validation, A*, pooling, HP/combat, stat data in ScriptableObjects incl. R7/R8 values) has EditMode tests. Run via MCP. If they cannot be run, the milestone is reported **INCOMPLETE** — never "done".
5. **Honesty over delivery.** Incomplete, untested or uncertain work is reported as such.
6. **Dependencies:** no new packages, third-party code or assets without Can's approval. Can decides whether a licence is acceptable; record source + terms in a `README.txt` next to the asset. Visual source of truth: **our generated flat vector art** (`Tools/ArtGen`, ADR-007) — every sprite comes from the generator; do not mix in other art styles. Building sprites are exactly footprint × 64 px with a centred pivot.
7. **Git:**
   - Never push, never `reset --hard`, never `clean`, never rewrite history (hook-enforced).
   - Stage explicit paths only; never `git add -A` / `git add .`.
   - Never stage `Packages/`, `.mcp.json` or `ProjectSettings/` unless the milestone explicitly says so.
   - **You own all commits** (Can does not commit). Commit at milestone end: game repo first, then devdocs. Conventional Commits.
   - At session start, if either repo has uncommitted changes you did not make (planning edits to `CLAUDE.md`, `.claude/`, `Packages/manifest.json` or devdocs), commit them first as a separate `chore:`/`docs:` commit with explicit paths — these exceptions to the staging rule are allowed here.
   - Keep the `Co-Authored-By: Claude` trailer on every commit (transparent AI use; Can's decision, ADR-000).
8. **Docs gate:** before finishing, update `../case-devdocs` (Turkish): `Systems/<system>.md`, `Log.md`, and an ADR for every critical choice. A Stop hook enforces this (devdocs must be newer than the last game change).
9. **Docs are interview prep.** Can will defend this code in a technical interview. Write devdocs in a teaching tone (explain concepts, not just decisions). Every ADR and `Systems/` doc follows its template, including **Senaryolar** (≥3, one from an alternative's point of view) and **Mülakat soruları** (question → short answer); append new questions to `../case-devdocs/Mulakat/Soru-Bankasi.md`. Code you can't explain simply is a smell — simplify it.

## Project layout (target — created in M0)
```
Assets/_Project/
  Scenes/                Main.unity (single gameplay scene)
  Scripts/<Module>/      one asmdef per module (defined in ADR-001)
  Scripts/EditorTools/   editor-only tooling (BuildScript, scene builders), namespace StrategyDemo.EditorTools
  Prefabs/  ScriptableObjects/  UI/  Settings/ (URP assets)
  Art/Generated/         PNGs written by Tools/ArtGen (README.txt: "created for this project")
  Tests/EditMode/        asmdef StrategyDemo.Tests.EditMode
Tools/ArtGen/            Node sprite generator (outside Assets/, never imported by Unity)
```
Template leftovers (SampleScene, `Assets/Settings`, `InputSystem_Actions`) are moved into `_Project` or removed in M0 via Unity.

## Code standards
- Namespace `StrategyDemo.<Module>`, matching the folder. Never use `Editor` as a namespace segment (clashes with `UnityEditor.Editor`).
- PascalCase types/methods/properties; `_camelCase` private fields; `[SerializeField] private` over public fields; interfaces `IName`.
- One top-level type per file; file name = type name.
- Data-driven: stats, sizes, sprites, names live in **ScriptableObjects**. Literal numbers only as named `const` for true invariants (e.g. `const int MaxPathNodes`), never for tunable gameplay values.
- Modules talk through interfaces / C# events. No `FindObjectOfType`, no `GameObject.Find`, no mutable static singletons unless an ADR allows it.
- Subscribe in `OnEnable`, unsubscribe in `OnDisable`. Pooled objects reset state and clear subscribers on release.
- No per-frame allocations (Update, pathfinding loops, scroll view). Pool anything spawned repeatedly.
- Public API has `///` XML docs; inline comments explain *why*. Prefer clear code over clever code — Can must be able to explain every line (T6).
- Tests: `MethodOrScenario_Condition_ExpectedResult`.

## Rendering / build rules
- All gameplay sprites in one SpriteAtlas (per ADR), except the tiled ground tile (ADR-007); never access `renderer.material` at runtime (breaks batching) — use `sharedMaterial` or sprite/color only.
- UI: Canvas Scaler = Scale With Screen Size; anchors, no fixed pixel positions.
- Player Settings: windowed, **resizable window** on.

## Scene / hierarchy standards
- Root groups: `--- Systems ---`, `--- Board ---`, `--- UI ---`, `--- Camera ---`.
- `EventSystem` under UI; `Global Light 2D` under Camera; pool parents under Board (`Pool_<Type>`).
- Runtime-spawned objects go under a named parent, never the scene root. No default names ("GameObject (1)", "Image").

## Workflow
Every milestone runs through `/feature` (see `.claude/skills/feature/SKILL.md`).

## Final report (short)
- Requirement IDs covered
- Done
- Evidence: tests n/n, console vs baseline
- **Can must check in-game:** numbered list
- Open issues / risks
- Commits: game `<hash>`, devdocs `<hash>`
