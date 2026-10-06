[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$projectDirectories = Get-ChildItem -LiteralPath $sourceRoot -Directory |
    Where-Object { Get-ChildItem -LiteralPath $_.FullName -File -Filter '*.csproj' }

foreach ($projectDirectory in $projectDirectories) {
    foreach ($directory in @('bin', 'obj')) {
        $targetPath = [IO.Path]::GetFullPath((Join-Path $projectDirectory.FullName $directory))
        if (-not $targetPath.StartsWith($sourceRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Parent $targetPath) -ne $projectDirectory.FullName -or
            (Split-Path -Leaf $targetPath) -notin @('bin', 'obj')) {
            throw "Build cleanup path is outside the project: $targetPath"
        }
        if ((Test-Path -LiteralPath $targetPath) -and $PSCmdlet.ShouldProcess($targetPath, 'Remove build directory')) {
            Remove-Item -LiteralPath $targetPath -Recurse -Force
            Write-Output "Removed: $targetPath"
        }
    }
}
