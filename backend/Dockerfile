# ── build ──────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first for layer caching
COPY src/WhatsOrder.Domain/WhatsOrder.Domain.csproj src/WhatsOrder.Domain/
COPY src/WhatsOrder.Application/WhatsOrder.Application.csproj src/WhatsOrder.Application/
COPY src/WhatsOrder.Infrastructure/WhatsOrder.Infrastructure.csproj src/WhatsOrder.Infrastructure/
COPY src/WhatsOrder.Api/WhatsOrder.Api.csproj src/WhatsOrder.Api/
RUN dotnet restore src/WhatsOrder.Api/WhatsOrder.Api.csproj

COPY src/ src/
RUN dotnet publish src/WhatsOrder.Api/WhatsOrder.Api.csproj -c Release -o /app /p:UseAppHost=false

# ── runtime ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WhatsOrder.Api.dll"]
