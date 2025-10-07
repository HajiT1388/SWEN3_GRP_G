docker compose down -v
for /f %%i in ('docker ps -aq') do docker rm -f %%i
for /f %%i in ('docker volume ls -q') do docker volume rm %%i
pause
