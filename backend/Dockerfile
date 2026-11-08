FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Backend.csproj ./src/
RUN dotnet restore ./src/Backend.csproj
COPY src/ ./src/
RUN dotnet publish ./src/Backend.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY init.sql /app/init.sql
EXPOSE 8080
ENTRYPOINT ["dotnet", "Backend.dll"]
