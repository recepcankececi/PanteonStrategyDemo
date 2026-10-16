# Stop hook: the agent may not finish while game changes are newer than the docs.
# Blocks (exit 2) when either holds:
#   a) game repo has uncommitted changes in watched paths and devdocs has none, or
#   b) the last commit touching watched paths is newer than devdocs' last commit
#      and devdocs has no pending changes.
# 'Continue': Windows PowerShell 5.1 turns native-command stderr into terminating
# errors under 'Stop'.
$ErrorActionPreference = 'Continue'
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json

# Loop guard: Claude Code sets this when the agent is already continuing because of this hook.
if ($payload.stop_hook_active) { exit 0 }

$project = $env:CLAUDE_PROJECT_DIR
$docs = Join-Path $project '..\case-devdocs'
if (-not (Test-Path (Join-Path $project '.git'))) { exit 0 }
if (-not (Test-Path (Join-Path $docs '.git'))) { exit 0 }

$watched = @('Assets/_Project', 'ProjectSettings') | Where-Object { Test-Path (Join-Path $project $_) }
if (-not $watched) { exit 0 }

$docsPending = & git -C $docs status --porcelain 2>$null
if ($docsPending) { exit 0 }

$msg = "DOCS GATE: game changes under Assets/_Project or ProjectSettings are newer than ../case-devdocs. Update Systems/<system>.md and Log.md (Turkish), add an ADR via /adr for critical decisions, commit devdocs, then finish."

$codePending = & git -C $project status --porcelain -- @watched 2>$null
if ($codePending) { [Console]::Error.WriteLine($msg); exit 2 }

$codeTs = & git -C $project log -1 --format=%ct -- @watched 2>$null
$docsTs = & git -C $docs log -1 --format=%ct 2>$null
if ($codeTs -and $docsTs -and ([long]$codeTs -gt [long]$docsTs)) {
    [Console]::Error.WriteLine($msg); exit 2
}

exit 0
