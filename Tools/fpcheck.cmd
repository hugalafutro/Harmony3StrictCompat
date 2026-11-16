@echo off
rem Pre-launch fingerprint check. Silent when nothing changed; opens a window with the changed mods otherwise.
cd /d "%~dp0"
uv run --no-project --with dnfile python -I fpcheck.py > "%TEMP%\fpcheck.txt" 2>&1
if errorlevel 1 start "RimWorld mod fingerprints changed" cmd /k type "%TEMP%\fpcheck.txt"
