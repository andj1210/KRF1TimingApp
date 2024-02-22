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
mkdir %tempfolder%\krf1timing

copy ..\_build\bin\Krf1Timing.exe %tempfolder%\krf1timing\Krf1Timing.exe
copy ..\_build\bin\adjsw.F1Udp.23.dll %tempfolder%\krf1timing\adjsw.F1Udp.23.dll
copy ..\_build\bin\Newtonsoft.Json.dll %tempfolder%\krf1timing\Newtonsoft.Json.dll
copy ..\_build\bin\Razorvine.Pickle.dll %tempfolder%\krf1timing\Razorvine.Pickle.dll
copy ..\changelog.txt %tempfolder%\krf1timing\.
copy ..\LICENSE.md %tempfolder%\krf1timing\.
copy ..\LICENSE.*.* %tempfolder%\krf1timing\.
copy ..\namemappings.json.example %tempfolder%\krf1timing\.
copy ..\README.md %tempfolder%\krf1timing\.

echo "Remove temp files"
cd %tempfolder%
zip -r ../%fnam% *
cd ..
rm -R ./%tempfolder%

echo DONE
pause