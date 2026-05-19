# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar csproj y restaurar como capas separadas para aprovechar caché
COPY ["src/backend-dotnet/VortexFlow.Api/VortexFlow.Api.csproj", "VortexFlow.Api/"]
COPY ["src/backend-dotnet/VortexFlow.Application/VortexFlow.Application.csproj", "VortexFlow.Application/"]
COPY ["src/backend-dotnet/VortexFlow.Domain/VortexFlow.Domain.csproj", "VortexFlow.Domain/"]
COPY ["src/backend-dotnet/VortexFlow.Infrastructure/VortexFlow.Infrastructure.csproj", "VortexFlow.Infrastructure/"]
COPY ["src/backend-dotnet/VortexFlow.sln", "./"]
RUN dotnet restore "VortexFlow.sln"

# Copiar resto del código y hacer build
COPY src/backend-dotnet/ .
WORKDIR "/src/VortexFlow.Api"
RUN dotnet build "VortexFlow.Api.csproj" -c Release -o /app/build
RUN dotnet publish "VortexFlow.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 8080

# Usuario no root para seguridad
RUN adduser -D -u 1000 appuser
USER appuser

# Variables de entorno
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "VortexFlow.Api.dll"]
