# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar csproj y restaurar como capas separadas para aprovechar caché
COPY ["src/backend-dotnet/VortexFlow.Api/VortexFlow.Api.csproj", "VortexFlow.Api/"]
COPY ["src/backend-dotnet/VortexFlow.Application/VortexFlow.Application.csproj", "VortexFlow.Application/"]
COPY ["src/backend-dotnet/VortexFlow.Domain/VortexFlow.Domain.csproj", "VortexFlow.Domain/"]
COPY ["src/backend-dotnet/VortexFlow.Infrastructure/VortexFlow.Infrastructure.csproj", "VortexFlow.Infrastructure/"]
RUN dotnet restore "VortexFlow.Api/VortexFlow.Api.csproj" && \
    dotnet restore "VortexFlow.Application/VortexFlow.Application.csproj" && \
    dotnet restore "VortexFlow.Domain/VortexFlow.Domain.csproj" && \
    dotnet restore "VortexFlow.Infrastructure/VortexFlow.Infrastructure.csproj"

# Copiar resto del código y hacer build
COPY src/backend-dotnet/ .
WORKDIR "/src/VortexFlow.Api"
RUN dotnet build "VortexFlow.Api.csproj" -c Release -o /app/build
RUN dotnet publish "VortexFlow.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 8080

# Patch OS packages to the latest available in the Alpine repo. The base
# mcr.microsoft.com/dotnet/aspnet:8.0-alpine layer is frozen in image
# history; `apk upgrade --no-cache` adds a thin overlay that upgrades
# installed packages at build time, mirroring the `apt-get upgrade -y`
# pattern used in python.Dockerfile so transient Trivy findings do not
# block the CI gate.
RUN apk upgrade --no-cache

# Usuario no root para seguridad
RUN adduser -D -u 1000 appuser
USER appuser

# Variables de entorno
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Custom entrypoint that resolves the `PasswordFile=` / `passwordFile=`
# placeholders in the connection strings by reading the secret files.
# Baked into the image (not bind-mounted) so the API container has it
# even when running on Docker Desktop for Windows where bind mounts of
# shell scripts sometimes fail with "no such file or directory" at
# exec time. `--chmod=755` sets the executable bit in the COPY itself
# because the previous `USER appuser` line above would make a
# subsequent `RUN chmod +x` fail with "permission denied".
COPY --chmod=755 docker/dotnet-entrypoint.sh /usr/local/bin/dotnet-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/dotnet-entrypoint.sh"]
