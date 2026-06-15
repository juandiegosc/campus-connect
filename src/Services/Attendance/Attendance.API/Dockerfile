# Build context: root of the repo (see docker-compose.yml)
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy MSBuild props (Central Package Management + root build props)
COPY Directory.Packages.props .
COPY Directory.Build.props .

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Attendance/ src/Services/Attendance/

RUN dotnet restore "src/Services/Attendance/Attendance.API/Attendance.API.csproj"
RUN dotnet publish "src/Services/Attendance/Attendance.API/Attendance.API.csproj" \
    -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Attendance.API.dll"]
