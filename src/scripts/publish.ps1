[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot 'DemoOCR.Wpf\DemoOCR.Wpf.csproj'
& dotnet publish $projectPath -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed. dotnet exit code: $LASTEXITCODE"
}

$publishPath = & dotnet msbuild $projectPath -p:Configuration=Release -p:RuntimeIdentifier=win-x64 -getProperty:PublishDir
if ($LASTEXITCODE -ne 0) {
    throw "Failed to read the publish directory. dotnet exit code: $LASTEXITCODE"
}
if (-not [IO.Path]::IsPathRooted($publishPath)) {
    $publishPath = Join-Path (Split-Path -Parent $projectPath) $publishPath
}
$executablePath = Join-Path $publishPath 'DemoOCR.exe'
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Published executable is missing: $executablePath"
}

Write-Output "Published: $executablePath"
