#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/UrlShortener.Frontend/UrlShortener.Frontend.csproj", "src/UrlShortener.Frontend/"]
COPY ["src/UrlShortener.Backend.Grains/UrlShortener.Backend.Grains.csproj", "src/UrlShortener.Backend.Grains/"]
COPY ["src/UrlShortener.Backend.Interfaces/UrlShortener.Backend.Interfaces.csproj", "src/UrlShortener.Backend.Interfaces/"]
COPY ["src/UrlShortener.Infra.Silo/UrlShortener.Infra.Silo.csproj", "src/UrlShortener.Infra.Silo/"]
RUN dotnet restore "src/UrlShortener.Frontend/UrlShortener.Frontend.csproj"
COPY . .
WORKDIR "/src/src/UrlShortener.Frontend"
RUN dotnet build "UrlShortener.Frontend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "UrlShortener.Frontend.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UrlShortener.Frontend.dll"]