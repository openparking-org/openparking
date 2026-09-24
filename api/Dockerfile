FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "OpenParking.Api.dll"]
