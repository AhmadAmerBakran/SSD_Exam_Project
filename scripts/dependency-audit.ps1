$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
New-Item -ItemType Directory -Force -Path "security-results" | Out-Null
Write-Host "Restoring packages..."
dotnet restore .\SSD_Exam_Project.sln
Write-Host "Checking for vulnerable NuGet packages..."
dotnet list .\SSD_Exam_Project.sln package --vulnerable --include-transitive | Tee-Object -FilePath "security-results\dependency-audit.txt"
Write-Host "Result saved to security-results\dependency-audit.txt"
