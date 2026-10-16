# PreToolUse guard (single source of truth for protected files).
# Blocks:
#   - text edits (Edit/Write) of Unity-serialized files, ProjectSettings and package manifests
#   - shell writes/copies/deletes/moves touching those files or .meta
#   - recursive deletes under Assets/
#   - dangerous git: push, reset --hard, clean, add -A / add ., history rewrite
# Exit 2 = block; stderr is shown to the agent.
# Known gap: targets hidden behind variables or command substitution ($f, $(...)) cannot be seen.
$ErrorActionPreference = 'Continue'
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$tool = $payload.tool_name
$toolInput = $payload.tool_input

# Protected Unity-serialized extensions — keep this list as the only definition.
$ext = 'unity|prefab|asset|meta|mat|spriteatlas|spriteatlasv2|anim|controller|overrideController|physicsMaterial2D|scenetemplate|shadergraph|shadersubgraph|preset|inputactions|lighting|mask|playable|signal|asmdef|asmref'
# Negative lookahead instead of \b: '-' is a word boundary, so \b let ".unity-mcp" match.
$serializedFile = "\.($ext)(?![\w-])"
# Package manifests: invariant 6 (no new packages without Can's approval).
$packageManifest = 'Packages[\\/](manifest|packages-lock)\.json'

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
    if ($path -match $packageManifest) {
        Block "'$path' changes packages; that needs Can's approval and goes through the Package Manager (CLAUDE.md invariant 6)."
    }
    exit 0
}

if ($tool -in @('Bash', 'PowerShell')) {
    $raw = [string]$toolInput.command

    # Two views of the command:
    #   $protectionCmd — literal text that is never executed (heredoc bodies, commit messages) removed,
    #                    quotes unwrapped so a quoted path is still seen.
    #   $gitCmd        — additionally drops all quoted text, so words like "push" inside an argument
    #                    cannot trip the git checks.
    # Heredoc: keep the opener line (it may carry a redirect, e.g. cat <<'EOF' > x.prefab), drop only the body.
    $text = [regex]::Replace($raw, "(?ms)(<<-?[ \t]*(['""]?)(\w+)\2[^\n]*)\n.*?^[ \t]*\3[ \t]*$", '$1')
    $text = [regex]::Replace($text, "(?ms)@(['""])\r?\n.*?^\1@", '')   # PowerShell here-strings
    $text = [regex]::Replace($text, "(?<=\s)(-m|-F|--message|--file)(=|\s+)(""(?:[^""\\]|\\.)*""|'[^']*'|\S+)", '')
    $gitCmd = [regex]::Replace($text, "'[^']*'|""(?:[^""\\]|\\.)*""", '')
    $protectionCmd = $text -replace "['""]", ''

    # --- git safety ---
    if ($gitCmd -match '\bgit(\s+-C\s+\S+)?\s+push\b')                 { Block 'git push is forbidden; Can pushes manually.' }
    if ($gitCmd -match '\bgit(\s+-C\s+\S+)?\s+reset\s+--hard\b')          { Block 'git reset --hard is forbidden.' }
    if ($gitCmd -match '\bgit(\s+-C\s+\S+)?\s+clean\b')                   { Block 'git clean is forbidden.' }
    if ($gitCmd -match '\bgit(\s+-C\s+\S+)?\s+add\s+(-A|--all|\.)(\s|$)') { Block 'Stage explicit paths only (CLAUDE.md invariant 7).' }
    if ($gitCmd -match '\bgit(\s+-C\s+\S+)?\s+(rebase|filter-branch|filter-repo)\b' -or $gitCmd -match '\bgit(\s+-C\s+\S+)?\s+commit\b[^;&|]*--amend') {
        Block 'History rewriting is forbidden.'
    }

    # --- recursive delete under Assets ---
    if ($protectionCmd -match '(\brm\s+-[a-zA-Z]*r|\bRemove-Item\b[^;&|]*-Recurse|\brmdir\b|\brd\s+/s)' -and $protectionCmd -match 'Assets') {
        Block 'Recursive deletes under Assets/ are forbidden; delete assets through Unity (MCP).'
    }

    $protectedTarget = "$serializedFile|ProjectSettings[\\/]|$packageManifest"
    $blockMessage = 'Shell writes/moves/deletes on Unity-serialized files or package manifests are forbidden; use MCP for Unity (CLAUDE.md invariants 1, 2, 6).'

    # --- redirections: only the redirect target is written ---
    # '>&1', '>&2', '>&-' duplicate or close a stream and write no file; '>&file', '>|' and '&>' do write.
    foreach ($redirect in [regex]::Matches($protectionCmd, '>{1,2}\|?(?!&[\d-])&?\s*([^\s;&|<>]+)')) {
        if ($redirect.Groups[1].Value -match $protectedTarget) { Block $blockMessage }
    }

    # --- write/copy/move/delete commands touching a protected file ---
    $mutating = @(
        '\b(rm|del|mv|move|cp|copy|tee|truncate|touch|ln|install|xcopy|robocopy)\b',
        '\b(Remove-Item|Move-Item|Copy-Item|Rename-Item|New-Item|Set-Content|Add-Content|Clear-Content|Out-File)\b',
        '(^|[;&|(]\s*)(sc|ac|ni|ri|mi|cpi|rni)\s',                    # PowerShell aliases, command position only
        '\bsed\b[^;&|]*\s(-[a-zA-Z]*i|--in-place)',
        '\bperl\b[^;&|]*\s-[a-zA-Z]*i',
        '\bdd\b[^;&|]*\bof=',
        '\bgit(\s+-C\s+\S+)?\s+(checkout|restore|stash|rm|mv)\b',    # discard or move tracked changes
        'IO\.File\]::(Write|Append|Copy|Move|Delete|Replace)',
        '\bpython[\d.]*\s+-c\b',
        '\bnode\s+-e\b'
    ) -join '|'
    if ($protectionCmd -match $mutating -and $protectionCmd -match $protectedTarget) {
        Block $blockMessage
    }
}

exit 0
