param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$manifest = Join-Path $project 'Packages/manifest.json'
$lock = Join-Path $project 'Packages/packages-lock.json'
$backup = Join-Path $project '.artifacts/published-packages'
if ($Restore) {
    if (!(Test-Path -LiteralPath (Join-Path $backup 'manifest.json'))) { throw 'No saved published package configuration.' }
    Copy-Item -LiteralPath (Join-Path $backup 'manifest.json') -Destination $manifest
    if (Test-Path -LiteralPath (Join-Path $backup 'packages-lock.json')) { Copy-Item -LiteralPath (Join-Path $backup 'packages-lock.json') -Destination $lock }
    Write-Output 'Published Core pin restored. Reopen Unity to resolve packages.'
    exit
}
$core = Join-Path (Split-Path -Parent $project) 'Core/Packages/com.terraloom.core'
if (!(Test-Path -LiteralPath $core -PathType Container)) { throw 'Expected the sibling Core project.' }
$data = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
if ($data.dependencies.'com.terraloom.core' -notlike 'file:*') {
    New-Item -ItemType Directory -Force -Path $backup | Out-Null
    Copy-Item -LiteralPath $manifest -Destination (Join-Path $backup 'manifest.json')
    if (Test-Path -LiteralPath $lock) { Copy-Item -LiteralPath $lock -Destination (Join-Path $backup 'packages-lock.json') }
}
$data.dependencies.'com.terraloom.core' = 'file:../../Core/Packages/com.terraloom.core'
[IO.File]::WriteAllText($manifest, ($data | ConvertTo-Json -Depth 30) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output 'Local Core enabled for development. Run -Restore before committing package configuration.'
