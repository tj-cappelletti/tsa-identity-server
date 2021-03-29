#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443
#TODO: This ideally should not be here and is only needed for VS debugging
COPY ["Tsa.IdentityServer.Web/identityserverseeddata.json", "/data/"]

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["Tsa.IdentityServer.Web/Tsa.IdentityServer.Web.csproj", "Tsa.IdentityServer.Web/"]
RUN dotnet restore "Tsa.IdentityServer.Web/Tsa.IdentityServer.Web.csproj"
COPY . .
WORKDIR "/src/Tsa.IdentityServer.Web"
RUN dotnet build "Tsa.IdentityServer.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Tsa.IdentityServer.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tsa.IdentityServer.Web.dll"]