param(
    [string]$Unity = '',
    [ValidateSet('EditMode','PlayMode')][string]$Platform = 'EditMode',
    [string]$Filter = 'TerraLoom'
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'UnityTestRunner.ps1')
Invoke-TerraLoomUnityTests -Unity $Unity -ProjectRoot $taskRoot -Platform $Platform -Filter $Filter
