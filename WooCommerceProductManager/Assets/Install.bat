@echo off
setlocal EnableExtensions
chcp 65001 >nul
title نصب نارون یدک

set "SOURCE=%~dp0"
set "DEST=%LOCALAPPDATA%\NarvanYadak"

if not exist "%SOURCE%NarvanYadak.exe" (
  echo پرونده NarvanYadak.exe پیدا نشد.
  pause
  exit /b 1
)

echo در حال نصب نارون یدک...
mkdir "%DEST%" 2>nul
robocopy "%SOURCE%." "%DEST%" /E /NFL /NDL /NJH /NJS /NC /NS /NP >nul
if errorlevel 8 (
  echo نصب ناموفق بود.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$dest = Join-Path $env:LOCALAPPDATA 'NarvanYadak';" ^
  "$exe = Join-Path $dest 'NarvanYadak.exe';" ^
  "$name = 'نارون یدک';" ^
  "$ws = New-Object -ComObject WScript.Shell;" ^
  "$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) ($name + '.lnk');" ^
  "$start = Join-Path ([Environment]::GetFolderPath('StartMenu')) ('Programs\' + $name + '.lnk');" ^
  "foreach ($path in @($desktop, $start)) { $s = $ws.CreateShortcut($path); $s.TargetPath = $exe; $s.WorkingDirectory = $dest; $s.IconLocation = $exe + ',0'; $s.Description = $name; $s.Save() }"

echo.
echo نصب تمام شد. میانبر «نارون یدک» روی میزکار و منوی شروع ساخته شد.
echo برای اجرا روی میانبر دوبار کلیک کنید.
echo.
pause
