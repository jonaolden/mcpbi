#!/usr/bin/env pwsh
param(
    [string]$Port = "",
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: ./test-all.ps1 [-Port <port>] [-Help]"
    Write-Host ""
    Write-Host "Parameters:"
    Write-Host "  -Port <port>    Optional PowerBI port number"
    Write-Host "  -Help           Show this help message"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  ./test-all.ps1                    # Auto-discover port from running PowerBI"
    Write-Host "  ./test-all.ps1 -Port 12345        # Use specific port"
    Write-Host ""
    Write-Host "The script will:"
    Write-Host "  1. Build the project"
    Write-Host "  2. Run all unit tests"
    Write-Host "  3. Start the MCP server for integration testing"
    Write-Host "  4. Run integration tests (if available)"
    exit 0
}

Write-Host "=== PowerBI Tabular MCP - Complete Test Suite ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build the project
Write-Host "Step 1: Building project..." -ForegroundColor Yellow
try {
    dotnet build -c Debug --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    Write-Host "✓ Build successful" -ForegroundColor Green
} catch {
    Write-Host "✗ Build failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 2: Run unit tests
Write-Host "Step 2: Running unit tests..." -ForegroundColor Yellow
try {
    dotnet test --no-build --logger "console;verbosity=normal" --configuration Debug
    if ($LASTEXITCODE -ne 0) {
        throw "Unit tests failed with exit code $LASTEXITCODE"
    }
    Write-Host "✓ All unit tests passed" -ForegroundColor Green
} catch {
    Write-Host "✗ Unit tests failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 3: PowerBI Instance Discovery and Integration Test
Write-Host "Step 3: Testing PowerBI integration..." -ForegroundColor Yellow

if ([string]::IsNullOrEmpty($Port)) {
    Write-Host "No port specified, attempting to discover PowerBI instances..." -ForegroundColor Cyan
    
    # Try to run the discovery tool if available
    $discoveryExe = "releases\pbi-local-mcp.DiscoverCli.exe"
    if (Test-Path $discoveryExe) {
        Write-Host "Found discovery tool, running auto-discovery..." -ForegroundColor Cyan
        try {
            $discoveryOutput = & $discoveryExe 2>&1
            Write-Host "Discovery output:" -ForegroundColor Gray
            Write-Host $discoveryOutput -ForegroundColor Gray
        } catch {
            Write-Host "Warning: Auto-discovery failed: $_" -ForegroundColor Yellow
        }
    } else {
        Write-Host "Discovery tool not found at $discoveryExe" -ForegroundColor Yellow
        Write-Host "You can build it with: dotnet build pbi-local-mcp.DiscoverCli" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "Manual PowerBI Setup Required:" -ForegroundColor Yellow
    Write-Host "1. Open PowerBI Desktop with a model (e.g., resources/testing/Sample.pbix)" -ForegroundColor Gray
    Write-Host "2. Go to External Tools -> Tabular Editor to see the port number" -ForegroundColor Gray
    Write-Host "3. Re-run this script with: ./test-all.ps1 -Port <your-port>" -ForegroundColor Gray
    Write-Host ""
    Write-Host "✓ Unit tests completed successfully" -ForegroundColor Green
    Write-Host "⚠ Integration tests skipped (no PowerBI port specified)" -ForegroundColor Yellow
    exit 0
}

# Step 4: Test MCP server startup with specified port
Write-Host "Testing MCP server startup with port $Port..." -ForegroundColor Cyan

# Create a test .env file for this test run
$testEnvContent = @"
PBI_PORT=$Port
"@

$testEnvFile = ".env.test"
$testEnvContent | Out-File -FilePath $testEnvFile -Encoding UTF8

try {
    # Test server startup (timeout after 10 seconds)
    Write-Host "Starting MCP server test (10 second timeout)..." -ForegroundColor Cyan
    
    $serverProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project pbi-local-mcp/pbi-local-mcp.csproj -- --port $Port" -PassThru -NoNewWindow -RedirectStandardOutput "test-server-output.log" -RedirectStandardError "test-server-error.log"
    
    # Wait up to 10 seconds for the server to start
    $timeout = 10
    $elapsed = 0
    $serverStarted = $false
    
    while ($elapsed -lt $timeout -and !$serverStarted) {
        Start-Sleep -Seconds 1
        $elapsed++
        
        # Check if process is still running
        if ($serverProcess.HasExited) {
            $exitCode = $serverProcess.ExitCode
            if ($exitCode -eq 0) {
                Write-Host "✓ Server started and exited normally" -ForegroundColor Green
                $serverStarted = $true
            } else {
                throw "Server process exited with code $exitCode"
            }
        } else {
            Write-Host "Server running... ($elapsed/$timeout seconds)" -ForegroundColor Gray
        }
    }
    
    # If still running after timeout, consider it successful and stop it
    if (!$serverProcess.HasExited) {
        Write-Host "✓ Server started successfully (stopping test instance)" -ForegroundColor Green
        $serverProcess.Kill()
        $serverStarted = $true
    }
    
    if ($serverStarted) {
        Write-Host "✓ MCP server integration test passed" -ForegroundColor Green
    } else {
        throw "Server failed to start within $timeout seconds"
    }
    
} catch {
    Write-Host "✗ MCP server test failed: $_" -ForegroundColor Red
    
    # Show error logs if available
    if (Test-Path "test-server-error.log") {
        $errorLog = Get-Content "test-server-error.log" -Raw
        if (![string]::IsNullOrWhiteSpace($errorLog)) {
            Write-Host "Server error log:" -ForegroundColor Red
            Write-Host $errorLog -ForegroundColor Red
        }
    }
    
    exit 1
} finally {
    # Cleanup
    if ($serverProcess -and !$serverProcess.HasExited) {
        $serverProcess.Kill()
    }
    
    # Clean up test files
    if (Test-Path $testEnvFile) { Remove-Item $testEnvFile -Force }
    if (Test-Path "test-server-output.log") { Remove-Item "test-server-output.log" -Force }
    if (Test-Path "test-server-error.log") { Remove-Item "test-server-error.log" -Force }
}

Write-Host ""
Write-Host "=== Test Suite Complete ===" -ForegroundColor Cyan
Write-Host "✓ Build: Successful" -ForegroundColor Green
Write-Host "✓ Unit Tests: All Passed" -ForegroundColor Green
Write-Host "✓ Integration: Server Startup Verified" -ForegroundColor Green
Write-Host ""
Write-Host "Ready for production use!" -ForegroundColor Green