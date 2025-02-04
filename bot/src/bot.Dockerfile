﻿FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "ChampionsOfKhazad.Bot/ChampionsOfKhazad.Bot.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ChampionsOfKhazad.Bot.dll"]
