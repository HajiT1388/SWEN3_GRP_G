FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build

COPY ["nuget.config", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["DMSG3.Domain/DMSG3.Domain.csproj", "DMSG3.Domain/"]
COPY ["DMSG3.Infrastructure/DMSG3.Infrastructure.csproj", "DMSG3.Infrastructure/"]
COPY ["DMSG3.REST/DMSG3.REST.csproj", "DMSG3.REST/"]

RUN dotnet restore "DMSG3.REST/DMSG3.REST.csproj"

COPY . .
WORKDIR /build/DMSG3.REST
RUN dotnet publish "DMSG3.REST.csproj" -c Release -o /out

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /out .
ENTRYPOINT ["dotnet", "DMSG3.REST.dll"]