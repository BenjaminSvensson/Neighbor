# Neighbor

Unity prototype project targeting Unity `6000.4.6f1`.

## Setup

1. Install the matching editor version through Unity Hub.
2. Clone the repository with Git LFS installed.
3. Open the project and allow Unity to restore packages.
4. Open `Assets/Main/Scenes/Main/TrueTreeHouse/TrueTreeHouse.unity`.

`TrueTreeHouse` is the primary playable and build-start scene. The older AI testing map remains available under `Assets/Main/Scenes/Testing/AITestingMap`, but is intentionally disabled in Build Settings.

Unity-generated solution and project files are intentionally ignored. Regenerate them from the editor when needed.

## Validation

Run the repository health check from PowerShell:

```powershell
.\Tools\Validate-Project.ps1
```

The script checks Unity metadata pairs, duplicate GUIDs, Git LFS objects, and C# compilation. When no Unity editor has the project open, it also runs the EditMode smoke-test suite, the PlayMode core-loop smoke suite, prefab/scene validation, and project-state parity validation. EditMode results are written to `Logs/EditModeTestResults.xml`; PlayMode results are written to `TestResults/PlayModeTestResults.xml`.

To skip the longer PlayMode pass during local iteration:

```powershell
.\Tools\Validate-Project.ps1 -SkipPlayMode
```

The same asset scan is available in Unity from `Tools > Neighbor > Validate Project`.

To run only the automated smoke tests from PowerShell:

```powershell
$unity = "$env:ProgramFiles\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe"
& $unity -batchmode -projectPath $PWD -runTests -testPlatform EditMode -testResults Logs/EditModeTestResults.xml -logFile Logs/EditModeTests.log
```

To run only the PlayMode core-loop smoke tests:

```powershell
$unity = "$env:ProgramFiles\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe"
& $unity -batchmode -projectPath $PWD -runTests -testPlatform PlayMode -testResults TestResults/PlayModeTestResults.xml -logFile Logs/PlayModeTests.log
```

The tests are also available in Unity from `Window > General > Test Runner` under the EditMode and PlayMode tabs.
