FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Project files first, so that a source-only change does not invalidate the restore layer.
COPY src/Domain/GraphQLGatewayService.Domain.csproj src/Domain/
COPY src/Infrastructure/GraphQLGatewayService.Infrastructure.csproj src/Infrastructure/
COPY src/Api/GraphQLGatewayService.Api.csproj src/Api/
RUN dotnet restore src/Api/GraphQLGatewayService.Api.csproj

COPY src/ src/
RUN dotnet publish src/Api/GraphQLGatewayService.Api.csproj \
    -c "$BUILD_CONFIGURATION" \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GraphQLGatewayService.Api.dll"]
