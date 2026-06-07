# Etapa 1: Build
FROM node:20-alpine AS build
WORKDIR /app

COPY src/frontend/package*.json ./
RUN npm ci

COPY src/frontend/ ./
RUN npm run build

# Etapa 2: Serve con Nginx
FROM nginx:alpine AS final

# Patch OS packages to the latest available in the Alpine 3.23 repo.
# The base nginx:alpine layer is frozen in image history; if a CVE is
# disclosed after the layer was published, the binary is still vulnerable
# in our image even after a re-pull. `apk upgrade --no-cache` adds a thin
# overlay that upgrades installed packages to whatever the current Alpine
# repo serves at build time, mirroring the `apt-get upgrade -y` pattern
# used in python.Dockerfile. CVE-2026-6732 (libxml2 2.13.9-r0) was the
# first finding that motivated this step.
RUN apk upgrade --no-cache

# Configuración custom de Nginx para Vue SPA + headers de seguridad.
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# Copiar build
COPY --from=build /app/dist /usr/share/nginx/html

# Nginx corre como root para enlazar puerto 80 por defecto en Alpine, pero
# podemos pasarlo a no-root exponiendo un puerto > 1024.
RUN chown -R nginx:nginx /usr/share/nginx/html /var/cache/nginx /var/log/nginx /etc/nginx/conf.d
RUN touch /var/run/nginx.pid && chown -R nginx:nginx /var/run/nginx.pid
USER nginx

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    # Use 127.0.0.1 (IPv4) explicitly: nginx listens on `listen 80;` (IPv4
    # only), and `localhost` resolves to `::1` (IPv6) first on Alpine,
    # which would fail the healthcheck even when nginx is up.
    CMD wget --quiet --tries=1 --spider http://127.0.0.1/ || exit 1

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
