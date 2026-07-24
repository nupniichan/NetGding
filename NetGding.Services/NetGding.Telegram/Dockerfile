FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NetGding.Services/NetGding.Telegram/NetGding.Telegram.csproj", "NetGding.Services/NetGding.Telegram/"]
COPY ["NetGding.Contracts/NetGding.Contracts.csproj", "NetGding.Contracts/"]
COPY ["NetGding.Configurations/NetGding.Configurations.csproj", "NetGding.Configurations/"]
RUN dotnet restore "NetGding.Services/NetGding.Telegram/NetGding.Telegram.csproj"

COPY . .
WORKDIR "/src/NetGding.Services/NetGding.Telegram"
RUN dotnet publish "NetGding.Telegram.csproj" -c Release -o /app/publish /p:UseAppHost=false
COPY appsettings.Shared.json /app/publish/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
LABEL org.opencontainers.image.title="NetGding.Telegram" \
      org.opencontainers.image.description="NetGding Telegram Bot Service"
WORKDIR /app
USER app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NetGding.Telegram.dll"]