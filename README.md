# FanControl.NvidiaThermals

English | [Español](#spanish)

<table>
  <tr>
    <td valign="top">

### Contents

- [English](#english)
- [Why this exists](#why-this-exists)
- [Current status](#current-status)
- [Compatibility](#compatibility)
- [Requirements](#requirements)
- [Installation](#installation)
- [Diagnostics](#diagnostics)
- [Repository layout](#repository-layout)
- [Legal note](#legal-note)
- [License](#license)

  </td>
  <td valign="top">

### Índice

- [Español](#spanish)
- [Por qué existe](#por-qué-existe)
- [Estado actual](#estado-actual)
- [Compatibilidad](#compatibilidad)
- [Requisitos](#requisitos)
- [Instalación](#instalación)
- [Diagnóstico](#diagnóstico)
- [Estructura del repositorio](#estructura-del-repositorio)
- [Nota legal](#nota-legal)
- [Licencia](#licencia)

  </td>
  </tr>
</table>

`FanControl.NvidiaThermals` is a FanControl plugin for NVIDIA GPUs that exposes:

- `GPU Core`
- `GPU Memory Junction`
- `GPU Hot Spot`

The project exists to provide these temperatures directly inside FanControl without depending on GPU-Z or HWMonitor running in the background.

<a id="english"></a>

## English

### Why this exists

Recent NVIDIA driver changes broke the old Hot Spot reading path used by `FanControl.NvThermalSensors`. On RTX 50 GPUs, the Hot Spot value can still be read reliably through a signed PawnIO module, while Core and Memory Junction remain available through NVAPI.

This plugin combines both paths:

- `NVAPI` for `GPU Core`
- `NVAPI` for `GPU Memory Junction`
- `PawnIO + Nvidia.bin` for `GPU Hot Spot` on RTX 50 GPUs

### Current status

- Validated on `NVIDIA GeForce RTX 5090`
- `GPU Hot Spot` matches `HWMonitor 1.65.1`
- PCIe device/function auto-detection is implemented, so the plugin is not limited to `bus:device:function = xx:00.0`

### Compatibility

- `RTX 50`
  - `GPU Core`: supported
  - `GPU Memory Junction`: supported
  - `GPU Hot Spot`: supported through PawnIO
- `RTX 40`
  - `GPU Core`: supported
  - `GPU Memory Junction`: supported
  - `GPU Hot Spot`: uses the NVAPI thermal sensor path
- `Older NVIDIA generations`
  - The plugin includes fallback logic, but they have not been validated on enough hardware to claim full support yet.

Important note:

- The code is designed to support more than the RTX 5090, but the strongest real-world validation so far is still on an RTX 5090 Founders Edition.

### Requirements

- Windows
- FanControl
- NVIDIA GPU
- `PawnIO` installed if you want `GPU Hot Spot` on RTX 50
- Official signed `Nvidia.bin` module from PawnIO

### Installation

1. Install `PawnIO`.
2. Place `FanControl.NvidiaThermals.dll` in your FanControl plugin folder.
3. Place `Nvidia.bin` in one of these locations:
   - next to the plugin DLL
   - inside a `modules` folder next to the plugin DLL
   - inside the FanControl base folder
   - inside `C:\Program Files\PawnIO\Modules\`
4. Restart FanControl.
5. Look for these sensors:
   - `GPU Core`
   - `GPU Memory Junction`
   - `GPU Hot Spot`

### Diagnostics

The plugin writes a log file here:

- `FanControl.NvidiaThermals.log` in the FanControl base directory

Useful things to check in the log:

- whether `Nvidia.bin` was found and loaded
- which PCI address was resolved for the GPU
- whether thermal register reads succeeded

### Repository layout

- [src/FanControlNvidiaThermals](C:/Users/DEEP/Documents/HWMonitor/src/FanControlNvidiaThermals) - main plugin source code
- [src/PawnModules](C:/Users/DEEP/Documents/HWMonitor/src/PawnModules) - Pawn source for the limited thermal module
- [scripts](C:/Users/DEEP/Documents/HWMonitor/scripts) - helper scripts used during development
- [docs/BUILD.md](C:/Users/DEEP/Documents/HWMonitor/docs/BUILD.md) - local build notes
- [docs/INVESTIGACION-HOTSPOT.md](C:/Users/DEEP/Documents/HWMonitor/docs/INVESTIGACION-HOTSPOT.md) - hotspot reverse-engineering notes
- [docs/INVESTIGACION-NVAPI-RTX5090.md](C:/Users/DEEP/Documents/HWMonitor/docs/INVESTIGACION-NVAPI-RTX5090.md) - NVAPI notes for RTX 5090

### Legal note

This repository should contain the plugin source code, but it is better not to publish third-party signed binaries such as `Nvidia.bin` or `PawnIOLib.dll` unless their license clearly allows redistribution.

A good public setup is:

- keep this repository focused on source code and documentation
- provide build instructions
- explain where users must obtain `PawnIO` and `Nvidia.bin`

### License

This project is published under the `MIT` license.

Publishing notes are available in [docs/GITHUB-PUBLISHING.md](C:/Users/DEEP/Documents/HWMonitor/docs/GITHUB-PUBLISHING.md).

<a id="spanish"></a>

## Español

`FanControl.NvidiaThermals` es un plugin para FanControl orientado a GPUs NVIDIA que expone:

- `GPU Core`
- `GPU Memory Junction`
- `GPU Hot Spot`

El objetivo del proyecto es ofrecer estas temperaturas directamente en FanControl, sin depender de GPU-Z ni de HWMonitor ejecutándose en segundo plano.

### Por qué existe

Los cambios recientes en los drivers de NVIDIA rompieron la antigua ruta de lectura de Hot Spot usada por `FanControl.NvThermalSensors`. En las RTX 50, el valor de Hot Spot sigue pudiéndose leer de forma fiable mediante un módulo firmado de PawnIO, mientras que las temperaturas de Core y Memory Junction siguen disponibles a través de NVAPI.

Este plugin combina ambas fuentes:

- `NVAPI` para `GPU Core`
- `NVAPI` para `GPU Memory Junction`
- `PawnIO + Nvidia.bin` para `GPU Hot Spot` en RTX 50

### Estado actual

- Validado en `NVIDIA GeForce RTX 5090`
- `GPU Hot Spot` coincide con `HWMonitor 1.65.1`
- La autodetección de dispositivo y función PCIe ya está implementada, así que el plugin no depende de `bus:device:function = xx:00.0`

### Compatibilidad

- `RTX 50`
  - `GPU Core`: compatible
  - `GPU Memory Junction`: compatible
  - `GPU Hot Spot`: compatible mediante PawnIO
- `RTX 40`
  - `GPU Core`: compatible
  - `GPU Memory Junction`: compatible
  - `GPU Hot Spot`: usa la ruta térmica de NVAPI
- `Generaciones anteriores de NVIDIA`
  - El plugin incluye lógica de fallback, pero todavía no se ha validado en suficiente hardware como para asegurar una compatibilidad total.

Nota importante:

- El código está pensado para soportar más modelos además de la RTX 5090, pero la validación real más sólida hasta ahora sigue siendo una RTX 5090 Founders Edition.

### Requisitos

- Windows
- FanControl
- GPU NVIDIA
- `PawnIO` instalado si quieres disponer de `GPU Hot Spot` en RTX 50
- Módulo oficial firmado `Nvidia.bin` de PawnIO

### Instalación

1. Instala `PawnIO`.
2. Coloca `FanControl.NvidiaThermals.dll` en la carpeta de plugins de FanControl.
3. Coloca `Nvidia.bin` en una de estas ubicaciones:
   - junto al DLL del plugin
   - dentro de una carpeta `modules` junto al DLL del plugin
   - dentro de la carpeta base de FanControl
   - dentro de `C:\Program Files\PawnIO\Modules\`
4. Reinicia FanControl.
5. Busca estos sensores:
   - `GPU Core`
   - `GPU Memory Junction`
   - `GPU Hot Spot`

### Diagnóstico

El plugin escribe un log aquí:

- `FanControl.NvidiaThermals.log` en la carpeta base de FanControl

Cosas útiles que revisar en el log:

- si `Nvidia.bin` se encontró y cargó correctamente
- qué dirección PCI se resolvió para la GPU
- si las lecturas de registros térmicos se realizaron correctamente

### Estructura del repositorio

- [src/FanControlNvidiaThermals](C:/Users/DEEP/Documents/HWMonitor/src/FanControlNvidiaThermals) - código fuente principal del plugin
- [src/PawnModules](C:/Users/DEEP/Documents/HWMonitor/src/PawnModules) - código Pawn del módulo térmico limitado
- [scripts](C:/Users/DEEP/Documents/HWMonitor/scripts) - scripts auxiliares usados durante el desarrollo
- [docs/BUILD.md](C:/Users/DEEP/Documents/HWMonitor/docs/BUILD.md) - notas de compilación local
- [docs/INVESTIGACION-HOTSPOT.md](C:/Users/DEEP/Documents/HWMonitor/docs/INVESTIGACION-HOTSPOT.md) - notas de ingeniería inversa del hotspot
- [docs/INVESTIGACION-NVAPI-RTX5090.md](C:/Users/DEEP/Documents/HWMonitor/docs/INVESTIGACION-NVAPI-RTX5090.md) - notas sobre NVAPI en RTX 5090

### Nota legal

Este repositorio está pensado para publicar el código fuente del plugin. Aun así, es preferible no incluir binarios firmados de terceros, como `Nvidia.bin` o `PawnIOLib.dll`, salvo que su licencia permita de forma expresa su redistribución.

Una configuración pública recomendable sería:

- centrar el repositorio en código fuente y documentación
- aportar instrucciones de compilación
- explicar de dónde puede obtener el usuario `PawnIO` y `Nvidia.bin`

### Licencia

Este proyecto se publica bajo licencia `MIT`.

Las notas de publicación están en [docs/GITHUB-PUBLISHING.md](C:/Users/DEEP/Documents/HWMonitor/docs/GITHUB-PUBLISHING.md).
