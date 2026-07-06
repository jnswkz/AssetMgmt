# =============================================================
# Multi-stage build for the AssetMgmt ASP.NET Core 8 Web API.
# =============================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (layer-cached while the csproj is unchanged).
COPY AssetMgmt.csproj ./
RUN dotnet restore AssetMgmt.csproj

# Build + publish.
COPY . .
RUN dotnet publish AssetMgmt.csproj -c Release -o /app/publish /p:UseAppHost=false

# -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Kestrel listens on 8080 (non-privileged) inside the container.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

# The app writes generated QR codes and handover PDFs under wwwroot at runtime.
RUN mkdir -p /app/wwwroot/qr /app/wwwroot/handovers \
    && chown -R $APP_UID:$APP_UID /app/wwwroot

# Run as the built-in non-root user shipped with the ASP.NET image.
USER $APP_UID

# Default app environment variables. Override secrets via compose/.env.
ENV ASPNETCORE_ENVIRONMENT=Development \
    DB_SERVER=localhost \
    DB_PORT=1433 \
    DB_NAME=AssetMgmt \
    DB_USER=sa \
    DB_PASSWORD=Str0ng!Passw0rd \
    DB_TRUST_CERT=True \
    JWT_SECRET=change-me-to-a-strong-secret

ENTRYPOINT ["dotnet", "AssetMgmt.dll"]
