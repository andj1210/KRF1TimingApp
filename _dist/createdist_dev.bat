@echo off
rem createdist_dev.bat - like createdist.bat, but builds ReleaseDev (dev tools enabled)
rem and names the zip krf1_timing_v<version>_dev.zip.
rem Further arguments are passed through, e.g.: createdist_dev.bat -PlatformToolset v143

call "%~dp0createdist.bat" -Configuration ReleaseDev %*
exit /b %ERRORLEVEL%
