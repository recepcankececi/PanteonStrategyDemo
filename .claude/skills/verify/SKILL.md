---
name: verify
description: Self-audit the project (or the given requirement IDs) against .claude/requirements.md and CLAUDE.md standards. Use before every milestone commit and before submission.
---

# /verify — requirement & standards audit

Argument: requirement IDs (e.g. `R2 R6 A1`) or `all`.

For each ID produce one line: `ID | PASS / FAIL / NEEDS-CAN | evidence (file:line, test name) | note`.
- `PASS` only with code + test/console evidence.
- `NEEDS-CAN` for anything visual or feel-based (layout, red preview, resize, SetPass count in Frame Debugger) — Can is the only visual PASS.

Then check standards:
- Naming, namespaces, one class per file, XML docs on public API
- No `Find*`, no magic numbers, no per-frame allocations, pooling where spawning repeats
- Scene hierarchy groups and names; folder layout under `_Project`
- Edge cases: placement off-board / overlapping, path to unreachable or occupied cell, target destroyed mid-path/attack, selecting destroyed object, many units same target, window resize, empty selection

Output the table plus a short list of FAILs to fix. Do not fix silently — list, then fix in the calling workflow.
