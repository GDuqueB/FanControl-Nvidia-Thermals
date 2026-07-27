# HWMonitor 1.65.1: lectura corregida del hotspot de NVIDIA RTX 5090

## Corrección respecto a HWMonitor 1.65

HWMonitor 1.65.1 cambió de forma sustancial la interpretación publicada dos días antes en 1.65:

- mantiene `BAR0 + 0x00AD0A90` como Hot Spot principal;
- elimina la lectura de `BAR0 + 0x00AD0AE0` como supuesto `HotSpot #2`;
- lee seis canales contiguos entre `0x00AD0A90` y `0x00AD0AA4`;
- considera válida una muestra únicamente cuando está activo el bit 30;
- convierte una muestra válida mediante `(raw & 0xFFFF) / 256.0` grados Celsius.

La fórmula de 1.65 basada en dos bytes y escala `1/32` queda descartada. Las
secciones históricas siguientes documentan cómo se detectó el comportamiento
de 1.65, pero no deben emplearse para implementar el consumidor.

Resultados obtenidos en la RTX 5090 de este equipo mediante comparación estática de HWMonitor 1.64/1.65 y trazado dinámico con WinDbg.

## Resultado histórico de 1.65 (obsoleto)

HWMonitor 1.65 no usa `nvapi_Direct_GetMethod` para obtener el hotspot. La exportación se carga, pero no se invoca durante la enumeración ni actualización de sensores.

La clase interna identificada por HWMonitor como `NVIDIA I/O` atiende las consultas de temperatura en `HWMonitor_x64.exe+0xA88B0`:

- Sensor interno 2 (`Hot Spot`): lee el registro `0x00AD0A90`.
- Sensor interno 34 (`[HotSpot #2]`): lee el registro `0x00AD0AE0`.

El sensor interno 29 lee `0x00AD0A94`, pero su etiqueta es `[TMP #9]`; no es el segundo hotspot.

La lectura se realiza por la función interna `HWMonitor_x64.exe+0x93C08`, que termina llamando a la infraestructura de acceso de bajo nivel de CPUID. No es una llamada pública de NVAPI o NVML.

## Conversión empleada por 1.65 (obsoleta)

Para ambos registros, HWMonitor interpreta:

```text
temperatura = byte_alto + byte_bajo * 0.03125
            = byte_alto + byte_bajo / 32
```

Rechaza la muestra cuando el byte alto vale `0x00` o `0xFF`.

## Evidencias históricas de 1.65

Las constantes little-endian correspondientes a `0x00AD0A90` y `0x00AD0AE0`:

- no aparecen en HWMonitor 1.64;
- aparecen una vez cada una en HWMonitor 1.65.

El proveedor `NVIDIA I/O` fue el que devolvió el hotspot principal durante el trazado de este equipo: aproximadamente 40-41 °C para el sensor 2. El desensamblado enlaza inequívocamente el sensor 34, cuya etiqueta es `[HotSpot #2]`, con el registro `0x00AD0AE0`.

## Consecuencia para FanControl

Un plugin ordinario de FanControl se ejecuta en modo usuario y no puede leer directamente registros MMIO de la GPU. Para reproducir esta técnica necesita una de estas vías:

1. una API soportada de NVIDIA que exponga los mismos datos;
2. el SDK de CPUID, si expone expresamente estos sensores;
3. un controlador de kernel fiable y firmado que permita una lectura MMIO estrictamente limitada y de solo lectura.

La tercera vía es técnicamente posible, pero no conviene implementar un controlador nuevo antes de comprobar si la infraestructura de bajo nivel que ya utiliza FanControl/LibreHardwareMonitor permite realizar esa lectura de forma segura.

## Comprobación de FanControl y PawnIO

La instalación local de FanControl incluye `LibreHardwareMonitorLib.dll` 0.9.6 y utiliza PawnIO en lugar de la antigua clase genérica `Ring0`. La DLL contiene módulos especializados que pueden hacer MMIO, pero no expone una lectura arbitraria de memoria física.

PawnIO permite implementar el lector como un módulo limitado que:

1. localice únicamente una GPU NVIDIA mediante su identificador PCI;
2. obtenga y valide su BAR0;
3. mapee sólo el intervalo mínimo necesario;
4. exponga únicamente los seis DWORD constantes entre `0x00AD0A90` y `0x00AD0AA4`;
5. no contenga ninguna operación de escritura.

El PawnIO originalmente instalado era la versión 2.0.1. Se actualizó a PawnIO Official 2.2.0 y se verificó que el nuevo controlador WHQL está activo desde DriverStore. FanControl volvió a iniciarse correctamente después de la actualización.

La edición oficial de PawnIO verifica la firma de cada módulo. Un módulo personal sin firma requeriría la edición `unrestricted` o que el módulo se incorporase al repositorio oficial y recibiese una firma oficial.

Secure Boot está activo en este equipo. Por ello no se instaló PawnIO `unrestricted`, cuyo controlador no está firmado. El módulo limitado `NvidiaGb202Thermal.p` y el lector de prueba `NvidiaThermalProbe` compilan correctamente, pero el módulo necesita una firma aceptada por PawnIO Official antes de poder ejecutar la primera lectura MMIO.

## Archivos de captura

- `windbg/provider-1.65.log`: identificación de los métodos virtuales de los proveedores.
- `windbg/provider-results-1.65.log`: resultados, valores crudos y método que los devuelve.
- `windbg/trace_direct.txt`: comandos de WinDbg usados para la captura.
