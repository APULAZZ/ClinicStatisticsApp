$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $PSScriptRoot 'publish'
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'

& "$env:ProgramFiles\dotnet\dotnet.exe" publish (Join-Path $root 'ClinicStatisticsApp.UI\ClinicStatisticsApp.UI.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Remove-Item (Join-Path $publish 'appsettings.Local.json') -Force -ErrorAction SilentlyContinue
& $iscc (Join-Path $PSScriptRoot 'ClinicStatisticsApp.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
