param([string]$Version = '1.0.0')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$setup = Join-Path $root "install\Poe2GroundLootPriceHelper-v$Version-Setup.exe"
$installScript = Join-Path $root 'installer\Install.ps1'

if (-not (Test-Path -LiteralPath $setup)) { throw "Missing installer: $setup" }
if ((Get-Item -LiteralPath $setup).Length -lt 1MB) { throw 'Installer is unexpectedly small.' }

$verifyDir = Join-Path $root '.artifacts\installer\verify'
if (Test-Path -LiteralPath $verifyDir) { Remove-Item -LiteralPath $verifyDir -Recurse -Force }
New-Item -ItemType Directory -Path $verifyDir | Out-Null
$setupExtract = Join-Path $verifyDir 'setup'
$appExtract = Join-Path $verifyDir 'app'
New-Item -ItemType Directory -Path $setupExtract, $appExtract | Out-Null
$extract = Start-Process -FilePath $setup -ArgumentList "/T:$setupExtract", '/C', '/Q' -Wait -PassThru
if ($extract.ExitCode -ne 0) { throw "Installer extraction failed with exit code $($extract.ExitCode)." }
$zip = Join-Path $setupExtract 'App.zip'
if (-not (Test-Path -LiteralPath $zip)) { throw 'Installer does not contain App.zip.' }
Expand-Archive -LiteralPath $zip -DestinationPath $appExtract

$exe = Join-Path $appExtract 'PoeAncientsPriceHelper.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Payload does not contain the application executable.' }
$fileVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
if (-not $fileVersion.StartsWith($Version, [StringComparison]::Ordinal)) {
    throw "Payload version $fileVersion does not match $Version."
}
$scriptText = Get-Content -LiteralPath $installScript -Raw
if ($scriptText -notmatch "DisplayVersion -Value '$([Regex]::Escape($Version))'") {
    throw 'Installer registry version does not match.'
}

$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
Write-Host "Verified payload version: $fileVersion"
Write-Host "Installer bytes: $((Get-Item -LiteralPath $setup).Length)"
Write-Host "SHA256: $hash"
