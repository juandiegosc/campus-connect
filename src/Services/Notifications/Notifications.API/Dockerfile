# Build context: root of the repo (see docker-compose.yml)
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy MSBuild props (Central Package Management + root build props)
COPY Directory.Packages.props .
COPY Directory.Build.props .

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Notifications/ src/Services/Notifications/

RUN dotnet restore "src/Services/Notifications/Notifications.API/Notifications.API.csproj"
RUN dotnet publish "src/Services/Notifications/Notifications.API/Notifications.API.csproj" \
    -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Notifications.API.dll"]
