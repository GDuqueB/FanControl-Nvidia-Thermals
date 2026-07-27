@echo off
set PATH=
set PATH=
set "PATH=C:\Windows\System32;C:\Windows"
"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S "C:\Users\DEEP\Documents\HWMonitor\reference\pawn-4.1.7152" -B "C:\Users\DEEP\Documents\HWMonitor\tools\pawn-build" -G "Visual Studio 16 2019" -A Win32
if errorlevel 1 exit /b %errorlevel%
"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build "C:\Users\DEEP\Documents\HWMonitor\tools\pawn-build" --config Release --target pawncc
