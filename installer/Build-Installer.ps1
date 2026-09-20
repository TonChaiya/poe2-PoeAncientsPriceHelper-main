param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactsRoot = Join-Path $projectRoot '.artifacts\installer'
$publishDir = Join-Path $artifactsRoot 'publish'
$payloadDir = Join-Path $artifactsRoot 'payload'
$outputDir = Join-Path $projectRoot 'install'
$setupPath = Join-Path $outputDir 'Poe2GroundLootPriceHelper-v1.0.0-Setup.exe'
$sedPath = Join-Path $artifactsRoot 'package.sed'
$projectFile = Join-Path $projectRoot 'src\PoeAncientsPriceHelper\PoeAncientsPriceHelper.csproj'

foreach ($path in @($artifactsRoot, $outputDir)) {
    $fullPath = [IO.Path]::GetFullPath($path)
    if (-not $fullPath.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe build path: $fullPath"
    }
}

if (Test-Path -LiteralPath $artifactsRoot) {
    Remove-Item -LiteralPath $artifactsRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir, $payloadDir, $outputDir -Force | Out-Null

dotnet publish $projectFile -c $Configuration -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

$appZip = Join-Path $payloadDir 'App.zip'
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $appZip -CompressionLevel Optimal
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.cmd') -Destination $payloadDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.ps1') -Destination $payloadDir
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination $payloadDir

$sourceDir = $payloadDir.TrimEnd('\') + '\'
$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=0
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=
TargetName=$setupPath
FriendlyName=PoE 2 Ground Loot Price Helper 1.0.0 Setup
AppLaunched=Install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=Install.cmd
UserQuietInstCmd=Install.cmd
SourceFiles=SourceFiles

[SourceFiles]
SourceFiles0=$sourceDir

[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=

[Strings]
FILE0=App.zip
FILE1=Install.cmd
FILE2=Install.ps1
FILE3=Uninstall.ps1
"@
Set-Content -LiteralPath $sedPath -Value $sed -Encoding ASCII

$iexpress = Join-Path $env:WINDIR 'System32\iexpress.exe'
if (-not (Test-Path -LiteralPath $iexpress)) {
    throw 'Windows IExpress is not available.'
}
& $iexpress /N /Q $sedPath
$deadline = [DateTime]::UtcNow.AddMinutes(10)
while (-not (Test-Path -LiteralPath $setupPath) -and [DateTime]::UtcNow -lt $deadline) {
    $packagingActive = (Get-Process -Name 'iexpress', 'makecab' -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0
    if (-not $packagingActive) {
        break
    }
    Start-Sleep -Seconds 1
}

if (-not (Test-Path -LiteralPath $setupPath)) {
    throw 'IExpress failed to create the installer.'
}

Get-ChildItem -LiteralPath $outputDir -Filter ('~' + [IO.Path]::GetFileNameWithoutExtension($setupPath) + '.*') -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

$hash = Get-FileHash -LiteralPath $setupPath -Algorithm SHA256
Write-Host "Installer: $setupPath"
Write-Host "SHA256:    $($hash.Hash)"
