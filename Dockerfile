FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy-arm64v8  AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["MrMovieClubCEO/*.csproj", "MrMovieClubCEO/"]
RUN dotnet restore "MrMovieClubCEO/MrMovieClubCEO.csproj"

# Copy the rest of the code
COPY . .
WORKDIR "/src/MrMovieClubCEO"

# Build the application
RUN dotnet build "MrMovieClubCEO.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "MrMovieClubCEO.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM mcr.microsoft.com/dotnet/runtime:8.0-jammy-arm64v8  AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a directory for configuration files
RUN mkdir -p /app/config

# Set the entry point for the application
ENTRYPOINT ["dotnet", "MrMovieClubCEO.dll"]