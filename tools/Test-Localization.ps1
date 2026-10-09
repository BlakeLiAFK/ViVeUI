# Run only after local Windows execution is authorized. Uses fake storage; never publishes.
# For the full immediate-UI evidence set, prefer New-WindowsPreviews.ps1 -Run.
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet run --project src/ViVeUI.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core/localization tests failed' }
    dotnet build src/ViVeUI.Windows -c Release
    if ($LASTEXITCODE -ne 0) { throw 'WPF build failed' }
    $app = Resolve-Path src/ViVeUI.Windows/bin/Release/net8.0-windows/ViVeUI.exe
    $output = Join-Path (Get-Location) ('.artifacts/localization-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $output | Out-Null
    $p = Start-Process $app -ArgumentList '--localization-smoke' -WorkingDirectory $output -PassThru
    if (-not $p.WaitForExit(300000)) { $p.Kill(); throw 'Localization smoke timed out' }
    if ($p.ExitCode -ne 0) { Get-Content "$output/smoke-error.txt" -ErrorAction SilentlyContinue; throw 'Localization smoke failed' }
    Get-Content "$output/localization-result.json"
    $imageCount = @(Get-ChildItem (Join-Path $output 'previews/localization') -Filter '*.png' -Recurse -File).Count
    Write-Host "Inspect $imageCount native renders in $output/previews/localization. Native-speaker and RTL/shaping review is still required."
} finally { Pop-Location }
