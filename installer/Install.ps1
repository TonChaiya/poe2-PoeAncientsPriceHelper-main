$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationFramework

$productName = 'PoE2 Ground Loot Price Helper'
$installRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Poe2GroundLootPriceHelper'
$payload = Join-Path $PSScriptRoot 'App.zip'

try {
    if (-not (Test-Path -LiteralPath $payload)) {
        throw 'The installer payload is missing.'
    }

    if (Get-Process -Name 'PoeAncientsPriceHelper' -ErrorAction SilentlyContinue) {
        [System.Windows.MessageBox]::Show(
            'Close PoE2 Ground Loot Price Helper before installing, then run Setup again.',
            $productName,
            'OK',
            'Warning') | Out-Null
        exit 2
    }

    $staging = Join-Path ([IO.Path]::GetTempPath()) ('Poe2GroundLootPriceHelper-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging | Out-Null

    try {
        Expand-Archive -LiteralPath $payload -DestinationPath $staging -Force
        New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
        Copy-Item -Path (Join-Path $staging '*') -Destination $installRoot -Recurse -Force
    }
    finally {
        if (Test-Path -LiteralPath $staging) {
            Remove-Item -LiteralPath $staging -Recurse -Force
        }
    }

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination (Join-Path $installRoot 'Uninstall.ps1') -Force

    $exePath = Join-Path $installRoot 'PoeAncientsPriceHelper.exe'
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw 'The application executable was not installed.'
    }

    $shell = New-Object -ComObject WScript.Shell
    $startMenuDir = Join-Path ([Environment]::GetFolderPath('Programs')) $productName
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null

    $startShortcut = $shell.CreateShortcut((Join-Path $startMenuDir ($productName + '.lnk')))
    $startShortcut.TargetPath = $exePath
    $startShortcut.WorkingDirectory = $installRoot
    $startShortcut.IconLocation = "$exePath,0"
    $startShortcut.Save()

    $desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) ($productName + '.lnk')))
    $desktopShortcut.TargetPath = $exePath
    $desktopShortcut.WorkingDirectory = $installRoot
    $desktopShortcut.IconLocation = "$exePath,0"
    $desktopShortcut.Save()

    $uninstallScript = Join-Path $installRoot 'Uninstall.ps1'
    $uninstallShortcut = $shell.CreateShortcut((Join-Path $startMenuDir ('Uninstall ' + $productName + '.lnk')))
    $uninstallShortcut.TargetPath = 'powershell.exe'
    $uninstallShortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`""
    $uninstallShortcut.WorkingDirectory = $installRoot
    $uninstallShortcut.Save()

    $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Poe2GroundLootPriceHelper'
    New-Item -Path $uninstallKey -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayName -Value $productName -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value '1.2.0' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'TonChaiya independent fork' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $installRoot -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value "$exePath,0" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name UninstallString -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`"" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null

    Start-Process -FilePath $exePath -WorkingDirectory $installRoot
    [System.Windows.MessageBox]::Show(
        "$productName 1.2.0 was installed successfully.",
        $productName,
        'OK',
        'Information') | Out-Null
}
catch {
    [System.Windows.MessageBox]::Show(
        "Installation failed:`n$($_.Exception.Message)",
        $productName,
        'OK',
        'Error') | Out-Null
    exit 1
}
