# Web-Oberfläche (Team und Kundenbereich) für den lokalen Testbetrieb mit Docker.
FROM node:22-alpine AS build
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM nginx:1.29-alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
# Feste Zuordnung für /hubs/: liegt bereit, nginx liest sie nur, wenn compose.scale.yaml sie in conf.d/hubs-balancing legt.
COPY docker/nginx-hubs-hash.conf /etc/nginx/hubs-hash.conf
COPY --from=build /web/dist /usr/share/nginx/html
