FROM node:22 AS build-frontend
WORKDIR /src
COPY ./ChampionsOfKhazad.Bot.Portal/frontend .
RUN npm ci --no-audit
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-backend
WORKDIR /src
COPY . .
COPY --from=build-frontend /src/dist ./ChampionsOfKhazad.Bot.Portal/wwwroot
RUN dotnet publish "ChampionsOfKhazad.Bot.Portal/ChampionsOfKhazad.Bot.Portal.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build-backend /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChampionsOfKhazad.Bot.Portal.dll"]
