FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Swaggregate.Sample/Swaggregate.Sample.csproj", "Swaggregate.Sample/"]
COPY ["Swaggregate/Swaggregate.csproj", "Swaggregate/"]
RUN dotnet restore "Swaggregate.Sample/Swaggregate.Sample.csproj"
COPY . .
WORKDIR "/src/Swaggregate.Sample"
RUN dotnet build "Swaggregate.Sample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Swaggregate.Sample.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Swaggregate.Sample.dll"]
