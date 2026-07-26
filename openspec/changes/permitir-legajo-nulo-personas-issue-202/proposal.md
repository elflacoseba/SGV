# Proposal: Permitir crear y editar Personas con legajo nulo

## Intent

Issue #202: que el sistema admita Personas sin `Legajo` en alta y edición, manteniendo la unicidad activa sólo cuando hay valor. Hoy el Dominio y validators ya lo permiten, pero la web exige `[Required]` y el wire-type es `string` no-nullable; el workaround `request.Legajo ?? string.Empty` en `SetupServicio.cs` evidencia la inconsistencia. Cambio: quitar obligatoriedad de UI, alinear wire, normalizar whitespace a null como Email/Teléfono y registrar auditoría explícita al limpiar un legajo persistido.

## Problema actual

- `PersonaInputModel.Legajo`: `[Required]` + `[StringLength(20)]` (backend acepta 50).
- `CrearPersonaRequest.Legajo` / `ActualizarPersonaRequest.Legajo`: `string` no-nullable.
- Sin contexto UI para advertir si un Puesto downstream exige legajo.
- Sin rastro auditable de quién y cuándo limpia un legajo existente.

## Scope

### In Scope

- `PersonaRequests.cs`: `string Legajo` → `string? Legajo` en ambos records.
- `PersonaInputModel.cs`: quitar `[Required]`, `[StringLength(20)]` → `[StringLength(50)]`, `string?`.
- `Create.cshtml.cs` y `Edit.cshtml.cs`: normalizar whitespace → null en POST; simplificar pre-carga GET.
- `SetupServicio.cs`: eliminar `?? string.Empty`.
- Auditoría explícita al limpiar un legajo persistido (`LegajoAnterior`, `LegajoNuevo=null`, `PersonaId`, `Usuario`) vía `IAuditoriaServicio.RegistrarAsync`.
- Advertencia UI contextual (no bloqueante) cuando un flujo downstream exija legajo.

### Out of Scope / Non-Goals

- No tocar Dominio, validators de Aplicación, repositorio, esquema DB ni migraciones.
- No introducir nueva familia de códigos de error. No relajar unicidad activa de legajos no nulos.
- No impactar el cambio activo `setup-admin-inicial-issue-195`.

## Capabilities

### New Capabilities
Ninguna.

### Modified Capabilities (delta spec en `sdd-spec`)

- `persona-management`: "Alta de Persona" cambia `Legajo MUST ser requerido` por `Legajo MAY omitirse`; agregar escenario de Persona sin legajo y requisito de auditoría explícita al limpiar.
- `web-apiclient-transport-contract`: escenario que verifica serialización de `Legajo=null` como `"legajo": null` sin romper el contrato.

## Approach (Enfoque A + auditoría explícita)

**Enfoque A (mínimo)** de la exploración + extensión de auditoría explícita. En `ActualizarAsync`, detectar transición `Legajo` no-nulo → null y emitir `IAuditoriaServicio.RegistrarAsync(entidad:"Persona", entityId, accion:"UpdateLegajo", valoresAnteriores:{"LegajoAnterior":…}, valoresNuevos:{"LegajoNuevo":null})`. UI: `<span class="text-warning">` bajo el campo cuando el flujo destino lo exija (preparación, no gate). Wire-type a `string?`; call-sites usan named args y compilan sin cambios.

## Affected Areas

| Área | Impacto | Descripción |
|------|---------|-------------|
| `src/SGV.Contracts/Personas/Comandos/PersonaRequests.cs` | Modificado | `Legajo` → `string?` |
| `src/SGV.Web/Integration/Personas/PersonaInputModel.cs` | Modificado | Sin `[Required]`, longitud 50, `string?` |
| `src/SGV.Web/Pages/Personas/Create.cshtml.cs` | Modificado | Normalización whitespace → null |
| `src/SGV.Web/Pages/Personas/Edit.cshtml.cs` | Modificado | Normalización + auditoría explícita |
| `src/SGV.Infraestructura/Setup/SetupServicio.cs` | Modificado | Sin `?? string.Empty` |
| `src/SGV.Aplicacion/Personas/Comandos/PersonaServicioComandos.cs` | Modificado | Emitir `IAuditoriaServicio.RegistrarAsync` al limpiar |
| Tests (Aplicación, Web, API) | Modificado | Casos legajo null, seam cliente HTTP, regresión unicidad |

## Tradeoffs

- **A vs. B**: A elimina el workaround y unifica tipos; B deja UI desalineada.
- **Auditoría explícita vs. interceptor EF**: la explícita garantiza los nombres canónicos `LegajoAnterior/LegajoNuevo`; el interceptor ya registra cambios genéricos, pero sin esos nombres.
- **Warning UI vs. bloqueo backend**: producto prioriza flexibilidad operativa; futuros módulos podrán endurecer.

## Risks

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| Source-breaking en call-sites posicionales de `CrearPersonaRequest` | Baja | Revisión previa de ~26 call-sites; mayoría usa named args |
| `ExistsActiveLegajoAsync(legajo="")` matchea legajos vacíos | Baja | Guarda `!string.IsNullOrEmpty` en `CheckUniquenessAsync`; test de regresión |
| Serialización JSON divergente (omitido vs null vs `""`) | Media | Normalización única en PageModel; test seam |
| Auditoría no emitida si la limpieza pasa por Reactivate u otro flujo | Baja | Centralizar en `PersonaServicioComandos.ActualizarAsync`, no en PageModel |
| Warning UI se vuelve ruido si la mayoría de Puestos lo exigen | Media | Diseñarlo contextual, sólo si el módulo downstream lo demanda |

## Rollback Plan

Revertir los seis archivos modificados; no hay migración de esquema ni datos. La pre-carga de Edit vuelve a `?? string.Empty` y los wire-types a `string` no-nullable. El registro contextual de auditoría deja de emitirse sin afectar el interceptor central.

## Dependencies

Sin nuevos paquetes NuGet. `IAuditoriaServicio` ya inyectado en `PersonaServicioComandos` vía la composición existente.

## Acceptance & Success Criteria

1. `/personas/crear` con `Legajo` vacío/whitespace → `201` + redirect a `Details`.
2. `/personas/editar/{id}` limpiando `Legajo` → `200` + fila en `Auditorias` con `Accion="UpdateLegajo"`, `LegajoAnterior`, `LegajoNuevo=null`, `PersonaId`, `Usuario`.
3. Crear con legajo explícito sigue funcionando; legajo duplicado no nulo sigue rechazado con `409`.
4. Wire emite `"legajo": null` cuando el PageModel envía null; backend persiste `NULL`.
5. Warning UI sólo aparece cuando el contexto downstream lo demande; nunca bloquea submit.
6. `Auth/Setup` (no afectado) sigue aceptando `Legajo?` opcional.
7. `dotnet build SGV.slnx` y `dotnet test SGV.slnx` en verde.

## Test Plan (alto nivel)

- **Unit (Aplicación)**: `PersonaServicioComandosTests` — `CrearAsync_LegajoNull_PermitidoYGuarda`, `ActualizarAsync_LimpiarLegajo_RegistraAuditoria`, `ActualizarAsync_LegajoDuplicado_SigueRechazando`.
- **Unit (Web)**: `PersonaApiClientBasicTests` — seam con `Legajo=null` y `Legajo=""`, assert de la forma serializada.
- **Integración (API)**: `PersonasApiTests` — `POST /api/v1/personas` con `legajo` omitido retorna `201`; `PUT` que limpia legajo retorna `200` y deja fila en `Auditorias`.
- **Integración (MySQL)**: `[MySqlFact]` persiste Persona con `Legajo=NULL` y verifica lectura.
- **UI smoke**: render de Edit con `Legajo=null` no muestra warning; con contexto downstream exigente sí lo muestra.
