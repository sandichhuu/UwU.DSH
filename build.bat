@echo off
setlocal
echo ====================================================
echo Building DSH Proxies...
echo ====================================================

:: Cleanup legacy output directories if present
if exist "dist" rmdir /s /q "dist"
if exist "dist-standalone" rmdir /s /q "dist-standalone"
if exist "Release\Standalone" rmdir /s /q "Release\Standalone"

:: Ensure output directory exists
if not exist "Release\windows_x86_64" mkdir "Release\windows_x86_64"

:: Staging folders
set "STAGING_STANDALONE=Release\_staging_standalone"
set "STAGING_LIGHTWEIGHT=Release\_staging_lightweight"

if exist "%STAGING_STANDALONE%" rmdir /s /q "%STAGING_STANDALONE%"
if exist "%STAGING_LIGHTWEIGHT%" rmdir /s /q "%STAGING_LIGHTWEIGHT%"

echo [1/2] Building Standalone Single-File Executable...
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%STAGING_STANDALONE%"
if errorlevel 1 (
    echo [ERROR] Standalone build failed!
    pause
    exit /b 1
)
move /y "%STAGING_STANDALONE%\DshAntigravityLauncher.exe" "Release\windows_x86_64\DshAntigravityLauncher-Standalone.exe" >nul
rmdir /s /q "%STAGING_STANDALONE%"

echo [2/2] Building Lightweight Executable...
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%STAGING_LIGHTWEIGHT%"
if errorlevel 1 (
    echo [ERROR] Lightweight build failed!
    pause
    exit /b 1
)
move /y "%STAGING_LIGHTWEIGHT%\DshAntigravityLauncher.exe" "Release\windows_x86_64\DshAntigravityLauncher-Lightweight.exe" >nul
rmdir /s /q "%STAGING_LIGHTWEIGHT%"

echo ====================================================
echo Build Completed!
echo Check output folder: Release\windows_x86_64
echo - Release\windows_x86_64\DshAntigravityLauncher-Standalone.exe   (~134MB)
echo - Release\windows_x86_64\DshAntigravityLauncher-Lightweight.exe  (~1.3MB)
echo ====================================================
pause
