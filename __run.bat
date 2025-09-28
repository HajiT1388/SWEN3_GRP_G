docker rm -f $(docker ps -aq)
docker compose up -d db adminer
docker compose up -d --build rest
docker compose build --no-cache ui
docker compose up -d ui
rem start http://localhost:9091/?pgsql=db&username=dmsg3&db=dmsg3_db&ns=public
rem start http://localhost:8081/swagger/index.html
start http://localhost/
pause .