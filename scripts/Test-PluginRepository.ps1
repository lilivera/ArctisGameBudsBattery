[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$ReleaseTag
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $repositoryRoot 'src/ArctisGameBudsBattery/ArctisGameBudsBattery.csproj') -Raw
$entries = @(Get-Content -LiteralPath (Join-Path $repositoryRoot 'pluginmaster.json') -Raw | ConvertFrom-Json)
if ($entries.Count -ne 1) { throw 'pluginmaster.jsonには1件のプラグインを定義してください。' }

$entry = $entries[0]
$metadata = $project.Project.PropertyGroup
$version = [string]$metadata.AssemblyVersion
$expectedTag = 'v' + ($version -replace '\.0$', '')
$repositoryUrl = [string]$metadata.RepoUrl
$repositoryPath = ([Uri]$repositoryUrl).AbsolutePath.Trim('/')
$downloadUrl = "https://raw.githubusercontent.com/$repositoryPath/main/dist/$expectedTag/latest.zip"

foreach ($name in @('Author', 'Name', 'InternalName', 'AssemblyVersion', 'MinimumDalamudVersion', 'RepoUrl', 'Punchline', 'Description', 'ApplicableVersion')) {
    if ($entry.$name -ne [string]$metadata.$name) { throw "プロジェクトと配布定義の値が一致しません: $name" }
}
if ($entry.DalamudApiLevel -ne 15 -or $entry.LastUpdate -le 0) {
    throw 'APIレベルまたは更新日時が不正です。'
}
foreach ($name in @('DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting')) {
    if ($entry.$name -ne $downloadUrl) { throw "配布先URLが一致しません: $name" }
}
if ($ReleaseTag -and $ReleaseTag -ne $expectedTag) { throw "タグは $expectedTag にしてください。" }

if ($PackagePath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
    try {
        foreach ($required in @('ArctisGameBudsBattery.dll', 'ArctisGameBudsBattery.Core.dll',
            'ArctisGameBudsBattery.json', 'ArctisGameBudsBattery.deps.json', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
            if ($null -eq $archive.GetEntry($required)) { throw "ZIPに必要なファイルがありません: $required" }
        }
        $reader = [System.IO.StreamReader]::new($archive.GetEntry('ArctisGameBudsBattery.json').Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        foreach ($name in @('InternalName', 'AssemblyVersion', 'Author', 'RepoUrl', 'DalamudApiLevel')) {
            if ($manifest.$name -ne $entry.$name) { throw "ZIPと配布定義の値が一致しません: $name" }
        }
    }
    finally { $archive.Dispose() }
}

Write-Host "配布定義を確認しました: $($entry.InternalName) $version ($expectedTag)"
