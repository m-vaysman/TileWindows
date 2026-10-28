@echo off
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /out:"Tile Windows.exe" /r:System.Windows.Forms.dll TileWindows.cs
if errorlevel 1 exit /b 1
echo Built: Tile Windows.exe
