# Estágio 1: build — usa a imagem do SDK (compilador, MSBuild) só para restaurar e publicar.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Dispatch.slnx .
COPY src/Dispatch.Domain/Dispatch.Domain.csproj src/Dispatch.Domain/
COPY src/Dispatch.Application/Dispatch.Application.csproj src/Dispatch.Application/
COPY src/Dispatch.Infrastructure/Dispatch.Infrastructure.csproj src/Dispatch.Infrastructure/
COPY src/Dispatch.Api/Dispatch.Api.csproj src/Dispatch.Api/
RUN dotnet restore src/Dispatch.Api/Dispatch.Api.csproj

COPY src/ src/
RUN dotnet publish src/Dispatch.Api/Dispatch.Api.csproj -c Release -o /app --no-restore

# Estágio 2: runtime — imagem enxuta, só o necessário para executar a aplicação publicada.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Dispatch.Api.dll"]
