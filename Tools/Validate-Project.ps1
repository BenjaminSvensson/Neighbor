[CmdletBinding()]
param(
    [switch]$SkipUnity
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$issueCount = 0

function Write-Issue {
    param([string]$Message)

    $script:issueCount++
    Write-Host "ERROR: $Message" -ForegroundColor Red
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
        Write-Host "Skipping dotnet compilation because Unity has not generated Assembly-CSharp.csproj." -ForegroundColor Yellow
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
        Write-Host "Skipping dotnet compilation because Unity-generated project files are stale." -ForegroundColor Yellow
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

function Test-UnityAssets {
    if ($SkipUnity) {
        Write-Host "Skipping Unity asset validation by request." -ForegroundColor Yellow
        return
    }

    if (Test-Path -LiteralPath "Temp\UnityLockfile") {
        Write-Host "Skipping Unity asset validation because this project is open in the editor." -ForegroundColor Yellow
        return
    }

    $versionLine = Get-Content -LiteralPath "ProjectSettings\ProjectVersion.txt" |
        Where-Object { $_ -like "m_EditorVersion:*" } |
        Select-Object -First 1
    $version = ($versionLine -split ":", 2)[1].Trim()
    $unityPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$version\Editor\Unity.exe"
    if (-not (Test-Path -LiteralPath $unityPath)) {
        Write-Issue "Unity $version was not found at $unityPath"
        return
    }

    Write-Host "Validating prefabs and scenes with Unity $version..."
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
        Write-Host "Skipping Unity asset validation because this project is open in the editor." -ForegroundColor Yellow
        return
    }

    if ($unityProcess.ExitCode -ne 0) {
        Write-Issue "Unity asset validation failed. See $logPath"
    }
}

Push-Location $projectRoot
try {
    Test-MetaFiles
    Test-DuplicateGuids
    Test-GitLfs
    Test-CSharpCompilation
    Test-UnityAssets

    if ($issueCount -gt 0) {
        Write-Host "Project validation found $issueCount issue(s)." -ForegroundColor Red
        exit 1
    }

    Write-Host "Project validation passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
