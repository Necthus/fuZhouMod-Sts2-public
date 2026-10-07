param(
    [string]$Sts2Path = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2',
    [string]$ModDirectory,
    [string]$BackupDirectory = (Join-Path $PSScriptRoot 'backups')
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue) {
    throw 'Please exit Slay the Spire 2 before replacing the mod DLL.'
}
$taskGamePath = [IO.Path]::GetFullPath($Sts2Path)
if (!(Test-Path -LiteralPath (Join-Path $taskGamePath 'data_sts2_windows_x86_64\sts2.dll'))) {
    throw "Game assembly not found under $taskGamePath"
}
if (!$ModDirectory) {
    $taskWorkshopPath = [IO.Path]::GetFullPath((Join-Path $taskGamePath '..\..\workshop\content\2868840\3747713325\ShengZhuSts2Mod'))
    if (Test-Path -LiteralPath (Join-Path $taskWorkshopPath 'ShengZhuSts2Mod.json')) {
        $ModDirectory = $taskWorkshopPath
    } else {
        $ModDirectory = Join-Path $taskGamePath 'mods\ShengZhuSts2Mod'
    }
}
$taskTargetPath = [IO.Path]::GetFullPath($ModDirectory)
if ([IO.Path]::GetFileName($taskTargetPath.TrimEnd('\', '/')) -ne 'ShengZhuSts2Mod') {
    throw "Expected a ShengZhuSts2Mod directory: $taskTargetPath"
}
$taskPackageManifestPath = Join-Path $PSScriptRoot 'ShengZhuSts2Mod.json'
$taskPackageDllPath = Join-Path $PSScriptRoot 'ShengZhuSts2Mod.dll'
if (!(Test-Path -LiteralPath $taskPackageDllPath) -or !(Test-Path -LiteralPath $taskPackageManifestPath)) {
    throw 'Run this installer from the distribution package beside its DLL and JSON.'
}
$taskPackageManifest = Get-Content -LiteralPath $taskPackageManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$taskManifestPath = Join-Path $taskTargetPath 'ShengZhuSts2Mod.json'
$taskDllPath = Join-Path $taskTargetPath 'ShengZhuSts2Mod.dll'
$taskPckPath = Join-Path $taskTargetPath 'ShengZhuSts2Mod.pck'
$taskNewManifest = $taskPackageManifest
if (Test-Path -LiteralPath $taskManifestPath) {
    $taskNewManifest = Get-Content -LiteralPath $taskManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($taskNewManifest.id -ne 'ShengZhuSts2Mod') { throw 'Existing manifest belongs to a different mod.' }
    foreach ($taskProperty in @('version', 'min_game_version', 'dependencies', 'has_dll', 'has_pck')) {
        $taskNewManifest | Add-Member -NotePropertyName $taskProperty -NotePropertyValue $taskPackageManifest.$taskProperty -Force
    }
}
if (!(Test-Path -LiteralPath $taskPckPath) -and !(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ShengZhuSts2Mod.pck'))) {
    throw 'The resource PCK is missing both from the installed mod and from the package.'
}
$taskBackupPath = Join-Path ([IO.Path]::GetFullPath($BackupDirectory)) (Get-Date -Format 'yyyyMMdd-HHmmss-ffff')
New-Item -ItemType Directory -Path $taskBackupPath -Force | Out-Null
foreach ($taskExistingPath in @($taskDllPath, $taskManifestPath)) {
    if (Test-Path -LiteralPath $taskExistingPath) {
        Copy-Item -LiteralPath $taskExistingPath -Destination $taskBackupPath
    }
}
New-Item -ItemType Directory -Path $taskTargetPath -Force | Out-Null
try {
    Copy-Item -LiteralPath $taskPackageDllPath -Destination $taskDllPath -Force
    if (!(Test-Path -LiteralPath $taskPckPath)) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ShengZhuSts2Mod.pck') -Destination $taskPckPath
    }
    $taskJson = $taskNewManifest | ConvertTo-Json -Depth 30
    [IO.File]::WriteAllText($taskManifestPath, $taskJson, [Text.UTF8Encoding]::new($false))
} catch {
    foreach ($taskFileName in @('ShengZhuSts2Mod.dll', 'ShengZhuSts2Mod.json')) {
        $taskBackupFile = Join-Path $taskBackupPath $taskFileName
        if (Test-Path -LiteralPath $taskBackupFile) {
            Copy-Item -LiteralPath $taskBackupFile -Destination (Join-Path $taskTargetPath $taskFileName) -Force
        }
    }
    throw
}
Write-Host "Installed $($taskPackageManifest.version): $taskTargetPath"
Write-Host "Backup: $taskBackupPath"
