FROM mcr.microsoft.com/dotnet/core/runtime:2.1-stretch-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:2.1-stretch AS build
WORKDIR /src
COPY ["GitRewrite/GitRewrite.csproj", "GitRewrite/"]
RUN dotnet restore "GitRewrite/GitRewrite.csproj"
COPY . .
WORKDIR "/src/GitRewrite"
RUN dotnet build "GitRewrite.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "GitRewrite.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "GitRewrite.dll"]