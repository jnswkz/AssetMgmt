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

# Only QR codes are public. Handover PDFs live in a separate persistent root.
RUN mkdir -p /app/wwwroot/qr /app/private/handovers \
    && chown -R $APP_UID:$APP_UID /app/wwwroot /app/private

# Run as the built-in non-root user shipped with the ASP.NET image.
USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Production \
    Storage__HandoverRoot=/app/private/handovers

ENTRYPOINT ["dotnet", "AssetMgmt.dll"]
