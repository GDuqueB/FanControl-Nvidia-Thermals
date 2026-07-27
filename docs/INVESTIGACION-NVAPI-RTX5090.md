# RTX 5090: temperaturas de core y memoria mediante NVAPI

## Resultado

La RTX 5090 expone directamente las temperaturas de core y memoria mediante la interfaz NVAPI interna `NvAPI_GPU_GetThermalSensors`, identificada por `0x65FE3AAD`. No requieren GPU-Z, HWMonitor, PawnIO ni una lectura MMIO.

Para la RTX 5090 probada:

- `Temperatures[1] / 256.0`: temperatura del core, con precisión fraccionaria.
- `Temperatures[2] / 256.0`: temperatura de unión de la memoria GDDR7.
- `0x0000FF00 / 256.0 = 255`: marcador de sensor no válido, no una temperatura real.
- máscara aceptada: `0x00FFFFFF`; el bit 24 devuelve error.

La interfaz no proporciona el hotspot correcto en Blackwell. El índice 1, utilizado como hotspot en generaciones anteriores, contiene ahora la temperatura del core. Esto coincide con el cambio realizado en LibreHardwareMonitor para las RTX 50.

## Prueba local

Se creó `src/NvApiThermalProbe`, que llama directamente a `nvapi64.dll`, enumera la GPU física, determina la máscara admitida y vuelca los 32 valores.

Una ejecución produjo:

```text
GPU 0: NVIDIA GeForce RTX 5090
temperature[1] = 34.613 C
temperature[2] = 46.000 C
```

La llamada NVAPI tradicional `NvAPI_GPU_GetThermalSettings` produjo simultáneamente 34 °C para el core. Su resolución es de un grado, mientras que `GetThermalSensors[1]` conserva la fracción.

## Comparación simultánea con GPU-Z

Durante diez muestras, la memoria compartida de GPU-Z y la consulta NVAPI directa mostraron la misma escala y evolución:

```text
GPU-Z core:       34.555 -> 33.738 C
NVAPI index 1:    34.613 -> 33.711 C
GPU-Z memory:     46/48 C
NVAPI index 2:    46 C
```

Las pequeñas diferencias corresponden a instantes y frecuencias de actualización distintos. La identificación de la memoria como índice 2 también coincide con la implementación específica para RTX 50 del código actual de LibreHardwareMonitor.

## Consecuencia para el proyecto

El plugin de FanControl debe combinar dos fuentes:

1. NVAPI en modo usuario para `GPU Core` y `GPU Memory Junction`.
2. El módulo PawnIO limitado para el hotspot de GB202.

No conviene añadir core ni memoria al módulo PawnIO. Hacerlo ampliaría innecesariamente el código privilegiado y la superficie que el mantenedor debe revisar y firmar.
