# build.ps1 - Build Pixel Heroes Access and deploy it into the game. Deploy itself is the host
# project's Debug post-build target (plugin + Core + Module into BepInEx\plugins\PixelHeroesAccess,
# prism.dll next to Pixel Heroes - Byte and Magic.exe); this script checks BepInEx is installed and runs the build. Close
# the game first for a HOST change (its DLL is locked while the game runs); Core/Module changes deploy
# fine with the game running and take effect on POST /reload.

param(
    [switch]$Help,
    [switch]$ModuleOnly
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-ModuleOnly] [-Help]"
    Write-Host "  Builds the solution and deploys the mod into the game folder."
    Write-Host "  -ModuleOnly builds just the reloadable Core + Module (the hot loop)."
    Write-Host "  Run setup-bepinex.ps1 once first."
    exit 0
}

$ErrorActionPreference = "Stop"

# The game dir the build itself resolves (Directory.Build.props: Directory.Build.local.props,
# PIXEL_HEROES_PATH, Steam's registry, the default path), so this check and the deploy agree.
$Game = (dotnet msbuild "$PSScriptRoot\src\PixelHeroesAccess\PixelHeroesAccess.csproj" -getProperty:GameDir).Trim()
if (-not $Game -or -not (Test-Path "$Game\Pixel Heroes - Byte and Magic.exe")) {
    Write-Host "ERROR: Pixel Heroes not found (GameDir='$Game'). Set PIXEL_HEROES_PATH or create Directory.Build.local.props." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path "$Game\BepInEx\core\BepInEx.dll")) {
    Write-Host "ERROR: BepInEx 5 is not installed at $Game\BepInEx. Run setup-bepinex.ps1 first." -ForegroundColor Red
    exit 1
}

if ($ModuleOnly) {
    dotnet build "$PSScriptRoot\src\PixelHeroesAccess.Module\PixelHeroesAccess.Module.csproj" -c Debug
} else {
    dotnet build "$PSScriptRoot\PixelHeroesAccess.slnx" -c Debug
}
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}
Write-Host ""
Write-Host "Done. Launch Pixel Heroes through Steam, or POST /reload if it is running." -ForegroundColor Cyan
