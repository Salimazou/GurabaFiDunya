# Use the official .NET runtime image as base
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the .NET SDK image for building and publishing
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["GurabaFiDunya.csproj", "./"]
RUN dotnet restore "GurabaFiDunya.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "GurabaFiDunya.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GurabaFiDunya.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Define the runtime and set the port to 80
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Create a non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "GurabaFiDunya.dll"] 