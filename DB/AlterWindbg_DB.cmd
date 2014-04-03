rem @echo off
if "%1"=="" goto noArgument
if "%2"=="" goto noArgument

SET SQL_SERVER=%1
SET MASTER_DB=master
SET TARGET_DB=%2

rem ====================================================
rem Create DB and Tables
rem ====================================================

osql -S %SQL_SERVER% -d %TARGET_DB% -E -i WinDbgScripts.sql 

goto finished

:noArgument
echo Please supply Server Name
echo.


:finished
echo Done