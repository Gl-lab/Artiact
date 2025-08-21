FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY *.csproj .
RUN dotnet restore 

# Копируем остальные исходные файлы и собираем приложение
COPY . .
RUN dotnet publish -c release -o /app --no-restore

# Этап выполнения
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Artiact.dll"] 