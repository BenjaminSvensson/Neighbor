# Codex Instructions

- Work directly on `main` for Codex-authored changes unless the user explicitly asks for a branch.
- When Codex finishes making changes in this repository, commit on `main` and push `main` to GitHub before reporting completion.
- Do not create `codex/` branches for routine work.
- Stage and commit only the files changed for the current task. Do not include unrelated user edits already present in the working tree.
- Unity scene files are an exception to the rule above. Before every commit, check for modified or untracked `Assets/**/*.unity` and matching `.unity.meta` files. Include all scene changes in the commit and push, even when they were manually authored or are unrelated to the current task, so project state is not left behind between PCs. Never discard, reset, stash, or overwrite scene changes without explicit user approval.
- If pushing is blocked by authentication, failing checks, or unclear change ownership, explain the blocker and the exact next step needed.
