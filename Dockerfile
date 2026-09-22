FROM node:22-bookworm-slim AS web
WORKDIR /web
COPY src/GhostWatch.Web/package*.json ./
RUN npm ci
COPY src/GhostWatch.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /source
COPY global.json ./
COPY src/GhostWatch.Api/ src/GhostWatch.Api/
RUN dotnet publish src/GhostWatch.Api -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app/publish ./
COPY --from=web /web/dist/ghost-watch-web/browser/ ./wwwroot/
ENV ASPNETCORE_URLS=http://+:8080
ENV Storage__Directory=/app/data
RUN mkdir -p /app/data && chown -R app:app /app/data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "GhostWatch.Api.dll"]
