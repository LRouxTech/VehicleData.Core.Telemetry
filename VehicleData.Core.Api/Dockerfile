FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["VehicleData.Core.Api/VehicleData.Core.Api.csproj", "VehicleData.Core.Api/"]
COPY ["VehicleData.Core.Database/VehicleData.Core.Database.csproj", "VehicleData.Core.Database/"]
COPY ["VehicleData.Core.Constants/VehicleData.Core.Constants.csproj", "VehicleData.Core.Constants/"]
RUN dotnet restore "VehicleData.Core.Api/VehicleData.Core.Api.csproj"
COPY . .
WORKDIR "/src/VehicleData.Core.Api"
RUN dotnet build "./VehicleData.Core.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VehicleData.Core.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VehicleData.Core.Api.dll"]
