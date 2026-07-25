FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NetGding.Services/NetGding.Collector/NetGding.Collector.csproj", "NetGding.Services/NetGding.Collector/"]
COPY ["NetGding.Services/NetGding.Analyzer/NetGding.Analyzer.csproj", "NetGding.Services/NetGding.Analyzer/"]
COPY ["NetGding.Contracts/NetGding.Contracts.csproj", "NetGding.Contracts/"]
COPY ["NetGding.Configurations/NetGding.Configurations.csproj", "NetGding.Configurations/"]
RUN dotnet restore "NetGding.Services/NetGding.Collector/NetGding.Collector.csproj"

COPY . .
WORKDIR "/src/NetGding.Services/NetGding.Collector"
RUN dotnet publish "NetGding.Collector.csproj" -c Release -o /app/publish /p:UseAppHost=false
COPY appsettings.Shared.json /app/publish/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
LABEL org.opencontainers.image.title="NetGding.Collector" \
      org.opencontainers.image.description="NetGding Market Data Collector Service"
WORKDIR /app
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NetGding.Collector.dll"]