FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpenParking.sln .
COPY OpenParking.Core/OpenParking.Core.csproj OpenParking.Core/
COPY OpenParking.Infrastructure/OpenParking.Infrastructure.csproj OpenParking.Infrastructure/
COPY OpenParking.Api/OpenParking.Api.csproj OpenParking.Api/
COPY OpenParking.Tests/OpenParking.Tests.csproj OpenParking.Tests/

RUN dotnet restore OpenParking.sln

COPY . .
RUN dotnet publish OpenParking.Api/OpenParking.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# docker-compose.prod.yml health-checks this container with curl, which the
# aspnet runtime image does not include; without it the check can never pass.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OpenParking.Api.dll"]
