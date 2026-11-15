@echo off
setlocal
cd /d "%~dp0"

set "APP=%~dp0autoclicker.py"

where pyw >nul 2>nul
if %errorlevel%==0 (
    start "" pyw -3 "%APP%"
    exit /b 0
)

where pythonw >nul 2>nul
if %errorlevel%==0 (
    start "" pythonw "%APP%"
    exit /b 0
)

where py >nul 2>nul
if %errorlevel%==0 (
    start "" py -3 "%APP%"
    exit /b 0
)

where python >nul 2>nul
if %errorlevel%==0 (
    start "" python "%APP%"
    exit /b 0
)

echo Could not find Python 3. Install Python 3.10+ with tkinter, then try again.
echo https://www.python.org/downloads/
pause
exit /b 1
