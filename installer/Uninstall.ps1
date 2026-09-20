$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework

$productName = 'PoE2 Ground Loot Price Helper'
$installRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$answer = [System.Windows.MessageBox]::Show(
    "Remove $productName from this computer?",
    $productName,
    'YesNo',
    'Question')

if ($answer -ne 'Yes') {
    exit 0
}

Get-Process -Name 'PoeAncientsPriceHelper' -ErrorAction SilentlyContinue | Stop-Process -Force

$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) ($productName + '.lnk')
$startMenuDir = Join-Path ([Environment]::GetFolderPath('Programs')) $productName
if (Test-Path -LiteralPath $desktopShortcut) {
    Remove-Item -LiteralPath $desktopShortcut -Force
}
if (Test-Path -LiteralPath $startMenuDir) {
    Remove-Item -LiteralPath $startMenuDir -Recurse -Force
}
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Poe2GroundLootPriceHelper' -Recurse -Force -ErrorAction SilentlyContinue

$cleanup = Join-Path ([IO.Path]::GetTempPath()) ('Poe2GroundLootPriceHelper-cleanup-' + [Guid]::NewGuid().ToString('N') + '.ps1')
@"
Start-Sleep -Seconds 2
if (Test-Path -LiteralPath '$($installRoot.Replace("'", "''"))') {
    Remove-Item -LiteralPath '$($installRoot.Replace("'", "''"))' -Recurse -Force
}
Remove-Item -LiteralPath `$MyInvocation.MyCommand.Path -Force
"@ | Set-Content -LiteralPath $cleanup -Encoding UTF8

Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $cleanup) -WindowStyle Hidden
[System.Windows.MessageBox]::Show(
    "$productName was removed. Your saved settings were kept.",
    $productName,
    'OK',
    'Information') | Out-Null
