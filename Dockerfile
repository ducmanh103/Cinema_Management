# ==========================================
# 1. Build Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy file .csproj để restore dependencies trước (tận dụng Docker layer cache)
COPY ["CINEMA_MANAGEMENT.csproj", "./"]
RUN dotnet restore "CINEMA_MANAGEMENT.csproj"

# Copy toàn bộ code và build publish
COPY . .
RUN dotnet publish "CINEMA_MANAGEMENT.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# 2. Runtime Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CINEMA_MANAGEMENT.dll"]
