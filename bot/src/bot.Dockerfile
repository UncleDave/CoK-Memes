FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet build "ChampionsOfKhazad.Bot/ChampionsOfKhazad.Bot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ChampionsOfKhazad.Bot/ChampionsOfKhazad.Bot.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChampionsOfKhazad.Bot.dll"]
