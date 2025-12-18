<#
.SYNOPSIS
    Publishes PanoramicData.SyslogServer to NuGet.org
.PARAMETER SkipTests
    Skip running unit tests
#>
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

# Step 1: Check for git porcelain (ensure working directory is clean)
Write-Host "Checking for clean git working directory..." -ForegroundColor Cyan
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Error "Git working directory is not clean. Please commit or stash your changes."
    exit 1
}
Write-Host "Git working directory is clean." -ForegroundColor Green

# Step 2: Determine the Nerdbank git version
Write-Host "Determining Nerdbank git version..." -ForegroundColor Cyan
$version = nbgv get-version -v NuGetPackageVersion
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to determine Nerdbank git version. Ensure nbgv is installed (dotnet tool install -g nbgv)."
    exit 1
}
Write-Host "Version: $version" -ForegroundColor Green

# Step 3: Check that nuget-key.txt exists, has content and is gitignored
Write-Host "Checking nuget-key.txt..." -ForegroundColor Cyan
$nugetKeyPath = Join-Path $PSScriptRoot "nuget-key.txt"

if (-not (Test-Path $nugetKeyPath)) {
    Write-Error "nuget-key.txt does not exist in the solution root."
    exit 1
}

$nugetKey = (Get-Content $nugetKeyPath -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($nugetKey)) {
    Write-Error "nuget-key.txt is empty."
    exit 1
}

$gitIgnorePath = Join-Path $PSScriptRoot ".gitignore"
if (-not (Test-Path $gitIgnorePath)) {
    Write-Error ".gitignore file does not exist."
    exit 1
}

$gitIgnoreContent = Get-Content $gitIgnorePath -Raw
if ($gitIgnoreContent -notmatch 'nuget-key\.txt') {
    Write-Error "nuget-key.txt is not listed in .gitignore. Please add it to protect your API key."
    exit 1
}
Write-Host "nuget-key.txt is valid and gitignored." -ForegroundColor Green

# Step 4: Run unit tests (unless -SkipTests is specified)
if (-not $SkipTests) {
    Write-Host "Running unit tests..." -ForegroundColor Cyan
    dotnet test --configuration Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Unit tests failed."
        exit 1
    }
    Write-Host "Unit tests passed." -ForegroundColor Green
} else {
    Write-Host "Skipping unit tests." -ForegroundColor Yellow
}

# Step 5: Build and publish to nuget.org
Write-Host "Building and publishing to NuGet.org..." -ForegroundColor Cyan

$projectPath = Join-Path $PSScriptRoot "PanoramicData.SyslogServer\PanoramicData.SyslogServer.csproj"
dotnet build $projectPath --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}

$nupkgPath = Join-Path $PSScriptRoot "PanoramicData.SyslogServer\bin\Release\PanoramicData.SyslogServer.$version.nupkg"
if (-not (Test-Path $nupkgPath)) {
    Write-Error "NuGet package not found at: $nupkgPath"
    exit 1
}

dotnet nuget push $nupkgPath --api-key $nugetKey --source https://api.nuget.org/v3/index.json --skip-duplicate
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish to NuGet.org."
    exit 1
}

Write-Host "Successfully published PanoramicData.SyslogServer version $version to NuGet.org" -ForegroundColor Green
exit 0
