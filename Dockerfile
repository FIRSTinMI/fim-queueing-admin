﻿FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["fim-queueing-admin.csproj", "./"]
RUN dotnet restore "fim-queueing-admin.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "fim-queueing-admin.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "fim-queueing-admin.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "fim-queueing-admin.dll"]
