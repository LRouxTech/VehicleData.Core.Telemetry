FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["VehicleData.Console.DataGenerator/VehicleData.Console.DataGenerator.csproj", "VehicleData.Console.DataGenerator/"]
RUN dotnet restore "VehicleData.Console.DataGenerator/VehicleData.Console.DataGenerator.csproj"
COPY . .
WORKDIR "/src/VehicleData.Console.DataGenerator"
RUN dotnet build "./VehicleData.Console.DataGenerator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./VehicleData.Console.DataGenerator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VehicleData.Console.DataGenerator.dll"]
