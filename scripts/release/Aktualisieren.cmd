@echo off
rem GSM-Kurswahl: spielt das neueste Programmpaket aus dem Ordner "Updates" ein.
rem Vorher alle Lehrerprogramme schliessen. Protokoll: Updates\update-protokoll.txt
rem Der Block wird von cmd vor dem Start vollstaendig eingelesen; das Update darf diese Datei daher ersetzen.
(
  rem Modulpfad einer evtl. aufrufenden PowerShell 7 nicht an Windows PowerShell 5.1 vererben.
  set "PSModulePath="
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0_Update\Update-Kurswahl.ps1" -Stammordner "%~dp0." %*
  if errorlevel 1 (
    echo.
    echo Aktualisierung NICHT durchgefuehrt. Der bisherige Stand ist unveraendert bzw. wiederhergestellt.
    pause
    exit /b 1
  )
  echo.
  echo Aktualisierung abgeschlossen.
  pause
  exit /b 0
)
