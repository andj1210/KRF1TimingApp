echo off

REM This is a poor distribution creation by just copying the files into an zip file
REM "zip" needs to be in the path (cygwin enviroment)

echo "Create Filename"
Set /A nr=0

Set fnam=%date:~6,4%-%date:~3,2%-%date:~0,2%_f1gamesessiondisplay.zip 
if exist %fnam% goto :increment
goto :zip

:increment
Set /A nr= nr + 1
echo %fnam%
Set fnam=%date:~6,4%-%date:~3,2%-%date:~0,2%-%nr%_f1gamesessiondisplay.zip 
if exist %fnam% goto :increment

:zip
echo "create temporary files"
set tempfolder=zipme_temp\

if exist %tempfolder%/ rm %tempfolder% -R
mkdir %tempfolder%
mkdir %tempfolder%\f1gamesessiondisplay

copy ..\_build\bin\F1GameSessionDisplay.exe %tempfolder%\f1gamesessiondisplay\F1GameSessionDisplay.exe
copy ..\_build\bin\F12020UdpParser.dll %tempfolder%\f1gamesessiondisplay\F12020UdpParser.dll 
copy ..\changelog.txt %tempfolder%\f1gamesessiondisplay\.
copy ..\LICENSE.md %tempfolder%\f1gamesessiondisplay\.
copy ..\LICENSE.Application.md %tempfolder%\f1gamesessiondisplay\.
copy ..\LICENSE.KeyboardHook.md %tempfolder%\f1gamesessiondisplay\.
copy ..\README.md %tempfolder%\f1gamesessiondisplay\.

echo "Remove temp files"
cd %tempfolder%
zip -r ../%fnam% *
cd ..
rm -R ./%tempfolder%

echo DONE
pause