@echo off
setlocal enableextensions enabledelayedexpansion

set roslyn_dir="roslyn"
rem set out_dir="..\src\app\protocol"
set out_dir=".\generated_ts"

bin\igorc.exe -v -t ts ^
    -x "gen_ts\*.cs" ^
    -p "igor\common" ^
    -p "igor\data" ^
    -p "igor\schema" ^
    -p "igor\api" ^
    -o %out_dir% ^
    -roslyn %roslyn_dir% ^
    *.igor

if errorlevel 1 pause && exit

copy /B /V /Y "ts\igor.ts" %out_dir%
if errorlevel 1 pause && exit

endlocal
