# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ../../gold-tracker.sln ./
COPY ../GoldTracker.Domain/ ../GoldTracker.Domain/
COPY ../GoldTracker.Application/ ../GoldTracker.Application/
COPY ../GoldTracker.Infrastructure/ ../GoldTracker.Infrastructure/
COPY . ./GoldTracker.Api/
RUN dotnet restore ./GoldTracker.Api/GoldTracker.Api.csproj
RUN dotnet publish ./GoldTracker.Api/GoldTracker.Api.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GoldTracker.Api.dll"]
