# Builds CasinoExpansion and produces a Thunderstore-layout zip in dist/.
$ErrorActionPreference = 'Stop'

$root     = $PSScriptRoot
$proj     = Join-Path $root 'src\CasinoExpansion.csproj'
$manifest = Join-Path $root 'package\manifest.json'
$dist     = Join-Path $root 'dist'
$staging  = Join-Path $root 'dist\_staging'

$version = (Get-Content $manifest -Raw | ConvertFrom-Json).version_number
Write-Host "Packaging CasinoExpansion $version"

dotnet build $proj -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw "build failed" }

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $staging 'Mods') | Out-Null

Copy-Item (Join-Path $root 'src\bin\Release\CasinoExpansion.dll') (Join-Path $staging 'Mods')
Copy-Item $manifest $staging
Copy-Item (Join-Path $root 'package\README.md') $staging

$icon = Join-Path $root 'package\icon.png'
if (Test-Path $icon) {
    Copy-Item $icon $staging
} else {
    Write-Warning "package\icon.png missing - fine for sharing directly, but Thunderstore upload requires a 512x512 icon.png"
}

$zip = Join-Path $dist "CasinoExpansion-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
Remove-Item $staging -Recurse -Force

Write-Host "`nReady to send: $zip"
