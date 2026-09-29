FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG GITHUB_USER
ARG GITHUB_TOKEN

COPY ["nuget.config", "Directory.Build.props", "Directory.Packages.props", "./"]

COPY ["src/Kariyer.Recruiting.Domain/Kariyer.Recruiting.Domain.csproj", "src/Kariyer.Recruiting.Domain/"]
COPY ["src/Kariyer.Recruiting.Api/Kariyer.Recruiting.Api.csproj", "src/Kariyer.Recruiting.Api/"]
RUN dotnet restore "src/Kariyer.Recruiting.Api/Kariyer.Recruiting.Api.csproj"

COPY . .
RUN dotnet publish "src/Kariyer.Recruiting.Api/Kariyer.Recruiting.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5340
ENV ASPNETCORE_HTTP_PORTS=5340
ENV HEALTHCHECK_URL=http://localhost:5340/health/live
EXPOSE 5340
COPY --from=build /app/publish .
USER app

HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD ["dotnet", "Kariyer.Recruiting.Api.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "Kariyer.Recruiting.Api.dll"]
