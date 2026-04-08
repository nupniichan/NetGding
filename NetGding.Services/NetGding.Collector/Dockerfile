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

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NetGding.Collector.dll"]