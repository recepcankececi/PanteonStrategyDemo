# PreToolUse guard (single source of truth for protected files).
# Blocks:
#   - text edits (Edit/Write) of Unity-serialized files and ProjectSettings
#   - shell writes/copies/deletes/moves touching those files or .meta
#   - recursive deletes under Assets/
#   - dangerous git: push, reset --hard, clean, add -A / add ., history rewrite
# Exit 2 = block; stderr is shown to the agent.
$ErrorActionPreference = 'Continue'
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$tool = $payload.tool_name
$toolInput = $payload.tool_input

# Protected Unity-serialized extensions — keep this list as the only definition.
$ext = 'unity|prefab|asset|meta|mat|spriteatlas|spriteatlasv2|anim|controller|overrideController|physicsMaterial2D|scenetemplate|shadergraph|shadersubgraph|preset|inputactions|lighting|mask|playable|signal|asmdef|asmref'
$serializedFile = "\.($ext)\b"

function Block([string]$msg) {
    [Console]::Error.WriteLine("BLOCKED: $msg")
    exit 2
}

if ($tool -in @('Edit', 'Write', 'MultiEdit')) {
    $path = [string]$toolInput.file_path
    # .asmdef/.asmref are JSON and may be authored as text when creating a module.
    if ($path -match '\.(asmdef|asmref)$') { exit 0 }
    if ($path -match "\.($ext)$" -or $path -match '[\\/]ProjectSettings[\\/]') {
        Block "'$path' is a Unity-serialized file. Change it via MCP for Unity or an Editor script (CLAUDE.md invariant 1)."
    }
    exit 0
}

if ($tool -in @('Bash', 'PowerShell')) {
    $cmd = [string]$toolInput.command

    # --- git safety ---
    if ($cmd -match '\bgit(\s+-C\s+\S+)?\s+push\b')                 { Block 'git push is forbidden; Can pushes manually.' }
    if ($cmd -match '\bgit(\s+-C\s+\S+)?\s+reset\s+--hard\b')          { Block 'git reset --hard is forbidden.' }
    if ($cmd -match '\bgit(\s+-C\s+\S+)?\s+clean\b')                   { Block 'git clean is forbidden.' }
    if ($cmd -match '\bgit(\s+-C\s+\S+)?\s+add\s+(-A|--all|\.)(\s|$)') { Block 'Stage explicit paths only (CLAUDE.md invariant 7).' }
    if ($cmd -match '\bgit(\s+-C\s+\S+)?\s+(rebase|filter-branch|filter-repo)\b' -or $cmd -match '\bgit(\s+-C\s+\S+)?\s+commit\b[^;&|]*--amend') {
        Block 'History rewriting is forbidden.'
    }

    # --- recursive delete under Assets ---
    if ($cmd -match '(\brm\s+-[a-zA-Z]*r|\bRemove-Item\b[^;&|]*-Recurse|\brmdir\b|\brd\s+/s)' -and $cmd -match 'Assets') {
        Block 'Recursive deletes under Assets/ are forbidden; delete assets through Unity (MCP).'
    }

    # --- any write/copy/move/delete touching a serialized file ---
    $mutating = '(\brm\b|\bdel\b|\bmv\b|\bmove\b|\bcp\b|\bcopy\b|\bRemove-Item\b|\bMove-Item\b|\bCopy-Item\b|\bRename-Item\b|\bSet-Content\b|\bAdd-Content\b|\bOut-File\b|\bsed\s+-i|\btee\b|>{1,2})'
    if ($cmd -match $mutating -and ($cmd -match $serializedFile -or $cmd -match 'ProjectSettings[\\/]')) {
        Block 'Shell writes/moves/deletes on Unity-serialized files are forbidden; use MCP for Unity (CLAUDE.md invariants 1-2).'
    }
}

exit 0
