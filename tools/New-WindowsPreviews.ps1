# Local native WPF rendering only. No commit, upload, Actions, release or real OS writes.
[CmdletBinding()]
param(
    [switch]$Run,
    [ValidateRange(30, 1800)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
if (-not $Run) {
    Write-Host 'After Windows execution is authorized, run: pwsh tools/New-WindowsPreviews.ps1 -Run'
    Write-Host 'Requires Windows, .NET SDK and restored dependencies. Writes only local .artifacts output.'
    return
}
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Native WPF previews require Windows. No preview or validation has been produced.'
}
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $repository
try {
    $output = Join-Path $repository ('.artifacts/windows-previews-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $output | Out-Null
    $baseCommit = (& git rev-parse HEAD)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record source commit.' }
    $status = @(& git status --porcelain=v1)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record working-tree status.' }
    # Include untracked implementation files, not old screenshots or build outputs.
    $sourcePaths = @(& git ls-files --cached --others --exclude-standard -- '*.cs' '*.csproj' '*.xaml' '*.json' '*.props' '*.targets' '*.ico' '*.png' '*.ps1' |
        Where-Object { ($_ -like 'src/*' -or $_ -like 'tools/*' -or $_ -eq 'Directory.Build.props') -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
        Sort-Object -Unique)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source files.' }
    $sourceHashes = @($sourcePaths | ForEach-Object {
        [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    $sourceInventory = Join-Path $output 'source-files.json'
    ConvertTo-Json -InputObject $sourceHashes -Depth 5 | Set-Content -LiteralPath $sourceInventory -Encoding utf8
    $manifest = [ordered]@{
        schema = 1; createdUtc = [DateTime]::UtcNow.ToString('o'); baseCommit = $baseCommit.Trim()
        workingTreeDirty = ($status.Count -gt 0); workingTreeStatus = $status
        sourceInventory = 'source-files.json'; sourceInventorySha256 = (Get-FileHash $sourceInventory -Algorithm SHA256).Hash.ToLowerInvariant()
        fixtureBackend = 'isolated fake storage'; realFeatureWrites = $false; elevatedIpcRun = $false
        publicationPerformed = $false; passed = $false; failure = $null; fixtures = @(); imageCount = 0
        humanVisualReviewRequired = $true; physicalDesktopCapture = $false
    }
    $manifestPath = Join-Path $output 'preview-manifest.json'
    try {
        # --no-restore prevents this script from silently fetching dependencies.
        & dotnet run --project src/ViVeUI.Tests -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Core tests failed; restore dependencies separately if needed.' }
        & dotnet build src/ViVeUI.Windows -c Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'WPF build failed; no current native preview is validated.' }
        $app = (Resolve-Path 'src/ViVeUI.Windows/bin/Release/net8.0-windows/ViVeUI.exe').Path
        $manifest.appSha256 = (Get-FileHash $app -Algorithm SHA256).Hash.ToLowerInvariant()
        foreach ($fixture in @('smoke', 'localization-smoke', 'ux-smoke', 'catalog-smoke', 'recipe-smoke')) {
            $folder = Join-Path $output $fixture
            New-Item -ItemType Directory -Path $folder | Out-Null
            # These flags select DemoStore and per-run temporary preferences in Program.cs.
            # Deliberately exclude --ipc-smoke and every worker/real-mutation mode.
            $process = Start-Process -FilePath $app -ArgumentList "--$fixture" -WorkingDirectory $folder -PassThru
            if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
                $process.Kill(); $process.WaitForExit()
                throw "$fixture timed out. Partial files are retained and are not passing evidence."
            }
            $process.Refresh()
            if ($process.ExitCode -ne 0) {
                Get-Content (Join-Path $folder 'smoke-error.txt') -ErrorAction SilentlyContinue
                throw "$fixture failed with exit code $($process.ExitCode)."
            }
            $report = switch ($fixture) {
                'localization-smoke' { 'localization-result.json' }
                'ux-smoke' { 'ux-result.json' }
                'catalog-smoke' { 'catalog-result.json' }
                'recipe-smoke' { 'recipe-result.json' }
                default { 'smoke-result.txt' }
            }
            $reportPath = Join-Path $folder $report
            if (-not (Test-Path $reportPath)) { throw "$fixture did not write its expected report: $report" }
            if ($report.EndsWith('.json')) {
                $result = Get-Content $reportPath -Raw | ConvertFrom-Json
                if ($result.passed -ne $true) { throw "$fixture report does not declare success." }
            }
            $images = @(Get-ChildItem $folder -Filter '*.png' -Recurse -File)
            if ($images.Count -eq 0) { throw "$fixture produced no native images." }
            $manifest.fixtures += [ordered]@{ argument = "--$fixture"; directory = $fixture; report = $report; imageCount = $images.Count; exitCode = $process.ExitCode }
        }
        $manifest.imageCount = @(Get-ChildItem $output -Filter '*.png' -Recurse -File).Count
        $manifest.passed = $true
    } catch {
        $manifest.failure = $_.Exception.Message
        throw
    } finally {
        $manifest.completedUtc = [DateTime]::UtcNow.ToString('o')
        $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
        Write-Host "Local evidence: $output"
    }
    Write-Host "Inspect $($manifest.imageCount) newly rendered images. Human visual, RTL, accessibility and native-speaker review remain necessary."
} finally {
    Pop-Location
}
