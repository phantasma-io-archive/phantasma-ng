@echo off

REM Set debug_mode to true if you want to run your own instance of node0 inside Visual Studio
set DEBUG_MODE=true
set TOTAL_NODES=2
set PUBLISH_PATH=C:\code\phantasma-ng\Phantasma.Node\publish
set STORAGE_PATH=C:\code\Spook\StorageAnalyser\bin\Debug\netcoreapp3.1\Output\Storage

If Not Exist "%~dp0\tendermint.exe" goto :MissingTendermint

REM Init tendermint 
echo Resetting Tendermint...
set NODE_INDEX=0
:TendermintLoop
set NODE_ROOT=%~dp0node%NODE_INDEX%\
SET TMHOME=%NODE_ROOT%
copy "%~dp0\tendermint.exe" %NODE_ROOT% /y > NUL
%NODE_ROOT%\tendermint.exe unsafe-reset-all
set /A NODE_INDEX+=1
if not %NODE_INDEX% == %TOTAL_NODES% goto :TendermintLoop

REM Loop to copy required files into each node folder
set NODE_INDEX=0
:CopyLoop
set NODE_ROOT=%~dp0node%NODE_INDEX%\

REM if running in debug mode, skip install/run of node0
if %NODE_INDEX% == 0 ( if "%DEBUG_MODE%" == "true" goto :SkipDebugNode)

REM Copy published dotnet files
echo Copying publish files for node %NODE_INDEX%
Robocopy %PUBLISH_PATH% %NODE_ROOT%\publish\ /E  > NUL
IF %ERRORLEVEL% GEQ 7 goto :FailedCopy

REM Copy storage files for previous chain data 
echo Copying storage files for node %NODE_INDEX%
REM echo %STORAGE_PATH%
REM echo %NODE_ROOT%\publish\Storage
Robocopy %STORAGE_PATH% %NODE_ROOT%\publish\Storage /it /E  > NUL
IF %ERRORLEVEL% GEQ 7 goto :FailedCopy

echo Copying config files for node %NODE_INDEX%
SET SRC_JSON=%NODE_ROOT%config_node%NODE_INDEX%.json
SET DST_JSON=%NODE_ROOT%publish\config.json 
REM echo src = %SRC_JSON%
REM echo dest = %DST_JSON%
copy %SRC_JSON% %DST_JSON% /y > NUL
IF ERRORLEVEL 1 goto :FailedCopy

echo Launching node %NODE_INDEX%
SET URLS="http://localhost:510%NODE_INDEX%"
start "Phantasma node %NODE_INDEX%" cmd /K "cd %NODE_ROOT%\publish & dotnet phantasma-node.dll --urls %URLS%"
cd %~dp0

:SkipDebugNode

echo Launching tendermint %NODE_INDEX%
start "Tendermint %NODE_INDEX%" cmd /K  "SET TMHOME=%NODE_ROOT%& cd %NODE_ROOT%& tendermint.exe node"

set /A NODE_INDEX+=1
if not %NODE_INDEX% == %TOTAL_NODES% goto :CopyLoop

echo Everything should be running now!
exit

REM ERROR SECTION
:FailedCopy
echo ERROR:Could not copy files. 
echo Make sure you have published the dotnet solution.
echo Also check if publish and storage path are pointing to the proper directories. 
goto :Finished

:MissingTendermint
echo ERROR: Tendermint.exe is missing, download it here: 
echo https://github.com/tendermint/tendermint/releases
echo Then copy tendermint.exe to %~dp0 
goto :Finished

:Finished
@pause
goto :EOF
