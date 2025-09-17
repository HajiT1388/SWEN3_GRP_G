docker compose up -d db adminer
docker compose up -d --build rest
start http://localhost:9091/?pgsql=db&username=dmsg3&db=dmsg3_db&ns=public
start http://localhost:8081/swagger/index.html