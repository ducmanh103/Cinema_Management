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

# Cài curl cho HEALTHCHECK
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Tạo non-root user (security best practice)
RUN adduser --disabled-password --gecos '' --uid 1001 appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Tạo thư mục lưu DataProtection keys & logs thuộc quyền sở hữu của appuser
RUN mkdir -p /app/dp_keys /app/logs && chown -R appuser:appuser /app

# Chuyển sang non-root user
USER appuser

HEALTHCHECK --interval=30s --timeout=5s --retries=3 --start-period=10s \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "CINEMA_MANAGEMENT.dll"]
