FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["VehicleData.Core.Web/VehicleData.Core.Web.csproj", "VehicleData.Core.Web/"]
RUN dotnet restore "VehicleData.Core.Web/VehicleData.Core.Web.csproj"
COPY . .
WORKDIR "/src/VehicleData.Core.Web"
RUN dotnet build "./VehicleData.Core.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VehicleData.Core.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VehicleData.Core.Web.dll"]
