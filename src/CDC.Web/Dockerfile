# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

COPY ["src/CDC.Web/CDC.Web.csproj", "src/CDC.Web/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/CDC.Web/CDC.Web.csproj"

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "src/CDC.Web/CDC.Web.csproj" -c Release -o /app/publish \
    --no-restore /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS final
WORKDIR /app
# Must match the ECS task definition's containerPort (and ALB target group
# port, if fronted by one) - coordinate any change here with infra.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CDC.Web.dll"]
