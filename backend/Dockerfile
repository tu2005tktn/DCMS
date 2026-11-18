# Build context: backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Low-memory build for a small server: one MSBuild process, capped GC heap, no lingering build servers.
ENV DOTNET_GCHeapHardLimit=0x40000000     DOTNET_CLI_TELEMETRY_OPTOUT=1     DOTNET_NOLOGO=1     MSBUILDDISABLENODEREUSE=1     DOTNET_CLI_USE_MSBUILD_SERVER=0
COPY src/DCMS.Domain/DCMS.Domain.csproj src/DCMS.Domain/
COPY src/DCMS.Application/DCMS.Application.csproj src/DCMS.Application/
COPY src/DCMS.Infrastructure/DCMS.Infrastructure.csproj src/DCMS.Infrastructure/
COPY src/DCMS.Api/DCMS.Api.csproj src/DCMS.Api/
RUN dotnet restore src/DCMS.Api/DCMS.Api.csproj -maxcpucount:1 --disable-build-servers
COPY src/ src/
RUN dotnet publish src/DCMS.Api/DCMS.Api.csproj -c Release -o /app --no-restore -maxcpucount:1 --disable-build-servers

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app .
RUN mkdir -p /data/clinical-documents /data/branch-assets && chown -R app:app /data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "DCMS.Api.dll"]
