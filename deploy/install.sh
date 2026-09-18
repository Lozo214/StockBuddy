#!/bin/bash
set -euxo pipefail

APP_ROOT=/opt/stockbuddy

sudo mkdir -p "$APP_ROOT/app" "$APP_ROOT/data" "$APP_ROOT/caddy-data" "$APP_ROOT/caddy-config"
sudo cp -R cloud-app/. "$APP_ROOT/app/"
sudo cp Caddyfile "$APP_ROOT/Caddyfile"
sudo chown -R ec2-user:ec2-user "$APP_ROOT"
sudo chmod 750 "$APP_ROOT" "$APP_ROOT/data"

sudo docker rm -f stockbuddy caddy 2>/dev/null || true

sudo docker run -d \
  --name stockbuddy \
  --restart unless-stopped \
  --network host \
  --user 1000:1000 \
  --read-only \
  --tmpfs /tmp \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://127.0.0.1:8080 \
  -e 'ConnectionStrings__StockBuddy=Data Source=/data/stockbuddy.db' \
  -e 'ConnectionStrings__Accounts=Data Source=/data/accounts.db' \
  -e DataProtection__KeyPath=/data/keys \
  -v "$APP_ROOT/app:/app:ro" \
  -v "$APP_ROOT/data:/data" \
  -w /app \
  mcr.microsoft.com/dotnet/aspnet:10.0 \
  dotnet StockBuddy.Web.dll

sudo docker run -d \
  --name caddy \
  --restart unless-stopped \
  --network host \
  -v "$APP_ROOT/Caddyfile:/etc/caddy/Caddyfile:ro" \
  -v "$APP_ROOT/caddy-data:/data" \
  -v "$APP_ROOT/caddy-config:/config" \
  caddy:2-alpine

for attempt in $(seq 1 30); do
  if curl --fail --silent http://127.0.0.1:8080/health; then
    exit 0
  fi
  sleep 2
done

sudo docker logs stockbuddy
exit 1
