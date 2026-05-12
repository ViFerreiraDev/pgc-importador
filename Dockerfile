# syntax=docker/dockerfile:1.7

# =====================================================================
#  Build stage — .NET SDK 9 + Node 22 (necessário para o SPA build)
# =====================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
WORKDIR /src

# Node 22 (para o target PublishRunWebpack do PcaImporter.Api.csproj
# rodar `npm install` + `npm run build` durante o `dotnet publish`).
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
 && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
 && apt-get install -y --no-install-recommends nodejs \
 && rm -rf /var/lib/apt/lists/*

# --- camada cacheável: restore dotnet e npm (apenas manifestos) ---
COPY PcaImporter.sln ./
COPY src/PcaImporter.Domain/PcaImporter.Domain.csproj         src/PcaImporter.Domain/
COPY src/PcaImporter.Application/PcaImporter.Application.csproj src/PcaImporter.Application/
COPY src/PcaImporter.Infrastructure/PcaImporter.Infrastructure.csproj src/PcaImporter.Infrastructure/
COPY src/PcaImporter.Api/PcaImporter.Api.csproj               src/PcaImporter.Api/
COPY src/PcaImporter.Web/package.json src/PcaImporter.Web/package-lock.json src/PcaImporter.Web/
RUN dotnet restore src/PcaImporter.Api/PcaImporter.Api.csproj
RUN cd src/PcaImporter.Web && npm ci

# --- fonte completa + publish ---
COPY . .
RUN dotnet publish src/PcaImporter.Api/PcaImporter.Api.csproj \
      -c Release \
      -o /app/publish \
      --no-restore \
      /p:UseAppHost=false

# =====================================================================
#  Runtime stage — só o ASP.NET runtime, imagem pequena.
# =====================================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime
WORKDIR /app

# Diretório persistente para o SQLite (mapeado por volume).
RUN mkdir -p /data && chown -R 1000:1000 /data
ENV ConnectionStrings__Sqlite="Data Source=/data/pca-importer.db" \
    ASPNETCORE_URLS="http://+:8080" \
    ASPNETCORE_ENVIRONMENT="Production" \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=1

COPY --from=build --chown=1000:1000 /app/publish .

USER 1000
EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "PcaImporter.Api.dll"]
