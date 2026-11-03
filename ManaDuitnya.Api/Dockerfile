FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ManaDuitnya.Api/ManaDuitnya.Api.csproj","ManaDuitnya.Api/"]
RUN dotnet restore "ManaDuitnya.Api/ManaDuitnya.Api.csproj"

COPY . .
WORKDIR "/src/ManaDuitnya.Api"
RUN dotnet publish "ManaDuitnya.Api.csproj" -c Release -o /app/publish /p:UserAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT [ "dotnet", "ManaDuitnya.Api.dll" ]