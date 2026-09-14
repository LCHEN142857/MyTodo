[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [ValidateSet('win-x64')]
    [string] $Runtime = 'win-x64',
    [string] $OutputPath = ''
)

$ErrorActionPreference = 'Stop'

function IsChildOf([string] $Path, [string] $Parent) {
    $resolvedPath = [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $resolvedParent = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    return $resolvedPath.StartsWith($resolvedParent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
}

function Invoke-Dotnet([string[]] $Arguments) {
    & $script:Dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code ${LASTEXITCODE}: $($Arguments -join ' ')"
    }
}

$repositoryRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$defaultOutputPath = [IO.Path]::GetFullPath((Join-Path $artifactsRoot 'publish'))
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $publishPath = $defaultOutputPath
} elseif ([IO.Path]::IsPathRooted($OutputPath)) {
    $publishPath = [IO.Path]::GetFullPath($OutputPath)
} else {
    $publishPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
}
$normalizedPublishPath = $publishPath.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
if ([string]::IsNullOrWhiteSpace($normalizedPublishPath) -or $normalizedPublishPath -eq [IO.Path]::GetPathRoot($normalizedPublishPath)) {
    throw "Refusing to clean filesystem root: $publishPath"
}
$isUnderRepository = IsChildOf $publishPath $repositoryRoot
$isUnderArtifacts = IsChildOf $publishPath $artifactsRoot
if ($isUnderRepository -and -not $isUnderArtifacts) {
    throw "Refusing to clean repository path outside artifacts: $publishPath"
}
if ([string]::IsNullOrWhiteSpace($OutputPath) -and -not $isUnderArtifacts) {
    throw "Refusing to clean default publish path outside artifacts: $publishPath"
}
if (Test-Path -LiteralPath $publishPath) {
    $publishItem = Get-Item -LiteralPath $publishPath -Force
    if (($publishItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to clean reparse-point publish path: $publishPath"
    }
    if (-not $publishItem.PSIsContainer) {
        throw "Publish path is not a directory: $publishPath"
    }
}

$dotnetCandidates = @()
if ($env:MYTODO_DOTNET) { $dotnetCandidates += $env:MYTODO_DOTNET }
$dotnetCandidates += (Join-Path $repositoryRoot '.tools\dotnet8\dotnet.exe')
$systemDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($systemDotnet) { $dotnetCandidates += $systemDotnet.Source }
$script:Dotnet = $dotnetCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $script:Dotnet) {
    throw 'Unable to find dotnet. Install the .NET 8 SDK or place it at .tools\dotnet8\dotnet.exe.'
}

Write-Host "Using dotnet: $script:Dotnet"
Write-Host 'Restoring solution...'
Invoke-Dotnet @('restore', (Join-Path $repositoryRoot 'MyToDo.sln'))

Write-Host 'Running test suite...'
Invoke-Dotnet @('test', (Join-Path $repositoryRoot 'MyToDo.sln'), '-c', $Configuration)

if (Test-Path -LiteralPath $publishPath) {
    Write-Host "Cleaning $publishPath"
    Remove-Item -LiteralPath $publishPath -Recurse -Force
}
New-Item -ItemType Directory -Path $publishPath -Force | Out-Null

Write-Host "Publishing $Runtime single-file executable..."
$runtimeIdentifierProperty = "-p:RuntimeIdentifier=$Runtime"
Invoke-Dotnet @('publish', (Join-Path $repositoryRoot 'src\MyToDo.App\MyToDo.App.csproj'), '-c', $Configuration, '-r', $Runtime, $runtimeIdentifierProperty, '-p:SelfContained=true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugSymbols=false', '-p:DebugType=None', '-p:AssemblyName=MyToDo', '-o', $publishPath)

$executablePath = Join-Path $publishPath 'MyToDo.exe'
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "Publish completed without expected executable: $executablePath"
}
$executable = Get-Item -LiteralPath $executablePath
if ($executable.Length -le 0) { throw "Published executable is empty: $executablePath" }
Write-Host ("Published executable: {0} ({1:N0} bytes)" -f $executable.FullName, $executable.Length)
