# Etapa 1: Build
FROM node:20-alpine AS build
WORKDIR /app

COPY src/frontend/package*.json ./
RUN npm ci

COPY src/frontend/ ./
RUN npm run build

# Etapa 2: Serve con Nginx
FROM nginx:alpine AS final

# Configuración custom de Nginx para Vue SPA (enrutamiento)
RUN echo 'server { \
    listen       80; \
    server_name  localhost; \
    location / { \
        root   /usr/share/nginx/html; \
        index  index.html index.htm; \
        try_files $$uri $$uri/ /index.html; \
    } \
}' > /etc/nginx/conf.d/default.conf

# Copiar build
COPY --from=build /app/dist /usr/share/nginx/html

# Nginx corre como root para enlazar puerto 80 por defecto en Alpine, 
# pero podemos pasarlo a no-root exponiendo un puerto > 1024
RUN chown -R nginx:nginx /usr/share/nginx/html /var/cache/nginx /var/log/nginx /etc/nginx/conf.d
RUN touch /var/run/nginx.pid && chown -R nginx:nginx /var/run/nginx.pid
USER nginx

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
