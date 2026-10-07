param(
    [string]$Sts2Path = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2',
    [string]$BaseLibDll = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\2868840\3737335127\BaseLib\BaseLib.dll',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\beta-build')
)
$ErrorActionPreference = 'Stop'
$taskOutputPath = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $taskOutputPath -Force | Out-Null
& dotnet build (Join-Path $PSScriptRoot 'ShengZhuSts2Mod.csproj') -c Release "-p:Sts2Path=$Sts2Path" "-p:BaseLibWorkshopDll=$BaseLibDll" "-p:ModsPath=$taskOutputPath/"
if ($LASTEXITCODE -ne 0) { throw "Beta build failed: exit code $LASTEXITCODE" }
Write-Host "Built DLL and manifest: $taskOutputPath\ShengZhuSts2Mod"
