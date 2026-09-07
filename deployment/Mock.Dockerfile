FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source
COPY global.json ./
COPY Artiact.MockService/ Artiact.MockService/
COPY Artiact.Contracts/ Artiact.Contracts/
RUN dotnet publish Artiact.MockService/Artiact.MockService.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Artiact.MockService.dll"]
