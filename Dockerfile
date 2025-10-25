# مرحله build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "./XAUUSDAnalyzerApi.csproj"
RUN dotnet publish "./XAUUSDAnalyzerApi.csproj" -c Release -o /app/publish

# مرحله runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "XAUUSDAnalyzerApi.dll"]
