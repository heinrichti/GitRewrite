#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS publish
WORKDIR /src
COPY ["GitRewrite/GitRewrite.csproj", "GitRewrite/"]
RUN dotnet restore "GitRewrite/GitRewrite.csproj"
COPY . .
WORKDIR "/src/GitRewrite"
RUN dotnet publish "GitRewrite.csproj" -c Release -o /app/publish -r linux-x64 -p:PublishReadyToRun=true --self-contained

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/app/GitRewrite"]