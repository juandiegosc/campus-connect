# Build context: root of the repo (see docker-compose.yml)
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy MSBuild props (Central Package Management + root build props)
COPY Directory.Packages.props .
COPY Directory.Build.props .

COPY src/Gateway/ src/Gateway/

RUN dotnet restore "src/Gateway/CampusConnect.Gateway/CampusConnect.Gateway.csproj"
RUN dotnet publish "src/Gateway/CampusConnect.Gateway/CampusConnect.Gateway.csproj" \
    -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CampusConnect.Gateway.dll"]
