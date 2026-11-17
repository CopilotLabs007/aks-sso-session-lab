FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/SsoSession.Api/SsoSession.Api.csproj backend/SsoSession.Api/
RUN dotnet restore backend/SsoSession.Api/SsoSession.Api.csproj

COPY backend/SsoSession.Api/ backend/SsoSession.Api/
WORKDIR /src/backend/SsoSession.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SsoSession.Api.dll"]