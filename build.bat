@echo off
chcp 936 >nul 2>nul
title NovelOverlay 一键编译
cd /d "%~dp0"

echo.
echo   ==================================================
echo      NovelOverlay  无边框透明小说阅读器  一键编译
echo   ==================================================
echo.

set "CSC="
for %%D in ("%WINDIR%\Microsoft.NET\Framework64\v4.0.30319" "%WINDIR%\Microsoft.NET\Framework\v4.0.30319" "%WINDIR%\Microsoft.NET\Framework\v3.5" "%WINDIR%\Microsoft.NET\Framework\v2.0.50727") do (
    if not defined CSC if exist "%%~D\csc.exe" set "CSC=%%~D\csc.exe"
)

if not defined CSC goto NOCOMPILER

echo   编译器 : %CSC%
echo   正在编译 ...
echo.

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:NovelOverlay.exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll src\Program.cs src\Settings.cs src\Formats.cs src\Overlay.cs src\ReaderView.cs src\MainForm.cs

if errorlevel 1 goto FAILED

echo.
echo   [成功] 已生成 NovelOverlay.exe
echo   正在启动 ...
start "" "NovelOverlay.exe"
exit /b 0

:NOCOMPILER
echo   [错误] 没有找到 .NET 编译器，请先安装 Microsoft .NET Framework 4.0 或更高版本。
echo   Win7 建议装 4.8：https://dotnet.microsoft.com/download/dotnet-framework
echo.
pause
exit /b 1

:FAILED
echo.
echo   [失败] 编译出错了，把上面的错误信息发给我。
echo.
pause
exit /b 1
