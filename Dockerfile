FROM alpine AS publish
WORKDIR /src
COPY ["GitRewrite/GitRewrite.csproj", "GitRewrite/"]
RUN apk update && apk add clang build-base zlib-dev dotnet7-sdk
RUN dotnet restore "GitRewrite/GitRewrite.csproj"
COPY . .
WORKDIR "/src/GitRewrite"
RUN dotnet publish "GitRewrite.csproj" -c Release -o /app/publish

FROM alpine AS final
RUN apk add libstdc++
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/app/GitRewrite"]
