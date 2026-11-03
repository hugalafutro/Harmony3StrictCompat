# Harmony 3 Strict Binding Compat (local)

Local stop-gap fixes for a large RimWorld 1.7 mod list (Harmony 3, UnityStubs). Every fix is temporary and
fingerprints the mod method it touches: when that mod updates, the log shows a red error so the fix can be
re-checked or removed. See `About/About.xml` for the list of fixes and `Source/` for the code.

Companion XML tweaks: [LocalTweaks](https://github.com/hugalafutro/LocalTweaks).

Build: `Tools/build.ps1` (compiles `Source/*.cs` against the game's DLLs into `1.7/Assemblies`).
