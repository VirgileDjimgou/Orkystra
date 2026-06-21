FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY backend/Orkystra.slnx backend/Orkystra.slnx
COPY backend/src/Orkystra.Api/Orkystra.Api.csproj backend/src/Orkystra.Api/
COPY backend/src/Orkystra.Application/Orkystra.Application.csproj backend/src/Orkystra.Application/
COPY backend/src/Orkystra.Contracts/Orkystra.Contracts.csproj backend/src/Orkystra.Contracts/
COPY backend/src/Orkystra.Domain/Orkystra.Domain.csproj backend/src/Orkystra.Domain/
RUN dotnet restore backend/Orkystra.slnx

COPY backend/ backend/
RUN dotnet publish backend/src/Orkystra.Api/Orkystra.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Orkystra.Api.dll"]
