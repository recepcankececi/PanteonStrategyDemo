---
name: adr
description: Write an Architecture Decision Record (Turkish) into ../case-devdocs/ADR whenever a critical technical choice is made (algorithm, data structure, pattern, rendering/batching, input, UI approach, tooling).
---

# /adr — decision record

1. Next number: highest `ADR-NNN-*.md` in `../case-devdocs/ADR` + 1.
2. Copy `../case-devdocs/ADR/_template.md` → `ADR-NNN-<kebab-title>.md`.
3. Fill every section in Turkish. Code identifiers stay in English.
   - **Seçenekler:** at least 2 real alternatives with pros/cons — no straw men.
   - **Nasıl çalışır:** concrete — class names, data flow, complexity where relevant.
   - **Kodda nerede:** file paths.
4. Status: `Önerildi` if Can hasn't approved it in this session, otherwise `Kabul`.
5. Add a line to the ADR index in `../case-devdocs/README.md`.
