FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["nuget.config", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["DMSG3.Worker/DMSG3.Worker.csproj", "DMSG3.Worker/"]
COPY ["DMSG3.Domain/DMSG3.Domain.csproj", "DMSG3.Domain/"]
COPY ["DMSG3.Infrastructure/DMSG3.Infrastructure.csproj", "DMSG3.Infrastructure/"]

RUN dotnet restore "DMSG3.Worker/DMSG3.Worker.csproj"

COPY . .
WORKDIR /src/DMSG3.Worker
RUN dotnet publish "DMSG3.Worker.csproj" -c Release -o /out

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr \
        tesseract-ocr-eng \
        tesseract-ocr-deu \
        ghostscript \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
ENTRYPOINT ["dotnet", "DMSG3.Worker.dll"]