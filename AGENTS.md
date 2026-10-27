# Project instructions

- Target Unity **6000.6.7f1**, revision `b22767f04f04`; Windows is the initial player target.
- Gameplay is **Japanese Battle Spirits Eternal**, Vietnamese UI. Read `Documentation/RULES_AND_SCOPE.md` before changing rules. Saga and Standard rules must not be silently substituted.
- Before every new batch of changes, preserve the current version in a unique `backup/YYYY-MM-DD-description` branch. Preserve dirty work first. If a remote is configured, push and verify that backup before editing. Never overwrite backups or force-push ordinary updates.
- Keep `Runtime/Core` independent of Unity; maintain C# 9 compatibility. Add meaningful rules tests for behavior changes, including core conservation and Soul Core uniqueness.
- Keep rendering, UI, audio and animation in `Runtime/Presentation`; do not put Unity objects into authoritative match state.
- Catalog fields describe official cards. Do not mark an ability card as implemented until its behavior has code and tests. Preserve IDs, Japanese names, source URLs, art credits and checksums.
- Generated art models are illustrative, and must not be described as exact official monsters. Keep Blender source and regeneration scripts.
- Pin compatible package versions. Editor configuration, Unity Play and build validation must be reported separately from pure C# tests or syntax checks.
- Do not commit `Library`, `Temp`, `Logs`, test build output or user settings.
