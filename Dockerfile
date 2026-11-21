FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/AgroOperations.Api/AgroOperations.Api.csproj src/AgroOperations.Api/
RUN dotnet restore src/AgroOperations.Api/AgroOperations.Api.csproj
COPY src/AgroOperations.Api/ src/AgroOperations.Api/
RUN dotnet publish src/AgroOperations.Api/AgroOperations.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgroOperations.Api.dll"]
