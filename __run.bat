@echo off
setlocal
chcp 65001 >nul

pushd "%~dp0"

set "USE_NOCACHE="
set /p USE_NOCACHE="Ohne Cache builden? (j/N) "
if /I "%USE_NOCACHE%"=="j" set "USE_NOCACHE=1"
if /I "%USE_NOCACHE%"=="y" set "USE_NOCACHE=1"
if defined USE_NOCACHE (
  docker compose build --no-cache
)

docker compose up --build -d

start "" "http://localhost:9091/?pgsql=db&username=dmsg3&db=dmsg3_db&ns=public"
start "" "http://localhost:8081/swagger/index.html"
start "" "http://localhost:9093/"
start "" "http://localhost:9090/"
start "" "http://localhost/"

start "REST Logs" cmd /k "pushd ""%~dp0"" && docker compose --ansi never logs -f rest"
start "OCR Logs" cmd /k "pushd ""%~dp0"" && docker compose --ansi never logs -f dmsg3-services"
start "GenAI Logs" cmd /k "pushd ""%~dp0"" && docker compose --ansi never logs -f genai-worker"
start "RabbitMQ Logs" cmd /k "pushd ""%~dp0"" && docker compose --ansi never logs -f rabbitmq"
start "MinIO Logs" cmd /k "pushd ""%~dp0"" && docker compose --ansi never logs -f minio"

popd
pause