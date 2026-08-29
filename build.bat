@echo off
echo ====================================================
echo Building DSH Proxies...
echo ====================================================

echo Building Lightweight Executable (dist)...
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --no-self-contained -o dist

echo Building Standalone Single-File Executable (Release/Standalone)...
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o Release/Standalone

echo Building Standalone Executable (dist-standalone)...
dotnet publish DshAntigravityLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist-standalone

echo Build Completed!
echo Check output folders:
echo - dist\DshAntigravityLauncher.exe (Lightweight ~1.3MB)
echo - Release\Standalone\DshAntigravityLauncher.exe (Standalone Single-File ~140MB)
echo - dist-standalone\DshAntigravityLauncher.exe (Standalone ~140MB)
pause
