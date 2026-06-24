## Why

El modelo actual de Ocupaciones fuerza una sola ocupación activa por Persona, lo que no representa correctamente escenarios educativos donde una misma Persona puede ocupar simultáneamente distintos Puestos. Además, `TipoAsignacion` hoy es texto libre y eso deja abierta una semántica inconsistente para un dato que ya tiene un conjunto inicial conocido.

## What Changes

- Permitir que una Persona mantenga múltiples Ocupaciones activas en paralelo siempre que correspondan a Puestos diferentes.
- Mantener la restricción de una sola Ocupación activa por combinación Persona + Puesto.
- Conservar la regla de una sola Ocupación activa por Puesto.
- Convertir `TipoAsignacion` de texto libre a un enum cerrado con los valores iniciales `Permanente`, `Interina` y `Temporal`.
- Ajustar dominio, persistencia, migraciones y pruebas para reflejar el nuevo modelo de concurrencia y el nuevo tipo controlado.

## Capabilities

### New Capabilities
- Ninguna.

### Modified Capabilities
- `sgv-database`: cambia las reglas del historial de Ocupaciones para permitir concurrencia por Persona en Puestos distintos y tipificar `TipoAsignacion` como valor controlado.

## Impact

- Dominio: `Ocupacion` y cualquier validación asociada al tipo de asignación.
- Persistencia EF Core/Pomelo: configuración, índices/columnas generadas y migración de base de datos para `Ocupaciones`.
- Pruebas: cobertura de invariantes de dominio y del modelo relacional MySQL para la nueva unicidad activa.
- Datos existentes: requiere migración compatible para transformar `TipoAsignacion` desde `varchar(50)` a representación persistible del enum.

## Non-goals

- No introducir jerarquía entre ocupaciones concurrentes (principal/secundaria).
- No agregar porcentaje de dedicación, carga horaria ni reglas de compatibilidad entre ocupaciones.
- No crear en este cambio un módulo API/aplicación completo para administrar Ocupaciones si no es necesario para soportar el ajuste del modelo.
