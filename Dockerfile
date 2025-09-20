# Fáze 1: Sestavení aplikace
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Zkopírování .csproj a obnovení závislostí
COPY *.csproj ./
RUN dotnet restore

# Zkopírování zbytku kódu a sestavení
COPY . .
RUN dotnet publish -c Release -o out

# Fáze 2: Spuštění aplikace
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

# TENTO ŘÁDEK ZKOPÍRUJE NAŠI LOKÁLNÍ DATABÁZI
COPY kodi.db /app/data/kodi.db

RUN mkdir -p /app/data

ENTRYPOINT ["dotnet", "KodiBackend.dll"]