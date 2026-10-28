<#
.SYNOPSIS
    Builds the netstandard2.1 (Unity) flavour of DemoFile and copies it into a Unity package.

.DESCRIPTION
    By default the output goes to the Source2Demo package next to this repository
    (..\unity-packages\Packages\Source2Demo\Plugins). The NuGet dependencies
    (Google.Protobuf, Snappier, System.Collections.Immutable, System.Runtime.CompilerServices.Unsafe,
    System.Threading.Channels) are installed in the Unity project by NuGetForUnity.
    Requires the .NET 10 SDK (the polyfills use C# 14 extension members).

.EXAMPLE
    ./build-unity-plugins.ps1
    ./build-unity-plugins.ps1 -Plugins 'D:\Unity\unity-packages\Packages\Source2Demo\Plugins'
#>
param(
    [string]$Plugins = (Join-Path $PSScriptRoot '..\..\unity-packages\Packages\Source2Demo\Plugins')
)

$ErrorActionPreference = 'Stop'

$repo = Join-Path $PSScriptRoot '..'
$project = Join-Path $repo 'src\DemoFile.Game.Cs\DemoFile.Game.Cs.csproj'
$output = Join-Path $repo 'src\DemoFile.Game.Cs\bin\Release\netstandard2.1'

dotnet build $project -c Release -f netstandard2.1 -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

New-Item -ItemType Directory -Force $Plugins | Out-Null
foreach ($assembly in 'DemoFile', 'DemoFile.Game.Cs') {
    Copy-Item "$output\$assembly.dll", "$output\$assembly.xml" $Plugins
}

Write-Host "Copied DemoFile plugins to $((Resolve-Path $Plugins).Path)"
