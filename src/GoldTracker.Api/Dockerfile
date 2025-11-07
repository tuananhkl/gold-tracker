# ---------- Build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

# copy solution + project files first (restore layer)
COPY ["gold-tracker.sln", "./"]
COPY ["src/GoldTracker.Domain/GoldTracker.Domain.csproj", "src/GoldTracker.Domain/"]
COPY ["src/GoldTracker.Application/GoldTracker.Application.csproj", "src/GoldTracker.Application/"]
COPY ["src/GoldTracker.Infrastructure/GoldTracker.Infrastructure.csproj", "src/GoldTracker.Infrastructure/"]
COPY ["src/GoldTracker.Api/GoldTracker.Api.csproj", "src/GoldTracker.Api/"]

RUN dotnet restore "src/GoldTracker.Api/GoldTracker.Api.csproj"

# copy the rest of the source
COPY . .

RUN dotnet publish "src/GoldTracker.Api/GoldTracker.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------- Runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "GoldTracker.Api.dll"]
