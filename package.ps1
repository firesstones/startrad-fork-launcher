$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $root "build.ps1")
if ($LASTEXITCODE -ne 0) {
  throw "Build StarTrad echoue."
}

$isccCandidates = @(
  "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
  throw "ISCC.exe introuvable. Installe Inno Setup 6 pour generer l'installateur."
}

Push-Location (Join-Path $root "installer")
try {
  & $iscc "StarTradForLauncher.iss"
  if ($LASTEXITCODE -ne 0) {
    throw "Compilation Inno Setup echouee."
  }
} finally {
  Pop-Location
}

Write-Host "[StarTrad] package OK"
