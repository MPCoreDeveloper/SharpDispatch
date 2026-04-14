# SharpDispatch Package Publishing Script
# Publiceert zowel het .nupkg als .snupkg naar NuGet.org

param(
    [Parameter(Mandatory = $true)]
    [string]$ApiKey,
    
    [string]$SourceUrl = "https://api.nuget.org/v3/index.json",
    
    [switch]$SkipSymbols,
    
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Kleuren voor output
$colors = @{
    Success = "Green"
    Error   = "Red"
    Warning = "Yellow"
    Info    = "Cyan"
}

function Write-ColoredOutput {
    param(
        [string]$Message,
        [string]$Type = "Info"
    )
    Write-Host $Message -ForegroundColor $colors[$Type]
}

function Invoke-PackagePublish {
    param(
        [string]$PackagePath,
        [string]$PackageName
    )
    
    Write-ColoredOutput "`n📦 Publishing $PackageName..." -Type "Info"
    Write-ColoredOutput "  Path: $PackagePath" -Type "Info"
    
    if ($DryRun) {
        Write-ColoredOutput "  [DRY RUN] Would execute:" -Type "Warning"
        Write-ColoredOutput "    dotnet nuget push `"$PackagePath`" --api-key [REDACTED] --source $SourceUrl" -Type "Info"
        return $true
    }
    
    try {
        & dotnet nuget push "$PackagePath" `
            --api-key $ApiKey `
            --source $SourceUrl
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColoredOutput "  ✓ Successfully pushed $PackageName" -Type "Success"
            return $true
        } else {
            Write-ColoredOutput "  ✗ Failed to push $PackageName (exit code: $LASTEXITCODE)" -Type "Error"
            return $false
        }
    } catch {
        Write-ColoredOutput "  ✗ Error pushing $PackageName : $_" -Type "Error"
        return $false
    }
}

# Main Script
Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║      SharpDispatch NuGet Package Publishing Script          ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

# Validate API Key
if ($ApiKey -eq "YOUR_NUGET_API_KEY" -or [string]::IsNullOrWhiteSpace($ApiKey)) {
    Write-ColoredOutput "`n✗ Error: Valid API key required!" -Type "Error"
    Write-ColoredOutput "  Get your API key from: https://www.nuget.org/account/ApiKeys" -Type "Info"
    exit 1
}

Write-ColoredOutput "`n🔐 Configuration:" -Type "Info"
Write-ColoredOutput "  Source: $SourceUrl" -Type "Info"
Write-ColoredOutput "  API Key: [$($ApiKey.Substring(0, 4))...$($ApiKey.Substring($ApiKey.Length - 4))]" -Type "Info"
if ($SkipSymbols) {
    Write-ColoredOutput "  Symbol Package: SKIPPED" -Type "Warning"
}
if ($DryRun) {
    Write-ColoredOutput "  Mode: DRY RUN (no packages will be published)" -Type "Warning"
}

# Find package files
$releaseDir = "src\SharpDispatch\bin\Release"
$nupkg = Get-ChildItem -Path $releaseDir -Filter "*.nupkg" -Exclude "*.snupkg" | Select-Object -First 1
$snupkg = Get-ChildItem -Path $releaseDir -Filter "*.snupkg" | Select-Object -First 1

if (-not $nupkg) {
    Write-ColoredOutput "`n✗ Error: NuGet package not found in $releaseDir" -Type "Error"
    Write-ColoredOutput "  Run 'dotnet pack -c Release' first" -Type "Info"
    exit 1
}

Write-ColoredOutput "`n📁 Found Packages:" -Type "Info"
Write-ColoredOutput "  .nupkg: $($nupkg.Name)" -Type "Info"
if ($snupkg) {
    Write-ColoredOutput "  .snupkg: $($snupkg.Name)" -Type "Info"
} else {
    Write-ColoredOutput "  .snupkg: NOT FOUND" -Type "Warning"
}

# Publish packages
$nupkgSuccess = Invoke-PackagePublish -PackagePath $nupkg.FullName -PackageName "NuGet Package (.nupkg)"

$snupkgSuccess = $true
if (-not $SkipSymbols -and $snupkg) {
    $snupkgSuccess = Invoke-PackagePublish -PackagePath $snupkg.FullName -PackageName "Symbol Package (.snupkg)"
}

# Summary
Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan

if ($nupkgSuccess -and $snupkgSuccess) {
    Write-Host "║              ✓ PUBLISHING COMPLETE!                        ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    
    Write-ColoredOutput "`n✨ Next Steps:" -Type "Success"
    Write-ColoredOutput "  1. Wait 5-10 minutes for NuGet.org to index" -Type "Info"
    Write-ColoredOutput "  2. Verify at: https://www.nuget.org/packages/SharpDispatch/" -Type "Info"
    Write-ColoredOutput "  3. Test: dotnet add package SharpDispatch" -Type "Info"
    Write-ColoredOutput "  4. Create GitHub release tag" -Type "Info"
    Write-ColoredOutput "  5. Announce on social media" -Type "Info"
    
    exit 0
} else {
    Write-Host "║              ✗ PUBLISHING FAILED!                         ║" -ForegroundColor Red
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    
    Write-ColoredOutput "`n📋 Troubleshooting:" -Type "Warning"
    if (-not $nupkgSuccess) {
        Write-ColoredOutput "  • NuGet package push failed - check API key and version" -Type "Error"
    }
    if (-not $snupkgSuccess -and $snupkg) {
        Write-ColoredOutput "  • Symbol package push failed - may retry automatically" -Type "Error"
    }
    
    exit 1
}
