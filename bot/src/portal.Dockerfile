FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-backend
WORKDIR /src
COPY . .
RUN dotnet publish "ChampionsOfKhazad.Bot.Portal/ChampionsOfKhazad.Bot.Portal.csproj" -c Release -o /app/publish

FROM node:22 AS build-frontend
WORKDIR /src
COPY ./ChampionsOfKhazad.Bot.Portal/frontend .
RUN npm i --no-audit
RUN npm run build
RUN mkdir /app
RUN cp -r dist /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build-backend /app/publish .
COPY --from=build-frontend /app/publish wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChampionsOfKhazad.Bot.Portal.dll"]
