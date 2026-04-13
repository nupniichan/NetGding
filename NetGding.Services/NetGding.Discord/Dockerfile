FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NetGding.Services/NetGding.Discord/NetGding.Discord.csproj", "NetGding.Services/NetGding.Discord/"]
COPY ["NetGding.Contracts/NetGding.Contracts.csproj", "NetGding.Contracts/"]
COPY ["NetGding.Configurations/NetGding.Configurations.csproj", "NetGding.Configurations/"]
RUN dotnet restore "NetGding.Services/NetGding.Discord/NetGding.Discord.csproj"

COPY . .
WORKDIR "/src/NetGding.Services/NetGding.Discord"
RUN dotnet publish "NetGding.Discord.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NetGding.Discord.dll"]