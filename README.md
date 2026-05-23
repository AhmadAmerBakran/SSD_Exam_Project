# SSD Exam Project — Secure File Upload Portal

Secure Software Development exam project by:

- Ahmad Amer Bakran
- Mahmoud Eybo

## Project description

This project is a secure file upload and sharing web application built with ASP.NET Core and .NET 10.

The application allows users to register, log in, upload files, view their own files, download files, delete files, and create temporary share links. The project focuses on secure software development practices such as authentication, authorization, Argon2id password hashing, CSRF protection, secure file validation, IDOR protection, security headers, dependency auditing, SBOM generation, SAST and DAST.

## Technologies

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQLite
- Argon2id password hashing
- HTML, CSS and JavaScript frontend
- SonarQube for IDE
- OWASP ZAP
- CycloneDX SBOM

## Project structure

```text
SSD_Exam_Project/
├── SSD_Exam_Project.sln
├── SecureFileUploadPortal.Api/
├── SecureFileUploadPortal.Application/
├── SecureFileUploadPortal.Domain/
├── SecureFileUploadPortal.Infrastructure/
├── scripts/
├── security-results/
└── .github/
```

## Prerequisites

Check that .NET is installed:

```bash
dotnet --version
```

This project uses .NET 10. The project was tested with:

```text
10.0.102
```

List installed SDKs:

```bash
dotnet --list-sdks
```

## Clone the repository

```bash
git clone https://github.com/AhmadAmerBakran/SSD_Exam_Project.git
cd SSD_Exam_Project
```

## Restore dependencies

```bash
dotnet restore
```

## Build the solution

```bash
dotnet build
```

## Run the application

```bash
dotnet run --project SecureFileUploadPortal.Api
```

Open the application in the browser:

```text
http://localhost:5000
```

## Run dependency vulnerability audit

PowerShell:

```powershell
.\scripts\dependency-audit.ps1
```

Manual command:

```bash
dotnet package list SSD_Exam_Project.sln --vulnerable --include-transitive
```

The result should be saved in:

```text
security-results/dependency-audit.txt
```

## Generate SBOM

PowerShell:

```powershell
.\scripts\generate-sbom-cyclonedx.ps1
```

The SBOM should be saved in:

```text
security-results/sbom/bom.json
```

## SAST

SAST was performed using SonarQube for IDE in Rider.

## Run DAST with OWASP ZAP

First run the application:

```bash
dotnet run --project SecureFileUploadPortal.Api
```

Open OWASP ZAP and scan this target:

```text
http://localhost:5000
```

In OWASP ZAP:

```text
Quick Start → Automated Scan → URL to attack: http://localhost:5000 → Attack
```

Export the report as HTML and inspect it


## Run GitHub security workflows

The project includes GitHub Actions workflows:

```text
.github/workflows/security-checks.yml
.github/workflows/codeql.yml
```

These run automatically on GitHub when the project is pushed.

## Security features

The project includes:

- Argon2id password hashing
- Unique password salts
- Server-side sessions
- HttpOnly authentication cookies
- CSRF protection
- Frontend and backend validation
- Secure file upload validation
- File size limits
- File extension allowlist
- MIME type validation
- File signature validation
- Files stored outside the public web root
- IDOR protection with owner-based access checks
- SHA-256 file integrity hashes
- Temporary share links
- Hashed share tokens
- Share-link expiration and revocation
- Rate limiting
- Security headers
- Dependency vulnerability auditing
- SBOM generation
- SAST
- DAST

## Notes

The application is an exam project prototype and is not intended as a production-ready cloud storage platform.