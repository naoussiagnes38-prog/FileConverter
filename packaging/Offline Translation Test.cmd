@echo off
setlocal
chcp 65001 >nul
set "PYTHONIOENCODING=utf-8"
"%~dp0TranslationRuntime\python.exe" -B "%~dp0TranslationRuntime\run_libretranslate.py" --self-test
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" echo Translation test failed. Check the error above.
if "%RESULT%"=="0" echo Both offline translation directions passed.
pause
exit /b %RESULT%
