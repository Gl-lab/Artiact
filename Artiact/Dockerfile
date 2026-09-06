FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY global.json ./
COPY Artiact/Artiact.csproj Artiact/
COPY Artiact.Contracts/Artiact.Contracts.csproj Artiact.Contracts/
RUN dotnet restore Artiact/Artiact.csproj

# Копируем остальные исходные файлы и собираем приложение
COPY Artiact/ Artiact/
COPY Artiact.Contracts/ Artiact.Contracts/
RUN dotnet publish Artiact/Artiact.csproj -c Release -o /app --no-restore

# Этап выполнения
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Artiact.dll"]
