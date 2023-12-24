FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-backend
WORKDIR /src
COPY . .
RUN dotnet build "ChampionsOfKhazad.Bot.Portal/ChampionsOfKhazad.Bot.Portal.csproj" -c Release -o /app/build

FROM build-backend AS publish-backend
RUN dotnet publish "ChampionsOfKhazad.Bot.Portal/ChampionsOfKhazad.Bot.Portal.csproj" -c Release -o /app/publish

FROM node:20 AS build-frontend
WORKDIR /src
COPY ./ChampionsOfKhazad.Bot.Portal/frontend .
RUN npm i --no-audit
RUN npm run build

FROM build-frontend as publish-frontend
RUN mkdir /app
RUN cp -r dist /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish-backend /app/publish .
COPY --from=publish-frontend /app/publish wwwroot
RUN ls
RUN ls wwwroot
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChampionsOfKhazad.Bot.Portal.dll"]
