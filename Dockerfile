# syntax=docker/dockerfile:1

# Imagem única do Coordin (DT-01): a API ASP.NET Core serve o build do Angular na mesma origem.
# Vale para qualquer hospedagem de containers (DT-09). A string de conexão e os segredos NÃO
# entram na imagem: chegam por variáveis de ambiente (ConnectionStrings__Default etc.).

# ---- 1) Front: build de produção do Angular ----------------------------------------------
FROM node:24 AS web-build
WORKDIR /web

# Primeiro só os arquivos de dependências, para o `npm ci` ficar em cache enquanto elas não mudam.
COPY src/coordin-web/package.json src/coordin-web/package-lock.json ./
RUN npm ci

COPY src/coordin-web/ ./
RUN npm run build

# ---- 2) API: publicação em Release ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src

# Primeiro os arquivos de projeto e de versões, para o `restore` ficar em cache.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Coordin.Engine/Coordin.Engine.csproj src/Coordin.Engine/
COPY src/Coordin.Api/Coordin.Api.csproj src/Coordin.Api/
RUN dotnet restore src/Coordin.Api/Coordin.Api.csproj

COPY src/Coordin.Engine/ src/Coordin.Engine/
COPY src/Coordin.Api/ src/Coordin.Api/
RUN dotnet publish src/Coordin.Api/Coordin.Api.csproj -c Release -o /app/publish --no-restore

# ---- 3) Imagem final: só o runtime --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=api-build /app/publish ./
# O build do Angular vira o wwwroot que a API serve.
COPY --from=web-build /web/dist/coordin-web/browser ./wwwroot

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Usuário sem privilégios, já presente na imagem oficial.
USER $APP_UID

ENTRYPOINT ["dotnet", "Coordin.Api.dll"]
