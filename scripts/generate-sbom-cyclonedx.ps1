$ErrorActionPreference = "Stop"

Set-Location (Join-Path $PSScriptRoot "..")

New-Item -ItemType Directory -Force -Path "security-results\sbom" | Out-Null

Write-Host "Installing/updating CycloneDX .NET tool if needed..."
dotnet tool update --global CycloneDX

Write-Host "Generating CycloneDX SBOM as JSON..."

dotnet-CycloneDX .\SSD_Exam_Project.sln `
  -o .\security-results\sbom `
  -F Json `
  -fn bom.json

Write-Host "SBOM generated in security-results\sbom\bom.json"