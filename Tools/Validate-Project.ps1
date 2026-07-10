[CmdletBinding()]
param(
    [switch]$SkipUnity,
    [switch]$SkipPlayMode,
    [switch]$Strict,
    [switch]$BuildPlayer
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$issueCount = 0
$unavailableCheckCount = 0

function Write-Issue {
    param([string]$Message)

    $script:issueCount++
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Write-Unavailable {
    param([string]$Message)

    $script:unavailableCheckCount++
    if ($Strict) {
        Write-Issue $Message
        return
    }

    Write-Host "SKIPPED: $Message" -ForegroundColor Yellow
}

function Test-MetaFiles {
    Write-Host "Checking Unity metadata..."

    Get-ChildItem -LiteralPath "Assets" -Recurse -Force |
        Where-Object { $_.Name -notlike "*.meta" } |
        ForEach-Object {
            if (-not (Test-Path -LiteralPath ($_.FullName + ".meta"))) {
                Write-Issue "Missing meta file for $($_.FullName.Substring($projectRoot.Length + 1))"
            }
        }

    Get-ChildItem -LiteralPath "Assets" -Recurse -Force -Filter "*.meta" -File |
        ForEach-Object {
            $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
            if (-not (Test-Path -LiteralPath $assetPath)) {
                Write-Issue "Orphan meta file $($_.FullName.Substring($projectRoot.Length + 1))"
            }
        }
}

function Test-DuplicateGuids {
    Write-Host "Checking Unity GUIDs..."
    $guidOwners = @{}

    Get-ChildItem -LiteralPath "Assets" -Recurse -Force -Filter "*.meta" -File |
        ForEach-Object {
            $match = Select-String -LiteralPath $_.FullName -Pattern "^guid: ([0-9a-f]{32})$" | Select-Object -First 1
            if ($null -eq $match) {
                Write-Issue "Meta file has no GUID: $($_.FullName.Substring($projectRoot.Length + 1))"
                return
            }

            $guid = $match.Matches[0].Groups[1].Value
            if (-not $guidOwners.ContainsKey($guid)) {
                $guidOwners[$guid] = [System.Collections.Generic.List[string]]::new()
            }

            $guidOwners[$guid].Add($_.FullName.Substring($projectRoot.Length + 1))
        }

    foreach ($entry in $guidOwners.GetEnumerator()) {
        if ($entry.Value.Count -gt 1) {
            Write-Issue "Duplicate GUID $($entry.Key): $($entry.Value -join ', ')"
        }
    }
}

function Test-CSharpCompilation {
    if (-not (Test-Path -LiteralPath "Assembly-CSharp.csproj")) {
        Write-Unavailable "C# compilation requires Unity-generated Assembly-CSharp.csproj. Open Unity once or use strict Unity validation on a prepared workspace."
        return
    }

    $projectSources = ""
    Get-ChildItem -LiteralPath "." -Filter "Assembly-CSharp*.csproj" -File |
        ForEach-Object { $projectSources += Get-Content -LiteralPath $_.FullName -Raw }
    $missingSources = @(Get-ChildItem -LiteralPath "Assets" -Recurse -Filter "*.cs" -File |
        Where-Object {
            $relativePath = $_.FullName.Substring($projectRoot.Length + 1).Replace("/", "\")
            -not $projectSources.Contains($relativePath)
        })

    if ($missingSources.Count -gt 0) {
        Write-Unavailable "C# compilation requires refreshed Unity-generated project files; one or more Assets/**/*.cs files are missing from the current projects."
        return
    }

    Write-Host "Compiling C#..."
    & dotnet build "Assembly-CSharp.csproj"
    if ($LASTEXITCODE -ne 0) {
        Write-Issue "Runtime C# compilation failed."
    }

    if (-not (Test-Path -LiteralPath "Assembly-CSharp-Editor.csproj")) {
        return
    }

    & dotnet build "Assembly-CSharp-Editor.csproj"
    if ($LASTEXITCODE -ne 0) {
        Write-Issue "Editor C# compilation failed."
    }
}

function Test-GitLfs {
    Write-Host "Checking Git LFS objects..."
    & git lfs fsck
    if ($LASTEXITCODE -ne 0) {
        Write-Issue "Git LFS validation failed."
    }
}

function Get-UnityEditorPath {
    $versionLine = Get-Content -LiteralPath "ProjectSettings\ProjectVersion.txt" |
        Where-Object { $_ -like "m_EditorVersion:*" } |
        Select-Object -First 1
    $version = ($versionLine -split ":", 2)[1].Trim()
    $unityPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (-not (Test-Path -LiteralPath $unityPath)) {
        Write-Issue "Unity $version was not found at $unityPath"
        return $null
    }

    return $unityPath
}

function Wait-UnityBatchProcesses {
    param(
        [datetime]$StartedAt,
        [string]$UnityPath,
        [int]$TimeoutSeconds = 600
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $startedAtWindow = $StartedAt.AddSeconds(-2)
    while ((Get-Date) -lt $deadline) {
        $matchingProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue |
            Where-Object {
                $_.Path -eq $UnityPath -and $_.StartTime -ge $startedAtWindow
            })
        if ($matchingProcesses.Count -eq 0) {
            return $true
        }

        Start-Sleep -Seconds 2
    }

    Get-Process Unity -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Path -eq $UnityPath -and $_.StartTime -ge $startedAtWindow
        } |
        Stop-Process -Force
    return $false
}

function Test-UnityEditModeTests {
    if ($SkipUnity) {
        Write-Host "Skipping Unity EditMode tests by request." -ForegroundColor Yellow
        return
    }

    if (Test-Path -LiteralPath "Temp\UnityLockfile") {
        Write-Unavailable "Unity EditMode tests require the project to be closed in the interactive editor."
        return
    }

    $unityPath = Get-UnityEditorPath
    if ($null -eq $unityPath) {
        return
    }

    Write-Host "Running Unity EditMode tests..."
    $logPath = Join-Path $projectRoot "Logs\EditModeTests.log"
    $resultsPath = Join-Path $projectRoot "Logs\EditModeTestResults.xml"
    Remove-Item -LiteralPath $resultsPath -Force -ErrorAction SilentlyContinue
    $arguments = @(
        "-batchmode",
        "-projectPath", "`"$projectRoot`"",
        "-runTests",
        "-testPlatform", "EditMode",
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$logPath`""
    )
    $unityProcess = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    $logText = ""
    if (Test-Path -LiteralPath $logPath) {
        $logText = Get-Content -LiteralPath $logPath -Raw
    }

    if ($logText -match "another Unity instance is running") {
        Write-Unavailable "Unity EditMode tests could not start because another Unity instance owns the project."
        return
    }

    if ($unityProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultsPath)) {
        Write-Issue "Unity EditMode tests failed. See $logPath and $resultsPath"
        return
    }

    [xml]$testResults = Get-Content -LiteralPath $resultsPath -Raw
    if ([int]$testResults."test-run".failed -gt 0) {
        Write-Issue "Unity EditMode tests reported failures. See $logPath and $resultsPath"
    }
}

function Test-UnityPlayModeTests {
    if ($SkipUnity -or $SkipPlayMode) {
        Write-Host "Skipping Unity PlayMode tests by request." -ForegroundColor Yellow
        return
    }

    if (Test-Path -LiteralPath "Temp\UnityLockfile") {
        Write-Unavailable "Unity PlayMode tests require the project to be closed in the interactive editor."
        return
    }

    $unityPath = Get-UnityEditorPath
    if ($null -eq $unityPath) {
        return
    }

    Write-Host "Running Unity PlayMode tests..."
    $logPath = Join-Path $projectRoot "Logs\PlayModeTests.log"
    $resultsDirectory = Join-Path $projectRoot "TestResults"
    $resultsPath = Join-Path $resultsDirectory "PlayModeTestResults.xml"
    New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
    Remove-Item -LiteralPath $resultsPath -Force -ErrorAction SilentlyContinue
    $arguments = @(
        "-batchmode",
        "-projectPath", "`"$projectRoot`"",
        "-runTests",
        "-testPlatform", "PlayMode",
        "-testResults", "`"$resultsPath`"",
        "-logFile", "`"$logPath`""
    )

    $startedAt = Get-Date
    $unityProcess = Start-Process -FilePath $unityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    Wait-Process -Id $unityProcess.Id -ErrorAction SilentlyContinue
    if (-not (Wait-UnityBatchProcesses $startedAt $unityPath 600)) {
        Write-Issue "Unity PlayMode tests timed out and the batch editor was stopped. See $logPath"
        return
    }

    $logText = ""
    if (Test-Path -LiteralPath $logPath) {
        $logText = Get-Content -LiteralPath $logPath -Raw
    }

    if ($logText -match "another Unity instance is running") {
        Write-Unavailable "Unity PlayMode tests could not start because another Unity instance owns the project."
        return
    }

    $unityProcess.Refresh()
    if ($unityProcess.ExitCode -ne 0) {
        Write-Issue "Unity PlayMode test process exited with code $($unityProcess.ExitCode). See $logPath"
        return
    }

    if (-not (Test-Path -LiteralPath $resultsPath)) {
        Write-Issue "Unity PlayMode tests did not produce results. See $logPath"
        return
    }

    [xml]$testResults = Get-Content -LiteralPath $resultsPath -Raw
    if ([int]$testResults."test-run".failed -gt 0) {
        Write-Issue "Unity PlayMode tests reported failures. See $logPath and $resultsPath"
    }
}

function Test-UnityAssets {
    if ($SkipUnity) {
        Write-Host "Skipping Unity asset validation by request." -ForegroundColor Yellow
        return
    }

    if (Test-Path -LiteralPath "Temp\UnityLockfile") {
        Write-Unavailable "Unity prefab and scene validation requires the project to be closed in the interactive editor."
        return
    }

    $unityPath = Get-UnityEditorPath
    if ($null -eq $unityPath) {
        return
    }

    Write-Host "Validating prefabs and scenes with Unity..."
    $logPath = Join-Path $projectRoot "Logs\ProjectValidation.log"
    $arguments = @(
        "-batchmode",
        "-quit",
        "-projectPath", "`"$projectRoot`"",
        "-executeMethod", "ProjectHealthValidator.ValidateFromCommandLine",
        "-logFile", "`"$logPath`""
    )
    $unityProcess = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    $logText = ""
    if (Test-Path -LiteralPath $logPath) {
        $logText = Get-Content -LiteralPath $logPath -Raw
    }

    if ($logText -match "another Unity instance is running") {
        Write-Unavailable "Unity prefab and scene validation could not start because another Unity instance owns the project."
        return
    }

    if ($unityProcess.ExitCode -ne 0) {
        Write-Issue "Unity asset validation failed. See $logPath"
    }
}

function Test-ProjectStateParity {
    if ($SkipUnity) {
        Write-Host "Skipping Unity project-state parity validation by request." -ForegroundColor Yellow
        return
    }

    if (Test-Path -LiteralPath "Temp\UnityLockfile") {
        Write-Unavailable "Unity project-state parity validation requires the project to be closed in the interactive editor."
        return
    }

    $unityPath = Get-UnityEditorPath
    if ($null -eq $unityPath) {
        return
    }

    Write-Host "Validating project-state parity..."
    $logPath = Join-Path $projectRoot "Logs\ProjectStateParityValidation.log"
    $arguments = @(
        "-batchmode",
        "-quit",
        "-projectPath", "`"$projectRoot`"",
        "-executeMethod", "ProjectStateParityValidator.ValidateFromCommandLine",
        "-logFile", "`"$logPath`""
    )
    $unityProcess = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    $logText = ""
    if (Test-Path -LiteralPath $logPath) {
        $logText = Get-Content -LiteralPath $logPath -Raw
    }

    if ($logText -match "another Unity instance is running") {
        Write-Unavailable "Unity project-state parity validation could not start because another Unity instance owns the project."
        return
    }

    if ($unityProcess.ExitCode -ne 0) {
        Write-Issue "Unity project-state parity validation failed. See $logPath"
    }
}

function Test-UnityPlayerBuild {
    if (-not $BuildPlayer) {
        return
    }

    if ($SkipUnity) {
        Write-Unavailable "Player build validation was requested together with -SkipUnity."
        return
    }

    if (Test-Path -LiteralPath "Temp\UnityLockfile") {
        Write-Unavailable "Player build validation requires the project to be closed in the interactive editor."
        return
    }

    $unityPath = Get-UnityEditorPath
    if ($null -eq $unityPath) {
        return
    }

    Write-Host "Building Windows validation player..."
    $outputDirectory = Join-Path $projectRoot "Builds\Validation"
    $outputPath = Join-Path $outputDirectory "NeighborValidation.exe"
    $logPath = Join-Path $projectRoot "Logs\PlayerBuildValidation.log"
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    $startedAt = Get-Date
    $arguments = @(
        "-batchmode",
        "-quit",
        "-projectPath", "`"$projectRoot`"",
        "-executeMethod", "NeighborBuildAutomation.BuildWindows64DevelopmentFromCommandLine",
        "-neighborBuildPath", "`"$outputPath`"",
        "-logFile", "`"$logPath`""
    )
    $unityProcess = Start-Process -FilePath $unityPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($unityProcess.ExitCode -ne 0
        -or -not (Test-Path -LiteralPath $outputPath)
        -or (Get-Item -LiteralPath $outputPath).LastWriteTime -lt $startedAt.AddSeconds(-2)) {
        Write-Issue "Unity Windows player build failed or did not produce a fresh executable. See $logPath"
    }
}

Push-Location $projectRoot
try {
    Test-MetaFiles
    Test-DuplicateGuids
    Test-GitLfs
    Test-CSharpCompilation
    Test-UnityEditModeTests
    Test-UnityPlayModeTests
    Test-UnityPlayerBuild
    Test-UnityAssets
    Test-ProjectStateParity

    if ($issueCount -gt 0) {
        Write-Host "Project validation found $issueCount issue(s)." -ForegroundColor Red
        exit 1
    }

    if ($unavailableCheckCount -gt 0) {
        Write-Host "Project validation completed with $unavailableCheckCount unavailable check(s); rerun with Unity closed or use -Strict to require a fully verified result." -ForegroundColor Yellow
        exit 0
    }

    Write-Host "Project validation passed with every requested check executed." -ForegroundColor Green
}
finally {
    Pop-Location
}
