FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["nuget.config", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["DMSG3.GenAIWorker/DMSG3.GenAIWorker.csproj", "DMSG3.GenAIWorker/"]
COPY ["DMSG3.Domain/DMSG3.Domain.csproj", "DMSG3.Domain/"]
COPY ["DMSG3.Infrastructure/DMSG3.Infrastructure.csproj", "DMSG3.Infrastructure/"]

RUN dotnet restore "DMSG3.GenAIWorker/DMSG3.GenAIWorker.csproj"

COPY . .
WORKDIR /src/DMSG3.GenAIWorker
RUN dotnet publish "DMSG3.GenAIWorker.csproj" -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "DMSG3.GenAIWorker.dll"]
