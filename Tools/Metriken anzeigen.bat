@echo off
rem Doppelklick: holt die Steam-Metriken und oeffnet sie im Browser.
rem Optional: "Metriken anzeigen.bat" -Tage 7   bzw.   -NeuerKey
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0metriken_anzeigen.ps1" %*
if errorlevel 1 (
  echo.
  echo Fehler - Fenster mit einer Taste schliessen.
  pause >nul
)
