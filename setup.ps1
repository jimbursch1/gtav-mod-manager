# setup.ps1 — Downloads required dependencies before building
# Run once after cloning: .\setup.ps1

$ErrorActionPreference = "Stop"

$libsDir = Join-Path $PSScriptRoot "libs"
$dll = Join-Path $libsDir "Newtonsoft.Json.dll"

if (Test-Path $dll) {
    Write-Host "Newtonsoft.Json.dll already present, skipping download." -ForegroundColor Green
    exit 0
}

New-Item -ItemType Directory -Path $libsDir -Force | Out-Null

Write-Host "Downloading Newtonsoft.Json 13.0.3..." -ForegroundColor Cyan
$nupkg = Join-Path $env:TEMP "Newtonsoft.Json.nupkg.zip"
Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Newtonsoft.Json/13.0.3" -OutFile $nupkg

$extracted = Join-Path $env:TEMP "Newtonsoft.Json.extracted"
Expand-Archive -Path $nupkg -DestinationPath $extracted -Force

Copy-Item "$extracted\lib\net45\Newtonsoft.Json.dll" $libsDir

Write-Host "Done. Newtonsoft.Json.dll placed in libs\." -ForegroundColor Green
Write-Host ""
Write-Host "To build:" -ForegroundColor Yellow
Write-Host '  MSBuild.exe GtavModManager.csproj /p:Configuration=Release'
