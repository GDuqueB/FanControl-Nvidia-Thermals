# Build notes

## English

This repository contains the source code for the FanControl plugin and the limited Pawn module source used during the Hot Spot investigation.

### Plugin build

The main plugin project is:

- `src/FanControlNvidiaThermals/FanControlNvidiaThermals.csproj`

Important detail:

- the project currently references `FanControl.Plugins.dll` from a local FanControl installation at `C:\Program Files (x86)\FanControl\FanControl.Plugins.dll`

That means you need FanControl installed locally, or you need to adjust the reference path in the project file before building.

Typical local build:

```powershell
dotnet build .\src\FanControlNvidiaThermals\FanControlNvidiaThermals.csproj -c Release
```

### Pawn module build

Helper script:

- `scripts/build-pawn.cmd`

This script was used during local experimentation and expects a Windows machine with the required Visual Studio Build Tools and local reference files already present.

## Espanol

Este repositorio contiene el código fuente del plugin de FanControl y el código fuente del módulo Pawn limitado utilizado durante la investigación del Hot Spot.

### Compilación del plugin

El proyecto principal del plugin es:

- `src/FanControlNvidiaThermals/FanControlNvidiaThermals.csproj`

Detalle importante:

- el proyecto referencia actualmente `FanControl.Plugins.dll` desde una instalación local de FanControl en `C:\Program Files (x86)\FanControl\FanControl.Plugins.dll`

Eso significa que necesitas tener FanControl instalado localmente, o bien ajustar esa ruta de referencia en el archivo del proyecto antes de compilar.

Compilación local típica:

```powershell
dotnet build .\src\FanControlNvidiaThermals\FanControlNvidiaThermals.csproj -c Release
```

### Compilación del módulo Pawn

Script auxiliar:

- `scripts/build-pawn.cmd`

Ese script se utilizó durante las pruebas locales y da por hecho un equipo Windows con Visual Studio Build Tools y con los archivos de referencia ya presentes en disco.
