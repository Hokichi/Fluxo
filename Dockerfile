# escape=`

FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

# Copy project files first to improve layer caching for restore.
COPY ["Fluxo/Fluxo.csproj", "Fluxo/"]
COPY ["Fluxo.Core/Fluxo.Core.csproj", "Fluxo.Core/"]
COPY ["Fluxo.Data/Fluxo.Data.csproj", "Fluxo.Data/"]
COPY ["Fluxo.Resources/Fluxo.Resources.csproj", "Fluxo.Resources/"]
COPY ["Fluxo.Services/Fluxo.Services.csproj", "Fluxo.Services/"]

RUN dotnet restore "Fluxo/Fluxo.csproj"

COPY . .

RUN dotnet publish "Fluxo/Fluxo.csproj" `
    -c Release `
    -o C:\app\publish `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true

FROM mcr.microsoft.com/windows/servercore:ltsc2022 AS runtime
WORKDIR /app

COPY --from=build C:\app\publish\ .\

ENTRYPOINT ["C:\\app\\fluxo.exe"]
