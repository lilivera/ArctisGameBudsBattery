param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $projectRoot
try {
    dotnet build ArctisGameBudsBattery.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'ビルドに失敗しました。' }

    dotnet test tests/ArctisGameBudsBattery.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'テストに失敗しました。' }

    dotnet test tests/ArctisGameBudsBattery.Plugin.Tests -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'プラグインの回帰テストに失敗しました。' }

    $packagePath = Join-Path $projectRoot 'src/ArctisGameBudsBattery/bin/Release/ArctisGameBudsBattery/latest.zip'
    & (Join-Path $PSScriptRoot 'Test-PluginRepository.ps1') -PackagePath $packagePath

    $artifactDirectory = Join-Path $projectRoot 'artifacts'
    New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
    Copy-Item -LiteralPath $packagePath -Destination (Join-Path $artifactDirectory 'latest.zip') -Force
    Write-Host "検証済みZIP: $(Join-Path $artifactDirectory 'latest.zip')"
}
finally { Pop-Location }
