@echo off
setlocal
chcp 65001 >nul

pushd "%~dp0"

docker compose down -v

for /f %%i in ('docker ps -aq') do docker rm -f %%i >nul 2>&1

for /f %%i in ('docker volume ls -q') do docker volume rm %%i >nul 2>&1

popd
pause