@echo off
setlocal enableextensions enabledelayedexpansion

set roslyn_dir="roslyn"
set out_dir="generated"

rmdir %out_dir% /s /q && mkdir %out_dir%

bin\igorc.exe -d -v -t elixir ^
  -set experimental_server=true ^
    -x "gen_elixir\gen_ecto.cs" ^
    -x "gen_elixir\gen_ecto_views.cs" ^
    -x "gen_elixir\gen_example.cs" ^
    -p "igor\common" ^
    -p "igor\data" ^
    -p "igor\schema" ^
    -p "igor\api" ^
    -o %out_dir% ^
    -roslyn %roslyn_dir% ^
    *.igor

if errorlevel 1 pause && exit

rem bin\igorc.exe -d -v -t elixir ^
rem     -p "igor\chronos" ^
rem     -p "igor\clickhouse" ^
rem     -o %out_dir% ^
rem     *.igor

rem if errorlevel 1 pause && exit

endlocal
