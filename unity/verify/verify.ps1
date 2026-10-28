<#
.SYNOPSIS
    Checks that the netstandard2.1 (Unity) build of DemoFile parses demos exactly like the net9.0 build.

.DESCRIPTION
    Dumps every CS2 demo in -DemoDir tick by tick (players, positions as raw float bits, events,
    a hash of all entities, plus a backwards seek) with both builds and compares the dumps byte for byte.
    With -UnityExe it also runs the dump inside Unity (Mono) in batch mode against a throwaway project.

.EXAMPLE
    ./verify.ps1
    ./verify.ps1 -UnityExe 'C:\Program Files\Unity\Hub\Editor\6000.0.67f1\Editor\Unity.exe'
#>
param(
    [string]$UnityExe,
    [string]$DemoDir = (Join-Path $PSScriptRoot '..\..\demos'),
    # Keep this short: the Unity project's Library breaks on long Windows paths
    [string]$WorkDir = (Join-Path $PSScriptRoot '..\_verify')
)

$ErrorActionPreference = 'Stop'
$DemoDir = (Resolve-Path $DemoDir).Path
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$WorkDir = (Resolve-Path $WorkDir).Path

function Invoke-Checked([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code ${LASTEXITCODE}: $Command" }
}

function Compare-Dumps([string]$Expected, [string]$Actual, [string]$Label) {
    $differences = @()
    foreach ($file in Get-ChildItem $Expected -Filter *.txt) {
        $other = Join-Path $Actual $file.Name
        if (-not (Test-Path $other) -or (Get-FileHash $file.FullName).Hash -ne (Get-FileHash $other).Hash) {
            $differences += $file.Name
        }
    }

    if ($differences) {
        Write-Host "FAIL: $Label differs in $($differences -join ', ')" -ForegroundColor Red
        return $false
    }

    Write-Host "OK: $Label is identical" -ForegroundColor Green
    return $true
}

$env:DOTNET_ROLL_FORWARD = 'Major'
$ok = $true

foreach ($flavour in 'net9', 'netstandard') {
    $project = Get-ChildItem (Join-Path $PSScriptRoot $flavour) -Filter *.csproj
    Invoke-Checked { dotnet build $project.FullName -c Release -nologo -v q }

    $output = Join-Path $WorkDir "out-$flavour"
    Remove-Item -Recurse -Force $output -ErrorAction SilentlyContinue
    Write-Host "== $flavour"
    $dll = Join-Path $project.DirectoryName "bin\Release\net10.0\$($project.BaseName).dll"
    Invoke-Checked { dotnet $dll $DemoDir $output }
}

$ok = (Compare-Dumps "$WorkDir\out-net9" "$WorkDir\out-netstandard" 'netstandard2.1 on .NET') -and $ok

if ($UnityExe) {
    $unityProject = Join-Path $WorkDir 'UnityProject'
    $plugins = Join-Path $unityProject 'Assets\Plugins\DemoFile'
    $editor = Join-Path $unityProject 'Assets\Editor'
    New-Item -ItemType Directory -Force $plugins, $editor, (Join-Path $unityProject 'Packages') | Out-Null
    Set-Content (Join-Path $unityProject 'Packages\manifest.json') '{ "dependencies": {} }'

    # The plugins exactly as the Unity package ships them: DemoFile itself plus the
    # netstandard builds of its NuGet dependencies (System.Memory etc. come with Unity)
    $build = Join-Path $PSScriptRoot '..\..\src\DemoFile.Game.Cs\bin\Release\netstandard2.1'
    Copy-Item "$build\DemoFile.dll", "$build\DemoFile.Game.Cs.dll" $plugins
    $nuget = Join-Path $env:USERPROFILE '.nuget\packages'
    foreach ($dependency in @(
            'google.protobuf\3.22.1\lib\netstandard2.0\Google.Protobuf.dll',
            'snappier\1.3.1\lib\netstandard2.0\Snappier.dll',
            'system.collections.immutable\8.0.0\lib\netstandard2.0\System.Collections.Immutable.dll',
            'system.runtime.compilerservices.unsafe\6.1.2\lib\netstandard2.0\System.Runtime.CompilerServices.Unsafe.dll',
            'system.threading.channels\8.0.0\lib\netstandard2.1\System.Threading.Channels.dll')) {
        Copy-Item (Join-Path $nuget $dependency) $plugins
    }
    Copy-Item (Join-Path $PSScriptRoot 'Dump.cs') (Join-Path $editor 'DemoDump.cs')
    Copy-Item (Join-Path $PSScriptRoot 'UnityRunner\Runner.cs') $editor

    $output = Join-Path $WorkDir 'out-unity'
    Remove-Item -Recurse -Force $output -ErrorAction SilentlyContinue
    $log = Join-Path $WorkDir 'unity.log'
    Write-Host "== Unity (batch mode, this takes a few minutes on the first run)"
    $process = Start-Process -FilePath $UnityExe -Wait -PassThru -ArgumentList @(
        '-batchmode', '-nographics',
        '-projectPath', "`"$unityProject`"",
        '-executeMethod', 'Runner.Run',
        '-demos', "`"$DemoDir`"",
        '-out', "`"$output`"",
        '-logFile', "`"$log`"")
    Select-String -Path $log -Pattern 'S2D_DUMP' -Context 0, 8 | ForEach-Object { $_.Line; $_.Context.PostContext }
    if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode), see $log" }

    $ok = (Compare-Dumps "$WorkDir\out-net9" $output 'netstandard2.1 in Unity (Mono)') -and $ok
}

if (-not $ok) { exit 1 }
