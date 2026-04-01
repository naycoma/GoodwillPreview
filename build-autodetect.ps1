$steamPath = (Get-ItemProperty "HKCU:\Software\Valve\Steam").SteamPath
$vdf = Get-Content "$steamPath/steamapps/libraryfolders.vdf" -Raw
$paths = [regex]::Matches($vdf, '"path"\s+"([^"]+)"') | ForEach-Object {
    $_.Groups[1].Value -replace '\\\\', '\'
}

$rimWorldPath = $paths |
    ForEach-Object { Join-Path $_ "steamapps\common\RimWorld" } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1

if (-not $rimWorldPath) {
    Write-Error "RimWorld not found in any Steam library"
    exit 1
}

$steamAppsPath = Split-Path (Split-Path $rimWorldPath -Parent) -Parent
$workshopPath = Join-Path $steamAppsPath "workshop\content\294100"
$localModsPath = Join-Path $rimWorldPath "Mods"
Write-Host "RimWorldPath:  $rimWorldPath"
Write-Host "LocalModsPath: $localModsPath"

dotnet build "$PSScriptRoot/mod.csproj" `
    -p:RimWorldPath="$rimWorldPath" `
    -p:LocalModsPath="$localModsPath"
