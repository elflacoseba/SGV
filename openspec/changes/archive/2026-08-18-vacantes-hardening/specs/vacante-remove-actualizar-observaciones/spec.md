# Especificación: Eliminación de ActualizarObservacionesAsync

## Propósito

Eliminar la superficie huérfana `IVacanteServicioComandos.ActualizarObservacionesAsync` y su implementación. La UI ya actualiza observaciones como side-effect de `CambiarEstadoAsync` vía `CambiarEstadoVacanteRequest.Observaciones`; no existe endpoint HTTP ni cliente tipado que consuma este método. Decisión D-2.

## Requisitos

### Requisito: Interfaz no declara ActualizarObservacionesAsync

`IVacanteServicioComandos` (en `src/SGV.Aplicacion/Vacantes/Comandos/IVacanteServicioComandos.cs`) NO DEBE contener ninguna firma `ActualizarObservacionesAsync`.

#### Escenario: Ausencia del símbolo en la interfaz

- **DADO** el archivo `IVacanteServicioComandos.cs`
- **CUANDO** se compila el proyecto `SGV.Aplicacion`
- **ENTONCES** el símbolo `ActualizarObservacionesAsync` NO DEBE existir en la interfaz.

### Requisito: Implementación no contiene ActualizarObservacionesAsync

`VacanteServicioComandos` (en `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs`) NO DEBE definir ningún método `ActualizarObservacionesAsync`.

#### Escenario: Ausencia del símbolo en la implementación

- **DADO** el archivo `VacanteServicioComandos.cs`
- **CUANDO** `grep -rn "ActualizarObservacionesAsync" src/SGV.Aplicacion` se ejecuta
- **ENTONCES** el resultado DEBE ser vacío.

### Requisito: Ningún test referencia el método

Los tests bajo `tests/SGV.Tests` NO DEBEN contener referencias al símbolo `ActualizarObservacionesAsync`.

#### Escenario: Ausencia del símbolo en tests

- **DADO** los tests existentes de Vacantes
- **CUANDO** `grep -rn "ActualizarObservacionesAsync" tests/SGV.Tests` se ejecuta
- **ENTONCES** el resultado DEBE ser vacío.

### Requisito: Ningún source code fuera del módulo referencia el método

`grep -rn "ActualizarObservacionesAsync" src/` NO DEBE retornar coincidencias en ningún capa (Dominio, Aplicación, Infraestructura, Api, Web, Contracts).

#### Escenario: Ausencia global en src

- **DADO** todo el código de `src/`
- **CUANDO** se ejecuta `grep -rn "ActualizarObservacionesAsync" src/`
- **ENTONCES** el resultado DEBE ser 0.

### Requisito: Observaciones siguen actualizables vía CambiarEstadoAsync

El comportamiento existente — `CambiarEstadoVacanteRequest.Observaciones` actualiza las observaciones de la vacante como side-effect de la transición — DEBE preservarse intacto.

(Previously: comportamiento vigente — no se introduce ni se quita.)

#### Escenario: Cambiar estado actualiza observaciones

- **DADO** una vacante con observaciones actuales
- **CUANDO** se invoca `CambiarEstadoAsync` con `Observaciones = "nuevo texto"`
- **ENTONCES** la vacante persistida DEBE tener `Observaciones = "nuevo texto"`.
