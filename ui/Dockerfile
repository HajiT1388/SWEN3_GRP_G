FROM nginx:1.28-alpine

# config
COPY ./nginx.conf /etc/nginx/conf.d/default.conf

# static (ui per se)
COPY ./public/ /usr/share/nginx/html