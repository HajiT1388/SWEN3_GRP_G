@echo off
setlocal
chcp 65001 >nul

pushd "%~dp0"

docker compose up --build -d

start "" "http://localhost:9091/?pgsql=db^&username=dmsg3^&db=dmsg3_db^&ns=public"
start "" "http://localhost:8081/swagger/index.html"
start "" "http://localhost:9093/"
start "" "http://localhost/"

start "REST Logs"     cmd /k "pushd ""%~dp0"" && docker compose --ansi never logs -f rest"
start "Worker Logs"   cmd /k "pushd ""%~dp0"" && docker compose --ansi never logs -f worker"
start "RabbitMQ Logs" cmd /k "pushd ""%~dp0"" && docker compose --ansi never logs -f rabbitmq"

popd
pause