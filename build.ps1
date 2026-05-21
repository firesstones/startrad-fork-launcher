param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$cscCandidates = @(
  "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
  "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
  throw "csc.exe introuvable. Installe .NET Framework Developer Pack ou Visual Studio Build Tools."
}

$out = Join-Path $dist "StarTrad.exe"
$sources = Get-ChildItem -Path $src -Filter "*.cs" | ForEach-Object { $_.FullName }

& $csc `
  /nologo `
  /target:winexe `
  /optimize+ `
  /platform:anycpu `
  /out:$out `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  $sources

if ($LASTEXITCODE -ne 0) {
  throw "Compilation StarTrad echouee."
}

$manifest = @{
  id = "startrad"
  name = "StarTrad"
  version = "1.0.1"
  build = "launcher-fork-2"
  executable = "StarTrad.exe"
} | ConvertTo-Json -Depth 3

Set-Content -Path (Join-Path $dist "startrad_launcher_version.json") -Value $manifest -Encoding UTF8
Set-Content -Path (Join-Path $dist "circus_launcher_startrad_version.json") -Value $manifest -Encoding UTF8
Write-Host "[StarTrad] build OK -> $out"
