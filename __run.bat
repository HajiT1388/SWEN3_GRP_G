@echo off

docker compose up --build -d

start http://localhost:9091/?pgsql=db^&username=dmsg3^&db=dmsg3_db^&ns=public
start http://localhost:8081/swagger/index.html
start http://localhost:8080/
start http://localhost:9093/
start http://localhost/

pause .
