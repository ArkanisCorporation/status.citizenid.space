# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore Template.slnx --locked-mode
RUN dotnet publish src/Template.Service/Template.Service.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Template.Service.dll"]
