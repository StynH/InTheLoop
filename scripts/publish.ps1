<#
.SYNOPSIS
    Publishes the desktop app and its MCP server to dist\InTheLoop.

.DESCRIPTION
    The result is a self-contained folder:
        dist\InTheLoop\InTheLoop.exe            the desktop app
        dist\InTheLoop\mcp\intheloop-mcp.exe    the MCP server to register with your AI client
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'dist\InTheLoop'

dotnet publish (Join-Path $root 'src\InTheLoop.App') `
    -c Release `
    -f net10.0-windows10.0.19041.0 `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    --self-contained `
    -o $out

if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

Write-Host ''
Write-Host "App:        $out\InTheLoop.exe"
Write-Host "MCP server: $out\mcp\intheloop-mcp.exe"
