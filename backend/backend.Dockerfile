FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/BluePrintHr.Api/BluePrintHr.Api.csproj backend/BluePrintHr.Api/
RUN dotnet restore backend/BluePrintHr.Api/BluePrintHr.Api.csproj

COPY backend/BluePrintHr.Api/ backend/BluePrintHr.Api/
RUN dotnet publish backend/BluePrintHr.Api/BluePrintHr.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    --property:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN addgroup --system --gid 10001 appgroup \
    && adduser --system --uid 10001 --ingroup appgroup appuser

COPY --from=build /app/publish .
RUN chown -R appuser:appgroup /app
USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "BluePrintHr.Api.dll"]
