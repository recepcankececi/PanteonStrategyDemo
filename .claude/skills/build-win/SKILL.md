---
name: build-win
description: Produce the Windows standalone build into Builds/Windows. Only when Can asks.
disable-model-invocation: true
---

# /build-win

Precondition: `Assets/_Project/Scripts/Editor/BuildScript.cs` exists (created in M0) with
menu `Tools/Build/Windows` and static method `StrategyDemo.Editor.BuildScript.BuildWindows`.

- **Editor open (normal case):** trigger the menu item `Tools/Build/Windows` via MCP (execute menu item), then read console for build result.
- **Editor closed:** run batchmode (Unity path from `../case-devdocs/Tooling/Kurulum.md`):
  `"<UnityPath>\Unity.exe" -batchmode -quit -projectPath . -executeMethod StrategyDemo.Editor.BuildScript.BuildWindows -logFile Builds/build.log`

Then report: build path, size, errors/warnings. Remind Can to: run the exe, resize the window, test 16:9 / 21:9 / 4:3, check SetPass in Frame Debugger (Editor) — those are his PASS items.
