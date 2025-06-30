# Gebruik de officiële .NET runtime image als base
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

# Gebruik de .NET SDK image voor bouwen en publiceren
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore server.csproj
RUN dotnet publish server.csproj -c Release -o /app/publish

# Definieer de runtime en stel de poort in naar 80
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Stel de omgevingsvariabele in zodat de app naar poort 80 luistert
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "server.dll"]
