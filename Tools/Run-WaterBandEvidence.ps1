param(
    [Parameter(Mandatory=$true)][string]$Python,
    [Parameter(Mandatory=$true)][string]$UnityEditor
)
$ErrorActionPreference='Stop'
$taskWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskProject=Join-Path $taskWorkspace 'Integration'
$taskEvidence=Join-Path $taskProject '.artifacts/WaterBand-20261010'
if(!(Test-Path -LiteralPath $UnityEditor)){throw 'Unity editor executable required'}
$taskRunning=Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($taskProject) }
if($taskRunning){throw 'Close the Integration editor/test runner before diagnostic capture'}
Push-Location $taskWorkspace
$taskOriginalFiles=@{}
foreach($taskRelative in @('Packages/manifest.json','Packages/packages-lock.json','Assets/TerraLoom/Integration/FrozenCrossingEvidence.cs')){
    $taskFile=Join-Path $taskProject $taskRelative
    $taskOriginalFiles[$taskFile]=[System.IO.File]::ReadAllBytes($taskFile)
}
try {
    try {
        & $Python (Join-Path $PSScriptRoot 'prepare_water_band_evidence.py')
        if($LASTEXITCODE -ne 0){throw 'Preparation failed; existing consumer configuration needs review'}
        $taskArgs=@('-batchmode','-nographics','-projectPath',$taskProject,'-executeMethod','TerraLoom.Integration.Editor.RuntimePlayerValidation.BuildSeededCrossingBatch','-quit','-logFile',"$taskEvidence/Build.log")
        $taskProcess=Start-Process -FilePath $UnityEditor -WindowStyle Hidden -PassThru -Wait -ArgumentList ($taskArgs | ForEach-Object {'"'+$_+'"'})
        if($taskProcess.ExitCode -ne 0){throw "Diagnostic build exit $($taskProcess.ExitCode)"}
        foreach($taskSeed in @(2043,2044)){
            $taskArgs=@('-batchmode','-force-d3d11','-terraloomSmoke','-terraloomRecipeSeed',"$taskSeed",'-terraloomScreenshot',"$taskEvidence/seed$taskSeed.png",'-logFile',"$taskEvidence/Seed$taskSeed.log")
            $taskProcess=Start-Process -FilePath "$taskProject/.artifacts/PlayerSeededCrossing/TerraLoom.exe" -WindowStyle Hidden -PassThru -Wait -ArgumentList ($taskArgs | ForEach-Object {'"'+$_+'"'})
            if($taskProcess.ExitCode -ne 0){throw "Diagnostic Player seed$taskSeed exit $($taskProcess.ExitCode)"}
            Get-Content "$taskEvidence/seed$taskSeed.txt"
        }
    } finally {
        foreach($taskFile in $taskOriginalFiles.Keys){[System.IO.File]::WriteAllBytes($taskFile,$taskOriginalFiles[$taskFile])}
    }
} finally { Pop-Location }
