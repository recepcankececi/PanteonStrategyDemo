# Stop hook: if game code has uncommitted changes but case-devdocs has none,
# block the stop and tell the agent to update the docs. Exit 2 = keep working.
$ErrorActionPreference = 'Stop'
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json

# Avoid infinite loops: if this stop was already triggered by this hook, let it end.
if ($payload.stop_hook_active) { exit 0 }

$project = $env:CLAUDE_PROJECT_DIR
$docs = Join-Path $project '..\case-devdocs'

if (-not (Test-Path (Join-Path $project '.git'))) { exit 0 }
if (-not (Test-Path (Join-Path $docs '.git'))) { exit 0 }

$codeChanges = git -C $project status --porcelain -- 'Assets/_Project/Scripts' 'Assets/_Project/Tests' 2>$null
if (-not $codeChanges) { exit 0 }

$docChanges = git -C $docs status --porcelain 2>$null
if ($docChanges) { exit 0 }

[Console]::Error.WriteLine("DOCS GATE: code under Assets/_Project changed but ../case-devdocs has no changes. Update Systems/<system>.md and Log.md (Turkish), add an ADR via /adr for any critical decision, then finish.")
exit 2
