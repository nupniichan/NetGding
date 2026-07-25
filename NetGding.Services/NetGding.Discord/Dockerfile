FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NetGding.Services/NetGding.Discord/NetGding.Discord.csproj", "NetGding.Services/NetGding.Discord/"]
COPY ["NetGding.Contracts/NetGding.Contracts.csproj", "NetGding.Contracts/"]
COPY ["NetGding.Configurations/NetGding.Configurations.csproj", "NetGding.Configurations/"]
RUN dotnet restore "NetGding.Services/NetGding.Discord/NetGding.Discord.csproj"

COPY . .
WORKDIR "/src/NetGding.Services/NetGding.Discord"
RUN dotnet publish "NetGding.Discord.csproj" -c Release -o /app/publish /p:UseAppHost=false
COPY appsettings.Shared.json /app/publish/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
LABEL org.opencontainers.image.title="NetGding.Discord" \
      org.opencontainers.image.description="NetGding Discord Bot Service"
WORKDIR /app
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NetGding.Discord.dll"]