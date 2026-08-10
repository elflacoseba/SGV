# Proposal: invertir-flujo-cubrir

## Why

El change `vacante-ocupacion-flow-alignment` (archivado 2026-08-07) implementó N2 como "Cubrir Vacante desde Vacante crea la Ocupación derivada". El frontend dejó `PersonaId` requerido en `CambiarEstadoVacanteRequest`. **Pero el form de Edit de Vacante no expone `PersonaId`** y el dropdown ya excluye Cubierta (issue #268). Resultado: el Administrador **no puede Cubrir una Vacante desde el frontend** — el ciclo "Crear Vacante → Cubrir" no cierra.

El usuario confirmó que para el MVP sin Postulación el flujo debe invertirse: **"Cubrir Vacante"** es una acción que crea directamente una Ocupación (asignando Persona al Puesto), no una transición de estado de Vacante.

## What Changes

### Backend (Lógica de negocio)

- **`OcupacionServicioComandos.CrearAsync`**: cuando el request incluye `VacanteId` opcional, en la misma transacción:
  - Validar que la Vacante existe y está **Abierta o En Selección**.
  - Validar que la Vacante no tiene ya Ocupación vigente.
  - Crear la Ocupación con `VacanteId` setado, `PuestoId` del de la Vacante, `PersonaId` del request.
  - Transicionar la Vacante a **Cubierta** con `PersonaId` (N2 interno).
  - Crear el `HistorialEstadoVacante`.
  - Atomicidad: si la transición de Vacante falla, la Ocupación no se persiste.

- **`VacanteServicioComandos.CambiarEstadoAsync`**: cuando el destino sea Cubierta, devolver `400 Validation` con código `PersonaIdRequeridoParaCubrir` y mensaje **"Use el botón 'Cubrir Vacante' en el detalle de la Vacante para crear la Ocupación derivada."** Eliminar el bloque de creación de Ocupación (líneas 330-344).

### Wire contracts (SGV.Contracts)

- **`CrearOcupacionRequest`**: agregar `VacanteId` opcional (`Guid?`). Validación: si está setado, `PuestoId` puede omitirse y se resuelve desde la Vacante; si vienen ambos, deben coincidir.

- **`VacanteDetailDto`**: agregar `Guid? OcupacionDerivadaId` y `string? PersonaAsignadaNombre`. Hidratar en `MapToDetailDto` y en `GetByIdAsync` con join a `Ocupaciones.Where(o => o.EsVigente && o.VacanteId == vacante.Id)`.

- **`CambiarEstadoVacanteRequest`**: `PersonaId` queda deprecated en el record (no se borra, se ignora en el path Cubierta). Documentar en XML doc.

### Frontend (Razor Pages)

- **`Vacantes/Details.cshtml`**: botón **"Cubrir Vacante"** cuando la vacante está Abierta o En Selección (no Cubierta, no Cancelada). Navega a `/organizacion/ocupaciones/crear?vacanteId={id}&returnUrl=…`. Visible para `CanMutate`. Mostrar bloque "Persona asignada" + link a Ocupación cuando `EsCubierta && OcupacionDerivadaId.HasValue`.

- **`Ocupaciones/Create.cshtml` + `.cs`**: soporte para `?vacanteId={guid}`. Si viene: resolver `PuestoId` desde la Vacante (API call), bloquear dropdown Puesto, mostrar hint informativo. Validar que la Vacante no esté Cubierta/Cancelada antes de mostrar el form.

- **`_Form.cshtml`**: replicar lógica de `?puestoId` para `?vacanteId` (dropdown bloqueado).

- **`PuestoOcupaciones.cshtml.cs`** (línea ~147-150): renombrar botón "Nueva ocupación" → "Cubrir Vacante" cuando hay Vacante abierta sin Ocupación activa. Agregar `NewOcupacionButtonLabel` al `ViewModel`.

- **`_CrossList.cshtml`**: leer el nuevo label y renderizarlo en el botón.

### Tests

- **Unitarios**: `OcupacionServicioComandosTests` — `CrearAsync_ConVacanteId_*` (5 tests nuevos).
- **Unitarios**: `VacanteServicioComandosTests` — actualizar existente y agregar `CambiarEstado_A_Cubierta_SinPersonaId_DevuelveValidationConMensajeUseCubrirVacante`.
- **API**: `OcupacionesControllerTests` — `Create_ConVacanteId_*` (3 tests).
- **API**: `VacantesControllerTests` — `PatchEstado_A_Cubierta_Returns400WithUseCubrirVacanteMensaje`.
- **API**: `VacantesControllerDetailTests` — `GetById_*` retornando OcupacionDerivadaId y PersonaAsignadaNombre (2 tests).
- **Web**: `VacantesDetailsTests` — coverage del botón y persona asignada (4 tests).
- **Web**: `OcupacionCreateTests` — coverage de `?vacanteId` (3 tests).
- **Web**: `PuestoOcupacionesTests` — label del botón (2 tests).

## Scope

### In Scope
- Inversión del flujo Cubrir: botón en Details de Vacante → Create de Ocupación.
- Backend: creación de Ocupación con `VacanteId` en `OcupacionServicioComandos`.
- Backend: rechazo de Cubierta vía `CambiarEstadoVacanteRequest`.
- Wire contracts: `CrearOcupacionRequest.VacanteId`, `VacanteDetailDto` con campos de Ocupación derivada.
- Frontend: botón "Cubrir Vacante", soporte de `?vacanteId` en Create, label dinámico en PuestoOcupaciones.
- Tests unitarios, API y Web cubriendo todos los escenarios.
- Actualización de spec `vacante-management` (N2 deprecado), `web-ocupaciones-crear-editar` (REQ-OCC-FORM-009), `vacante-web` (nuevo botón).

### Out of Scope
- Módulo de Postulaciones (selección de candidatos) — work separado.
- Backfill de `VacanteId` en Ocupaciones históricas.
- Nuevas migraciones (la columna `VacanteId` ya existe nullable desde el change archivado).
- Cambios al modelo de Identity.
- BFF hardening.

## Capabilities

> Contract entre proposal y specs. El agente `sdd-spec` crea/actualiza los spec files listados.

### New Capabilities
Ninguna. El change no introduce nuevas capacidades — redistribuye comportamiento existente.

### Modified Capabilities
- **`vacante-management`** — El requisito `CambiarEstado` con regla N2 (Cubrir crea Ocupación automáticamente desde `CambiarEstadoVacanteRequest`) se **depreca** y se reemplaza por: la transición a Cubierta **se rechaza** con mensaje `PersonaIdRequeridoParaCubrir`. El nuevo comportamiento de Cubrir vive en `OcupacionServicioComandos.CrearAsync` con `VacanteId`. Requiere delta spec.
- **`web-ocupaciones-crear-editar`** — `REQ-OCC-FORM-009` se **extiende** para soportar `?vacanteId={guid}`: el hint informativo ahora incluye el código de la Vacante, el dropdown de Puesto se bloquea, y se valida que la Vacante no esté Cubierta/Cancelada. Requiere delta spec.
- **`vacante-web`** — Nuevo requisito: la página Details de Vacante debe mostrar el botón "Cubrir Vacante" para roles con permiso de mutación, y un bloque de "Persona asignada" cuando la Vacante está Cubierta. Requiere delta spec.
- **`web-ocupaciones-navegacion-contextual`** — El botón de navegación de PuestoOcupaciones cambia de label de "Nueva ocupación" a "Cubrir Vacante" cuando existe Vacante abierta sin Ocupación activa. Requiere delta spec.

## Approach

1. **Backend**: modificar `OcupacionServicioComandos.CrearAsync` para aceptar `VacanteId` opcional y, cuando está presente, ejecutar la validación de Vacante + transición a Cubierta en la misma transacción. Modificar `VacanteServicioComandos.CambiarEstadoAsync` para rechazar destino Cubierta con `400 Validation` y mensaje de redirect al botón.
2. **Wire**: agregar `VacanteId` a `CrearOcupacionRequest`; extender `VacanteDetailDto` con `OcupacionDerivadaId` y `PersonaAsignadaNombre`; deprecar `PersonaId` en `CambiarEstadoVacanteRequest`.
3. **Frontend**: agregar botón en `Vacantes/Details.cshtml`; soportar `?vacanteId` en `Ocupaciones/Create` con PuestoId bloqueado y hint; actualizar label del botón en `PuestoOcupaciones`.
4. **Tests**: cubrir todos los paths nuevos con tests unitarios (servicio), API (controller) y Web (Razor Pages).

## Affected Areas

| Área | Impact | Descripción |
|------|--------|-------------|
| `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs` | Modificado | Nuevo path `VacanteId` en `CrearAsync` |
| `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs` | Modificado | Elimina creación de Ocupación en Cubierta; agrega rechazo con mensaje |
| `src/SGV.Contracts/Ocupaciones/Comandos/CrearOcupacionRequest.cs` | Modificado | Agrega `VacanteId?` con validación |
| `src/SGV.Contracts/Vacantes/Consultas/Dtos/VacanteDetailDto.cs` | Modificado | Agrega `OcupacionDerivadaId?`, `PersonaAsignadaNombre?` |
| `src/SGV.Contracts/Vacantes/Comandos/CambiarEstadoVacanteRequest.cs` | Modificado | `PersonaId` deprecated |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Details.cshtml` | Modificado | Botón "Cubrir Vacante" + bloque persona asignada |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/Create.cshtml` | Modificado | Soporte `?vacanteId` con PuestoId bloqueado y hint |
| `src/SGV.Web/Pages/Organizacion/Ocupaciones/_Form.cshtml` | Modificado | Lógica de Puesto bloqueado para `?vacanteId` |
| `src/SGV.Web/Pages/Organizacion/Puestos/PuestoOcupaciones.cshtml.cs` | Modificado | `NewOcupacionButtonLabel` dinámico |
| `src/SGV.Web/Pages/Organizacion/Puestos/_CrossList.cshtml` | Modificado | Renderiza nuevo label |
| `tests/SGV.Tests/Aplicacion/Ocupaciones/OcupacionServicioComandosTests.cs` | Modificado | 5 tests nuevos |
| `tests/SGV.Tests/Aplicacion/Vacantes/VacanteServicioComandosTests.cs` | Modificado | 1 test actualizado + 1 nuevo |
| `tests/SGV.Tests/Api/OcupacionesControllerTests.cs` | Modificado | 3 tests nuevos |
| `tests/SGV.Tests/Api/VacantesControllerTests.cs` | Modificado | 1 test nuevo |
| `tests/SGV.Tests/Api/Vacantes/VacantesControllerDetailTests.cs` | Modificado | 2 tests nuevos |
| `tests/SGV.Tests/Web/Vacantes/VacantesDetailsTests.cs` | Nuevo | 4 tests |
| `tests/SGV.Tests/Web/Ocupaciones/OcupacionCreateTests.cs` | Nuevo | 3 tests |
| `tests/SGV.Tests/Web/Puesto/PuestoOcupacionesTests.cs` | Nuevo | 2 tests |

## Risks

| Riesgo | Likelihood | Mitigation |
|--------|------------|------------|
| Tests existentes de `CambiarEstado_A_Cubierta_ConPersonaId_CreaOcupacionYRegistraHistorial` rompen | Alta | Actualizar el test para verificar rechazo 400 + mensaje. El comportamiento se reescribe en `OcupacionServicioComandos`. |
| Join de `Ocupacion.Persona` con AspNetUsers — la Ocupación referencia entidad `Persona`, no `Identity` | Media | Verificar `OcupacionConfiguracion.cs` y `PersonaConfiguracion.cs` antes de escribir el mapping de `PersonaAsignadaNombre`. |
| Compatibilidad con clientes que envían `PersonaId` en `CambiarEstadoVacanteRequest` | Baja | El campo queda en el record como deprecated; la API rechaza el path Cubierta. Documentar breaking change en PR. |
| Tests `MySqlFact` con lógica transaccional nueva | Media | El patrón de atomicidad ya existe en el archivado. Extender los `[MySqlFact]` de `OcupacionServicioComandos`. |

## Rollback Plan

1. Revertir los cambios en `OcupacionServicioComandos.CrearAsync` (quitar el bloque `VacanteId`).
2. Restaurar el bloque de creación de Ocupación en `VacanteServicioComandos.CambiarEstadoAsync` (líneas 330-344).
3. Revertir los cambios de `SGV.Contracts`: quitar `VacanteId` de `CrearOcupacionRequest`; revertir `VacanteDetailDto`; quitar deprecation de `CambiarEstadoVacanteRequest.PersonaId`.
4. Revertir cambios de frontend: quitar botón, soporte `?vacanteId` y label dinámico.
5. Revertir tests: eliminar los tests nuevos y restaurar los modificados.
6. No requiere migración — la columna `VacanteId` ya existe.

## Dependencies

- El change archivado `vacante-ocupacion-flow-alignment` (2026-08-07) — provee la columna `Ocupaciones.VacanteId` nullable y el FK.
- La entidad `Persona` existe como dominio propio; no es `AspNetUsers`. Verificar navegación en `OcupacionConfiguracion.cs`.

## Success Criteria

- [ ] **AC1**: DADO una Vacante Abierta, CUANDO el admin hace click en "Cubrir Vacante" en el detalle, ENTONCES es redirigido a `/organizacion/ocupaciones/crear?vacanteId={id}` con el `PuestoId` precargado y bloqueado.
- [ ] **AC2**: DADO el admin en `/organizacion/ocupaciones/crear?vacanteId={id}`, CUANDO completa Persona + FechaInicio + TipoAsignación y envía, ENTONCES se crea la Ocupación y la Vacante pasa a Cubierta en la misma transacción.
- [ ] **AC3**: DADO AC2, CUANDO redirige a `/organizacion/vacantes/detalles/{id}`, ENTONCES la Vacante Cubierta muestra Persona asignada y link a la Ocupación derivada.
- [ ] **AC4**: DADO el dropdown de Edit de Vacante, CUANDO el admin lo abre, ENTONCES Cubierta NO aparece (vigente desde issue #268).
- [ ] **AC5**: DADO `PATCH /api/v1/vacantes/{id}/estado` con destino Cubierta, CUANDO se invoca, ENTONCES responde `400 Validation` con mensaje "Use el botón 'Cubrir Vacante' en el detalle de la Vacante para crear la Ocupación derivada."
- [ ] **AC6**: DADO la Vacante está Cubierta o Cancelada, CUANDO el admin entra al detalle, ENTONCES el botón "Cubrir Vacante" NO se muestra.
- [ ] **AC7**: DADO la Vacante ya está Cubierta, CUANDO el admin intenta `/organizacion/ocupaciones/crear?vacanteId={id}`, ENTONCES ve error legible "Esta Vacante ya está cubierta."
- [ ] **AC8**: DADO PuestoOcupaciones con Vacante abierta y sin Ocupación activa, CUANDO el admin entra, ENTONCES el botón dice "Cubrir Vacante" (no "Nueva ocupación").
- [ ] **AC9**: Suite de tests completa en verde.
- [ ] **AC10**: No se introducen migraciones.

## Open Questions

1. **¿El `IVacanteServicioConsulta.GetByIdAsync` ya carga Ocupaciones?** Si no, ¿conviene un método nuevo `GetByIdWithOcupacionAsync` o extender el existente? → Decisión del design phase.
2. **¿El campo `PersonaAsignadaNombre` requiere otro DTO anidado (`PersonaResumenDto`) o alcanza con un string?** → Decisión del design phase.
3. **¿Eliminamos el campo `PersonaId` del `CambiarEstadoVacanteRequest` o lo dejamos como deprecated?** → Sugerencia: dejar como deprecated con XML doc warning.
