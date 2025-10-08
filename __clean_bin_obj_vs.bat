@echo off
for /d /r %%d in (bin obj .vs) do (
    if /i "%%~nxd"=="bin"  rmdir /s /q "%%d"
    if /i "%%~nxd"=="obj"  rmdir /s /q "%%d"
    if /i "%%~nxd"==".vs"  rmdir /s /q "%%d"
)