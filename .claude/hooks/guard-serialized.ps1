# PreToolUse guard: blocks direct text edits of Unity-serialized files and
# shell deletes/moves of .meta files. Exit 2 = block; stderr goes back to the agent.
$ErrorActionPreference = 'Stop'
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$tool = $payload.tool_name
$input_ = $payload.tool_input

$serialized = '\.(unity|prefab|asset|meta|mat|spriteatlas|anim|controller|physicsMaterial2D)$'

if ($tool -in @('Edit', 'Write', 'MultiEdit')) {
    $path = [string]$input_.file_path
    if ($path -match $serialized -or $path -match '[\\/]ProjectSettings[\\/]') {
        [Console]::Error.WriteLine("BLOCKED: '$path' is a Unity-serialized file. Change it through MCP for Unity tools or an Editor script (CLAUDE.md invariant 1).")
        exit 2
    }
}

if ($tool -eq 'Bash') {
    $cmd = [string]$input_.command
    if ($cmd -match '(rm|del|mv|move|Remove-Item|Move-Item)\b.*\.meta\b') {
        [Console]::Error.WriteLine("BLOCKED: shell operations on .meta files are forbidden. Move/rename/delete assets through Unity via MCP (CLAUDE.md invariant 2).")
        exit 2
    }
}

exit 0
