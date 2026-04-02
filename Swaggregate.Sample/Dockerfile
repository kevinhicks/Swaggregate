FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Pefcu.ApiPortal/Pefcu.ApiPortal.csproj", "Pefcu.ApiPortal/"]
COPY ["SwaggerAggregator/SwaggerAggregator.csproj", "SwaggerAggregator/"]
RUN dotnet restore "Pefcu.ApiPortal/Pefcu.ApiPortal.csproj"
COPY . .
WORKDIR "/src/Pefcu.ApiPortal"
RUN dotnet build "Pefcu.ApiPortal.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Pefcu.ApiPortal.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Pefcu.ApiPortal.dll"]
