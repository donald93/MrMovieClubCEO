FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy-arm64v8 AS build
WORKDIR /src

RUN mkdir https
RUN dotnet dev-certs https -ep /https/aspnetapp.pfx -p "Test1234"

# Copy csproj and restore dependencies
COPY ["MrMovieClubCEO/*.csproj", "MrMovieClubCEO/"]
RUN dotnet restore "MrMovieClubCEO/MrMovieClubCEO.csproj"

# Copy the rest of the code
COPY . .
WORKDIR "/src/MrMovieClubCEO"

# Build the application
RUN dotnet build "MrMovieClubCEO.csproj" -c Release -o /app/build

# Publish the application as self-contained
FROM build AS publish
RUN dotnet publish "MrMovieClubCEO.csproj" -c Release -o /app/publish -r linux-arm64 --self-contained true

# Final image - use a minimal base image since we're including the .NET runtime
FROM debian:bookworm-slim AS final
WORKDIR /app
RUN apt-get update && apt-get install -y libicu-dev && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .
COPY --chmod=0755 --from=publish /https/* /https/

# Create a directory for configuration files
RUN mkdir -p /app/config

RUN update-ca-certificates

# Set the entry point for the application (using the native executable)
ENTRYPOINT ["/app/MrMovieClubCEO"]