---
name: build-win
description: Produce the Windows standalone build into Builds/Windows. Only when Can asks.
disable-model-invocation: true
---

# /build-win

Precondition: `Assets/_Project/Scripts/EditorTools/BuildScript.cs` exists (created in M0) with
menu `Tools/Build/Windows` and static method `StrategyDemo.EditorTools.BuildScript.BuildWindows`.

- **Editor open (normal case):** execute menu item `Tools/Build/Windows` via MCP, then read the console for the build result.
- **Editor closed:** batchmode (Unity path in `../case-devdocs/Tooling/Kurulum.md`):
  `"<UnityPath>\Unity.exe" -batchmode -quit -projectPath . -executeMethod StrategyDemo.EditorTools.BuildScript.BuildWindows -logFile Builds/build.log`

Report: build path, size, errors/warnings. Remind Can to run the exe, resize the window, try 16:9 / 21:9 / 4:3, and check SetPass in the Frame Debugger — his PASS items.
