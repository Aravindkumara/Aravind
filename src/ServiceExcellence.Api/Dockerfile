# Build context: repository root (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ServiceExcellence.sln ./
COPY src/ServiceExcellence.Core/ServiceExcellence.Core.csproj src/ServiceExcellence.Core/
COPY src/ServiceExcellence.Data/ServiceExcellence.Data.csproj src/ServiceExcellence.Data/
COPY src/ServiceExcellence.Api/ServiceExcellence.Api.csproj src/ServiceExcellence.Api/
RUN dotnet restore src/ServiceExcellence.Api/ServiceExcellence.Api.csproj
COPY src/ src/
RUN dotnet publish src/ServiceExcellence.Api/ServiceExcellence.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/uploads /app/logs && chown -R app:app /app/uploads /app/logs
USER app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ServiceExcellence.Api.dll"]
