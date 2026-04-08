FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["NetGding.Services/NetGding.WebAPI/NetGding.WebAPI.csproj", "NetGding.Services/NetGding.WebAPI/"]
COPY ["NetGding.Contracts/NetGding.Contracts.csproj", "NetGding.Contracts/"]
COPY ["NetGding.Configurations/NetGding.Configurations.csproj", "NetGding.Configurations/"]
RUN dotnet restore "NetGding.Services/NetGding.WebAPI/NetGding.WebAPI.csproj"

COPY . .
WORKDIR "/src/NetGding.Services/NetGding.WebAPI"
RUN dotnet publish "NetGding.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NetGding.WebAPI.dll"]