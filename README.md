# Neighbor

Unity prototype project targeting Unity `6000.4.6f1`.

## Setup

1. Install the matching editor version through Unity Hub.
2. Clone the repository with Git LFS installed.
3. Open the project and allow Unity to restore packages.
4. Open `Assets/Main/Scenes/Testing.unity`.

Unity-generated solution and project files are intentionally ignored. Regenerate them from the editor when needed.

## Validation

Run the repository health check from PowerShell:

```powershell
.\Tools\Validate-Project.ps1
```

The script checks Unity metadata pairs, duplicate GUIDs, Git LFS objects, and C# compilation. When no Unity editor has the project open, it also starts the matching editor in batch mode and scans every prefab and scene for missing scripts.

The same asset scan is available in Unity from `Tools > Neighbor > Validate Project`.
