@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Demo80.ps1" -GkGeo
if errorlevel 1 pause
