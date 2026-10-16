# PanteonStrategyDemo — Agent Instructions

Unity **6.3 LTS**, 2D, Universal 2D (URP) template. Windows standalone build.
Panteon "Strategy Game Demo" case. Requirement checklist: `.claude/requirements.md` (source of truth).
Reviewers will read code, scene hierarchy, object names and folder layout. Quality > speed.

## Roles
- **Can** (developer) owns every decision and the only visual/gameplay PASS. You never declare a feature "works" visually; you provide evidence (tests, console) and list what Can must check in-game.
- **You** implement one milestone per session, end-to-end, without unnecessary stops. The rules below are invariants, not checkpoints. Report once, at the end.
- Stop and ask ONLY for: an irreversible action, a requirement contradiction, or an architecture choice not covered by an ADR in `../case-devdocs/ADR`.

## Invariants
1. **Never text-edit Unity-serialized files** (`.unity .prefab .asset .meta .mat .spriteatlas .anim .controller`, `ProjectSettings/`). Use MCP for Unity tools or an Editor script. A hook enforces this.
2. **Never delete, move or hand-create `.meta` files.** Move/rename assets only through Unity (MCP).
3. After any C# change: refresh Unity via MCP and read the console. **Zero compile errors, zero new warnings** before continuing.
4. Every system with logic (grid, placement validation, A*, pooling, HP/combat) gets **EditMode tests** in `Assets/_Project/Tests/EditMode`. All green before finishing.
5. **Honesty over delivery.** Incomplete, untested or uncertain work is reported as such. Never claim untested behavior.
6. No new packages, third-party code or assets without Can's approval. Assets must be CC0 (or equivalent) with the licence file committed next to them.
7. Never `git push`. Commit locally at milestone end, Conventional Commits, English.
8. **Docs gate:** before finishing, update `../case-devdocs` (Turkish): `Systems/<system>.md`, `Log.md`, and an ADR for every critical choice (`/adr`). A Stop hook enforces this.

## Project layout
```
Assets/_Project/
  Scenes/                Main.unity (single gameplay scene)
  Scripts/<Module>/      one asmdef per module (defined in ADR-001)
  Scripts/Editor/        editor-only tooling (BuildScript, scene builders)
  Prefabs/  ScriptableObjects/  UI/  Settings/
  Art/ThirdParty/Kenney_MedievalRTS/   (+ License.txt)
  Tests/EditMode/
```
Everything we author lives under `_Project`.

## Code standards
- Namespace `StrategyDemo.<Module>`, matching the folder.
- PascalCase types/methods/properties; `_camelCase` private fields; `[SerializeField] private` over public fields; interfaces `IName`.
- Data-driven: stats, sizes, sprites and names live in **ScriptableObjects**. No magic numbers.
- Modules talk through interfaces / C# events. No `FindObjectOfType`, no `GameObject.Find`, no mutable static singletons unless an ADR allows it.
- Public API has `///` XML docs; inline comments explain *why*, not *what*.
- No per-frame allocations (Update, pathfinding loops, scroll view). Pool anything spawned repeatedly.
- One class per file; file name = class name.

## Scene / hierarchy standards
- Root groups: `--- Systems ---`, `--- Board ---`, `--- UI ---`, `--- Camera ---`.
- Runtime-spawned objects go under a named parent, never the scene root.
- No default names ("GameObject (1)", "Image").

## Workflow
Use `/feature <milestone>` for every milestone: plan → implement → refresh + console → tests → `/verify` → docs → commit → report.

## Final report (short)
- Done
- Evidence: tests n/n, console clean
- **Can must check in-game:** numbered list
- Open issues / risks
- Docs updated
