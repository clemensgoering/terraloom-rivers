param([Parameter(Mandatory=$true)][string]$UnityData)
$ErrorActionPreference='Stop'
$moduleRoot=Split-Path $PSScriptRoot -Parent
$coreRoot=Join-Path (Split-Path $moduleRoot -Parent) 'Core'
$output=Join-Path $moduleRoot '.artifacts/WaterQueryChecks'
New-Item -ItemType Directory -Force $output | Out-Null
$compiler=Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$runtime=Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$mono=Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$references=Join-Path $UnityData 'MonoBleedingEdge/lib/mono/unityjit-win32'
foreach($required in @($compiler,$runtime,$mono,$references)) {
    if(!(Test-Path -LiteralPath $required)){throw "Required Unity compiler/runtime path missing: $required"}
}
$executable=Join-Path $output 'WaterSurfaceQueryChecks.exe'
$arguments=@('-nologo','-target:exe','-langversion:9','-warnaserror+','-nostdlib+',('-out:"'+$executable+'"'))
$arguments+=Get-ChildItem -LiteralPath $references -Filter '*.dll' | ForEach-Object {'-r:"'+$_.FullName+'"'}
$sources=Get-ChildItem (Join-Path $coreRoot 'Packages/com.terraloom.core/Runtime'),(Join-Path $moduleRoot 'Packages/com.terraloom.rivers/Runtime') -Recurse -Filter '*.cs'
$arguments+=$sources | ForEach-Object {'"'+$_.FullName+'"'}
$arguments+='"'+(Join-Path $PSScriptRoot 'WaterSurfaceQueryChecks.cs')+'"'
$response=Join-Path $output 'checks.rsp'
[IO.File]::WriteAllLines($response,$arguments)
& $runtime $compiler "@$response"
if($LASTEXITCODE -ne 0){throw 'Water surface query compilation failed.'}
& $mono $executable | Tee-Object (Join-Path $output 'checks.log')
if($LASTEXITCODE -ne 0){throw 'Water surface query checks failed.'}
