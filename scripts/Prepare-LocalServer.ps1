param(
    [Parameter(Mandatory = $true)][string] $GamePath
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game = (Resolve-Path -LiteralPath $GamePath).Path
$scratch = Join-Path $repo '.scratch'
$server = Join-Path $scratch 'server'
$managed = Join-Path $scratch 'managed'
$archive = Join-Path $scratch 'BepInEx_win_x64_5.4.23.5.zip'

New-Item -ItemType Directory -Force $server, $managed | Out-Null

foreach ($name in @('Risk of Rain 2.exe', 'steam_api64.dll', 'UnityPlayer.dll', 'UnityCrashHandler64.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $server $name))) {
        Copy-Item -LiteralPath (Join-Path $game $name) -Destination $server
    }
}
foreach ($name in @('Risk of Rain 2_Data', 'MonoBleedingEdge')) {
    $destination = Join-Path $server $name
    if (-not (Test-Path -LiteralPath $destination)) {
        Copy-Item -LiteralPath (Join-Path $game $name) -Destination $server -Recurse
    }
}
Copy-Item -Path (Join-Path $server 'Risk of Rain 2_Data\Managed\*.dll') -Destination $managed -Force

if (-not (Test-Path -LiteralPath $archive)) {
    Invoke-WebRequest -Uri 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip' -OutFile $archive
}
Expand-Archive -LiteralPath $archive -DestinationPath $server -Force

$project = Join-Path $repo 'src\Ror2UnofficialDedicatedServer\Ror2UnofficialDedicatedServer.csproj'
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }

$plugins = Join-Path $server 'BepInEx\plugins'
New-Item -ItemType Directory -Force $plugins | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'src\Ror2UnofficialDedicatedServer\bin\Release\net472\Ror2UnofficialDedicatedServer.dll') -Destination $plugins -Force
Write-Host "Prepared isolated server at $server"
