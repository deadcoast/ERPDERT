@echo off
"%~dp0ERPDERT.exe" demo
set "MER_DEMO_EXIT=%ERRORLEVEL%"
echo.
pause
exit /b %MER_DEMO_EXIT%
