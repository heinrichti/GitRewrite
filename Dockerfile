FROM debian:bullseye AS publish
WORKDIR /src
RUN apt update && apt install -y wget
RUN wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
RUN apt update && apt install -y dotnet-sdk-7.0 clang zlib1g-dev
COPY ["GitRewrite/GitRewrite.csproj", "GitRewrite/"]
RUN dotnet restore "GitRewrite/GitRewrite.csproj"
COPY . .
WORKDIR "/src/GitRewrite"
RUN dotnet publish "GitRewrite.csproj" -c Release -o /app/publish

FROM debian:bullseye-slim AS final
WORKDIR /app
COPY --from=publish /app/publish/GitRewrite .
ENTRYPOINT ["/app/GitRewrite"]
