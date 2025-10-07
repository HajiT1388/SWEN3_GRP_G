@echo off
chcp 65001
setlocal

docker compose up --build -d

start "" "http://localhost:9091/?pgsql=db^&username=dmsg3^&db=dmsg3_db^&ns=public"
start "" "http://localhost:8081/swagger/index.html"
start "" "http://localhost:9093/"
start "" "http://localhost/"

start "REST Logs" cmd /k "docker compose logs -f rest"
start "Worker Logs" cmd /k "docker compose logs -f worker"
start "RabbitMQ Logs" cmd /k "docker compose logs -f rabbitmq"

pause