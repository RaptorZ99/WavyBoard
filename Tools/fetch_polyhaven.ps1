# Re-downloads the Poly Haven CC0 assets excluded from git (see .gitignore) into Assets/ThirdParty/PolyHaven.
# Requires Python 3. Run from the project root: powershell -ExecutionPolicy Bypass -File Tools/fetch_polyhaven.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
python "$root/Tools/polyhaven_dl.py" "$root/Assets/ThirdParty/PolyHaven" "$root/Tools/polyhaven_spec.txt"
Write-Host "Done. Open the project in Unity to import the files (the .meta files are already versioned)."
