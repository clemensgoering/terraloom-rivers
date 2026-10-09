function Resolve-TerraLoomUnity {
    param([string]$Unity, [string]$ProjectRoot)
    if ($Unity) {
        if (!(Test-Path -LiteralPath $Unity -PathType Leaf)) { throw "Unity executable not found: $Unity" }
        return (Resolve-Path -LiteralPath $Unity).Path
    }
    $versionLine = Get-Content -LiteralPath (Join-Path $ProjectRoot 'ProjectSettings/ProjectVersion.txt') |
        Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
    if (!$versionLine) { throw 'Project Unity version is missing.' }
    $version = ($versionLine -split ': ', 2)[1].Trim()
    $candidates = @()
    # Hub's configurable installation directory (older Hub releases).
    if ($env:APPDATA) {
        $hubRoot = Join-Path $env:APPDATA 'UnityHub'
        $locationFile = Join-Path $hubRoot 'secondaryInstallPath.json'
        if (Test-Path -LiteralPath $locationFile) {
            $location = Get-Content -LiteralPath $locationFile -Raw | ConvertFrom-Json
            if ($location -isnot [string]) { $location = $location.secondaryInstallPath }
            if ($location) { $candidates += Join-Path $location "$version/Editor/Unity.exe" }
        }
    }
    # Hub installations register an editor icon even when InstallLocation is empty.
    foreach ($hive in @('HKCU:', 'HKLM:')) {
        $key = "$hive/Software/Microsoft/Windows/CurrentVersion/Uninstall/Unity $version"
        $installation = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($installation.DisplayIcon) { $candidates += $installation.DisplayIcon.Trim('"') }
    }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe" }
    # A relocated Unity tree beside the projects, independent of drive letter.
    $driveRoot = [IO.Path]::GetPathRoot($ProjectRoot)
    $candidates += Join-Path $driveRoot "Unity/Editor/$version/Editor/Unity.exe"
    $candidates += Join-Path $driveRoot "Unity/Hub/Editor/$version/Editor/Unity.exe"
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    throw "Unity $version not found in Hub/install locations. Supply -Unity <path-to-Unity.exe>."
}

function Invoke-TerraLoomUnityTests {
    param([string]$Unity, [string]$ProjectRoot, [string]$Platform, [string]$Filter, [int]$ExpectedTests = 0)
    $Unity = Resolve-TerraLoomUnity -Unity $Unity -ProjectRoot $ProjectRoot
    $output = Join-Path $ProjectRoot '.artifacts'
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    # Serialize this project's runners; other Unity projects may run concurrently.
    $lockPath = Join-Path $output 'Unity-validation.lock'
    try { $runLock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { throw "Another validation runner owns $ProjectRoot. Wait for it to finish." }
    try {
        $normalizedRoot = $ProjectRoot.Replace('\', '/').TrimEnd('/')
        foreach ($process in (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'")) {
            $command = $process.CommandLine
            if ($command -and $command.Replace('\', '/') -match ('(?i)(?:^|\s)"?-projectPath"?\s+(?:"' + [regex]::Escape($normalizedRoot) + '/?"|' + [regex]::Escape($normalizedRoot) + '/?(?=\s|$))')) {
                throw "Unity already uses $ProjectRoot (PID $($process.ProcessId)). Close it before validation."
            }
        }
        $xml = Join-Path $output ($Platform + '-v1.xml')
        $log = Join-Path $output ($Platform + '-v1.log')
        foreach ($oldOutput in @($xml, $log)) {
            if (Test-Path -LiteralPath $oldOutput) { Remove-Item -LiteralPath $oldOutput -Force }
        }
        $arguments = @('-batchmode', '-nographics', '-projectPath', $ProjectRoot, '-runTests', '-testPlatform', $Platform,
            '-testFilter', $Filter, '-testResults', $xml, '-logFile', $log)
        foreach ($argument in $arguments) {
            if ($argument.Contains('"')) { throw 'Unity arguments must not contain double quotes.' }
        }
        $quotedArguments = $arguments | ForEach-Object { '"' + $_ + '"' }
        Write-Output "Starting $Platform : $ProjectRoot; Unity $Unity"
        $started = [DateTime]::UtcNow
        $process = Start-Process -FilePath $Unity -WindowStyle Hidden -PassThru -Wait -ArgumentList $quotedArguments
        if ($process.ExitCode -ne 0) { throw "Unity $Platform exit $($process.ExitCode); inspect $log" }
        if (!(Test-Path -LiteralPath $xml) -or (Get-Item -LiteralPath $xml).LastWriteTimeUtc -lt $started) {
            throw "Unity $Platform produced no fresh test XML; inspect $log"
        }
        [xml]$result = Get-Content -LiteralPath $xml -Raw
        $run = $result.'test-run'
        $cases = @($result.SelectNodes('//test-case'))
        $notPassed = @($cases | Where-Object { $_.result -ne 'Passed' })
        if (!$run -or $run.result -ne 'Passed' -or [int]$run.total -le 0 -or
            [int]$run.passed -ne [int]$run.total -or [int]$run.failed -ne 0 -or [int]$run.skipped -ne 0 -or
            $cases.Count -ne [int]$run.total -or $notPassed.Count -gt 0 -or
            ($ExpectedTests -gt 0 -and [int]$run.total -ne $ExpectedTests)) {
            throw "Unexpected Unity $Platform result: $($run.result), passed=$($run.passed), total=$($run.total), failed=$($run.failed), skipped=$($run.skipped), expected=$ExpectedTests; inspect $xml and $log"
        }
        Write-Output "$Platform : $($run.passed)/$($run.total) passed, 0 failed, 0 skipped; Unity exit 0; results $xml; log $log"
    }
    finally { $runLock.Dispose() }
}
