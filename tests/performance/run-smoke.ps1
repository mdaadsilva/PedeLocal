# Runs every scenario once at PROFILE=smoke — meant for quick local sanity checks before a real
# load/stress run. See docs/PERFORMANCE_TESTING.md.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Get-Command k6 -ErrorAction SilentlyContinue)) {
    Write-Error "k6 not found on PATH — see docs/PERFORMANCE_TESTING.md#instalação-do-k6"
    exit 1
}

$env:PROFILE = "smoke"
New-Item -ItemType Directory -Force -Path "results" | Out-Null

$failed = $false
Get-ChildItem -Path "scenarios" -Filter "*.js" | ForEach-Object {
    $name = $_.BaseName
    Write-Host "=== smoke: $name ===" -ForegroundColor Cyan
    k6 run --summary-export "results/$name-smoke-summary.json" $_.FullName
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "smoke FAILED: $name"
        $failed = $true
    }
}

if ($failed) { exit 1 }
