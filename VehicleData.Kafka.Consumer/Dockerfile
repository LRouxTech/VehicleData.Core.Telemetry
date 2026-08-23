FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["VehicleData.Kafka.Consumer/VehicleData.Kafka.Consumer.csproj", "VehicleData.Kafka.Consumer/"]
COPY ["VehicleData.Core.Database/VehicleData.Core.Database.csproj", "VehicleData.Core.Database/"]
COPY ["VehicleData.Core.Constants/VehicleData.Core.Constants.csproj", "VehicleData.Core.Constants/"]
RUN dotnet restore "VehicleData.Kafka.Consumer/VehicleData.Kafka.Consumer.csproj"
COPY . .
WORKDIR "/src/VehicleData.Kafka.Consumer"
RUN dotnet build "./VehicleData.Kafka.Consumer.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VehicleData.Kafka.Consumer.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VehicleData.Kafka.Consumer.dll"]
