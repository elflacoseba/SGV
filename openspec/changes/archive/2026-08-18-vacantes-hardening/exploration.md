# Exploration: Vacantes Hardening

## TL;DR

- **En scope**: 8 issues mapping (3 critical, 5 recommended) que abarcan rol constants, auditoría de historial de estados, superficie HTTP huérfana, guard UI en Edit, test de concurrencia faltante, pre-populate de fecha, workaround de ModelState y dead code en `VacanteErrorCodigo`.
- **Out of scope**: Cualquier cambio de comportamiento funcional de Vacantes, Migraciones de BD, nuevos endpoints de API que no estén directamente motivados por los findings.
- **Capas afectadas**: Web (`Index.cshtml.cs`, `Create.cshtml.cs`, `Edit.cshtml.cs`), Aplicacion (`VacanteServicioComandos`, `OcupacionServicioComandos`), Contracts (`VacanteErrorCodigo`, `IVacanteServicioComandos`), Tests.
- **Hallazgo adicional espontáneo**: `CambiarEstadoAsync` en `VacanteServicioComandos` también pasa `usuarioId: null` (misma root cause que el ticket #2 del prior review) — alineado con la misma investigación.

---

## Hallazgos

### 🔴 Crítico #1 — Constantes de rol en Index.cshtml.cs

| Atributo | Detalle |
|---|---|
| **Severidad** | Critical |
| **Archivo** | `src/SGV.Web/Pages/Organizacion/Vacantes/Index.cshtml.cs:48` |
| **Línea exacta** | `public bool CanMutate => User.IsInRole("Administrador") \|\| User.IsInRole("GestorVacantes");` |
| **Código circundante** | Literal strings `"Administrador"` y `"GestorVacantes"` en lugar de `RolesSgv.Administrador` / `RolesSgv.GestrorVacantes` |
| ** blast radius** | Uso de `CanMutate` en `Index.cshtml` para ocultar botones Editar/Crear. Solo afecta esta línea; las otras tres páginas (Details, Create, Edit) ya usan `RolesSgv.*`. |
| **Arquitectura** | Capa Web; no cruza Dom/Apl/Infra. |
| **Spec coverage** | `vacante-web/spec.md` PB-1 menciona los roles pero no impone uso de constantes. |
| **Migración** | Ninguna. Solo string literal → constant. Cambio backward-compatible. |
| **Dependencias** | Aislado; no bloquea ni es bloqueado por otros hallazgos. |
| **Complejidad** | Trivial — una línea. |

---

### 🔴 Crítico #2 — `usuarioId: null` en `HistorialEstadoVacante` (2 call sites)

| Atributo | Detalle |
|---|---|
| **Severidad** | Critical |
| **Archivos** | `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs:351` y `src/SGV.Aplicacion/Ocupaciones/Comandos/OcupacionServicioComandos.cs:357` |
| **Líneas exactas** | `VacanteServicioComandos.cs:349-353`: `vacante.CambiarEstado(..., usuarioId: null, ...)` · `OcupacionServicioComandos.cs:355-359`: `vacante.CambiarEstado(..., usuarioId: null, ...)` |
| ** blast radius** | `VacanteServicioComandos.CambiarEstadoAsync` (12+ tests dependendientes). `OcupacionServicioComandos.CrearOcupacionCubriendoVacanteAsync` (flujo Cubrir). `HistorialEstadoVacante.ChangedByUserId` siempre será null en ambas flows. El endpoint `PATCH /{id}/estado` de `VacantesController` recibe el request que incluye `usuarioId` del JWT — pero el servicio lo ignora y pasa `null`. El TODO ya está documentado en `OcupacionServicioComandos.cs:240-249`. |
| **Arquitectura** | Cruza Aplicacion → Dominio. El fix requiere propagar el user ID desde el HTTP context (Api layer) hasta la llamada a `CambiarEstado`. |
| **Spec coverage** | `auditoria-query/spec.md` cubre la tabla `Auditorias` genérica, pero NO `HistorialEstadoVacante`. `vacante-management/spec.md` no menciona trazabilidad de usuario en transiciones de estado. |
| **Migración** | Ninguna en BD. Requiere cambiar la signatura de `CambiarEstadoVacanteRequest` para transportar `usuarioId` (wire-type change) o resolverlo en el controller via `IHttpContextAccessor`. |
| **Dependencias** | Dos call sites separados pero con la misma root cause. El fix de `#2` puede abordarse independientemente del resto, pero afecta el mismo dominio que `#3` (ambos tocan `IVacanteServicioComandos`). |
| **Complejidad** | Media — requiere threading del user identity desde el controller hasta el servicio. Decisión de diseño: ¿via request DTO o via `IHttpContextAccessor` en composition root? |

---

### 🔴 Crítico #3 — `ActualizarObservacionesAsync` sin endpoint HTTP

| Atributo | Detalle |
|---|---|
| **Severidad** | Critical |
| **Archivo** | `src/SGV.Aplicacion/Vacantes/Comandos/IVacanteServicioComandos.cs:46-49` (interfaz) · `src/SGV.Aplicacion/Vacantes/Comandos/VacanteServicioComandos.cs:391-443` (impl) · `src/SGV.Api/Controllers/VacantesController.cs` (sin endpoint) |
| ** blast radius** | El método existe y está testeado (`VacanteServicioComandosTests`). No es consumido por ningún controller ni por `VacanteApiClient`. La UI guarda observaciones como side-effect de `CambiarEstadoAsync` (en `CambiarEstadoVacanteRequest.Observaciones`). Si se expone el endpoint, la UI podría llamar directo sin cambiar estado. Si se elimina, se debe verificar que no hay otro consumidor. |
| **Arquitectura** | Capa Aplicacion + Contratos. El fix requiere agregar un endpoint PATCH en `VacantesController` y opcionalmente un método en `IVacanteApiClient`/`VacanteApiClient`. |
| **Spec coverage** | `vacante-management/spec.md` no menciona `ActualizarObservaciones` como capability independiente. Solo aparece como parte del request de `CambiarEstado`. |
| **Migración** | Posible breaking change si se expone: hay que decidir semántica (si es idempotente, si reemplaza o mezcla observaciones). Si se opta por eliminar la superficie, es un wire-type change en Contracts. |
| **Dependencias** | Clustering natural con `#2` — ambos tocan la interfaz `IVacanteServicioComandos` y el dominio de `HistorialEstadoVacante`. La decisión sobre `#2` (propagar usuarioId) puede condicionar `#3`. |
| **Complejidad** | Baja para exponer; media si se quiere delete. Requiere decisión del usuario: ¿exponer endpoint dedicado o quitar la superficie huérfana? |

---

### 🟡 Recomendado #4 — Edit carga formulario para vacante terminal

| Atributo | Detalle |
|---|---|
| **Severidad** | Recommended |
| **Archivo** | `src/SGV.Web/Pages/Organizacion/Vacantes/Edit.cshtml.cs` · `src/SGV.Web/Integration/Vacantes/VacanteDetailViewModel.cs:23` (`EsCerrada` property disponible) |
| **Línea** | `OnGetAsync` (líneas 51-67) carga la vacante y `PopulateInput` sin verificar `current.EsCerrada`. El backend rechaza con `409 EstadoTerminalInmutable` tras el round-trip. |
| ** blast radius** | UX: el usuario ve el formulario de Edit completo antes de intentar guardar, recibe error post-round-trip. Afecta solo `Edit.cshtml.cs`. `EsCerrada` ya existe en `VacanteDetailViewModel`. |
| **Arquitectura** | Capa Web. No cruza API. |
| **Spec coverage** | `vacante-web/spec.md` no menciona guard contra estados terminales en Edit. |
| **Migración** | Ninguna. Add < 5 líneas en `OnGetAsync` para redirect a Details. |
| **Dependencias** | Aislado. No comparte código con otros hallazgos. |
| **Complejidad** | Trivial — una condición de redirect. |

---

### 🟡 Recomendado #5 — Falta test de concurrencia para el flujo Cubrir

| Atributo | Detalle |
|---|---|
| **Severidad** | Recommended |
| **Archivo** | `tests/SGV.Tests/Api/Vacantes/VacantesConcurrenciaTests.cs` (existente) · `src/SGV.Aplicacion/Ocupaciones/Consultas/IOcupacionRepository.cs:67` (`ExistsActiveByVacanteAsync`) |
| **Línea** | `OcupacionServicioComandos.cs:278-285`: `ExistsActiveByVacanteAsync` es la defensa TOCTOU, pero no tiene `[MySqlFact]` que ejercite la carrera. |
| ** blast radius** | El test existente cubre `Crear` vacante (índice único `IX_Vacantes_ActivePuestoIdUnique`). Falta ejercitar `ExistsActiveByVacanteAsync` racing against another concurrent cover attempt for the same `VacanteId`. Si dos请求 concurrentes intentan cubrir la misma vacante, la DB constraint sobre `IX_Ocupaciones_VacanteIdUnique` debería rechazar la segunda, pero no hay `[MySqlFact]` que lo demuestre. |
| **Arquitectura** | Capa de Tests. No afecta producción. |
| **Spec coverage** | `vacante-management/spec.md` escenario "Carrera concurrente" solo cubre Crear Vacante, no Cubrir. |
| **Migración** | Ninguna. |
| **Dependencias** | Aislado. Puede implementarse independientemente. |
| **Complejidad** | Baja — nuevo `[MySqlFact]` análogo al patrón existente. |

---

### 🟡 Recomendado #6 — `Create.cshtml` no pre-popula `FechaApertura` con hoy

| Atributo | Detalle |
|---|---|
| **Severidad** | Recommended |
| **Archivo** | `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs:75-96` · `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml:66-70` |
| **Línea** | `OnGetAsync` no setea `Input.FechaApertura`. El `<input type="date">` en `Create.cshtml:68` se renderiza vacío. |
| ** blast radius** | Afecta solo la UX del formulario Create. El valor `DateTime?` nulo pasa la validación `[Required]` solo si el usuario no lo llena — genera friction en el primer uso. |
| **Arquitectura** | Capa Web. |
| **Spec coverage** | `vacante-web/spec.md` no menciona pre-poblamiento de `FechaApertura`. |
| **Migración** | Ninguna. Add `Input.FechaApertura = DateTime.Today;` en `OnGetAsync`. |
| **Dependencias** | Aislado. |
| **Complejidad** | Trivial — una línea en `OnGetAsync`. |

---

### 🟡 Recomendado #7 — `ModelState.Remove` workaround en Create.cshtml.cs

| Atributo | Detalle |
|---|---|
| **Severidad** | Recommended |
| **Archivo** | `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs:118` · `src/SGV.Web/Integration/Vacantes/VacanteInputModel.cs:16-18` |
| **Línea** | `Create.cshtml.cs:118`: `ModelState.Remove("Input.EstadoVacanteId");` · `VacanteInputModel.cs:16-18`: `[Required] public Guid? EstadoVacanteId { get; set; }` |
| ** blast radius** | El workaround existe porque `VacanteInputModel` conserva `[Required]` sobre `EstadoVacanteId` (necesario para `Edit`, donde el usuario debe elegir un estado). El campo ya no se envía en el formulario Create (#273 Slice A). El `ModelState.Remove` evita que `[Required]` valide un campo que no existe en el POST. Si se introduce un nuevo campo compartido entre Create y Edit con validación diferente, habría que repetir el workaround. |
| **Arquitectura** | Capa Web/Contracts. Afecta el contrato compartido `VacanteInputModel`. |
| **Spec coverage** | Ninguna spec cubre este detalle de implementación. |
| **Migración** | Ninguna. Alternativas: (a) dejar el workaround (status quo), (b) crear `VacanteCreateInputModel` sin `[Required]` en `EstadoVacanteId`, (c) usar `[BindNever]` condicional. Todas son backward-compatible. |
| **Dependencias** | Comparte `VacanteInputModel` con `Edit`. Si #7 se resuelve refactorizando a un modelo separado, hay que verificar que `Edit` sigue funcionando. |
| **Complejidad** | Baja. La propuesta de diseño debe evaluar las 3 alternativas y recomendar. |

---

### 🟡 Recomendado #8 — `VacanteErrorCodigo.MotivoObligatorio` dead code

| Atributo | Detalle |
|---|---|
| **Severidad** | Recommended |
| **Archivo** | `src/SGV.Contracts/Vacantes/Comandos/VacanteErrorCodigo.cs:29` |
| **Línea** | `public const string MotivoObligatorio = nameof(MotivoObligatorio);` |
| ** blast radius** | Declarado pero nunca referenciado en src/ ni tests/. Confirmado por grep: solo existe la declaración. PB-3 del change original `vacante-management` establece que `Motivo` es opcional al cerrar, por lo que el código no se usa. Fue marcado como S-2 (Suggestion) en el archive del change original. |
| **Arquitectura** | Capa Contracts. |
| **Spec coverage** | `vacante-management/spec.md` no menciona este código. El spec refleja el comportamiento real (Motivo opcional). |
| **Migración** | Ninguna. Solo eliminar la constante y su documentación XML. Cambio backward-compatible (ningún consumidor la usa). |
| **Dependencias** | Aislado. |
| **Complejidad** | Trivial — una constante + XML doc. |

---

## Concerns Transversales

### User Identity Propagation (#2, #3)
El problema de `usuarioId: null` atraviesa las capas API → Aplicacion → Dominio. El fix requiere propagar el identity del HTTP context hasta el servicio de comandos. Dos approaches:

1. **Via DTO** — agregar `UsuarioId` (opcional) a `CambiarEstadoVacanteRequest` y pasarlo al servicio. El controller loextrae del `HttpContext.User`. Decisión de diseño en `sdd-design`.
2. **Via IHttpContextAccessor** — inyectar en `VacanteServicioComandos` y resolver el usuario en el composition root (Api). Más oculto pero menos invasivo en los request DTOs.

Approach 1 es más explícito y testeable; Approach 2 evita tocar contratos wire. La propuesta debe evaluar ambas.

### Decisión de arquitectura para #3 (ActualizarObservacionesAsync)
El servicio existe y está testeado pero no es reachable. La decisión requerida es:
- **Opción A**: Exponer `PATCH /{id}/observaciones` en `VacantesController` + agregar a `IVacanteApiClient`. Permite editar observaciones sin cambiar estado.
- **Opción B**: Eliminar la superficie huérfana de `IVacanteServicioComandos` y sus tests asociados. La UI ya escribe observaciones via `CambiarEstadoAsync`.
- **Opción C**: Mantener como está (huérfano, documentado como no usado).

Opción B es la más limpia si no hay roadmap para uso independiente. Opción A requiere decidir semántica de idempotencia.

### VacanteInputModel Coupling (#7)
El modelo compartido causa el `ModelState.Remove` workaround. Una posible mejora (no obligatorio) sería separar en `VacanteCreateInputModel` y `VacanteEditInputModel` si el equipo quiere eliminar el workaround.

---

## Observaciones fuera de scope (no parte de los 8 hallazgos)

1. **`ExistsActiveByVacanteAsync` en `OcupacionServicioComandos.cs:278`**: TOCTOU check racing against concurrent cover — mitigado por constraint única en DB (`IX_Ocupaciones_VacanteIdUnique`) pero el `[MySqlFact]` falta (#5). Fuera de scope porque #5 ya lo cubre.

2. **`LoadStatesAsync` en `Edit.cshtml.cs:190-206`**: filtra `!s.EsCubierta` para ocultar el estado Cubierta del dropdown de Edit — pero cuando la vacante YA ES terminal, el dropdown muestra solo estados no-terminales y la UI igualmente hace el round-trip que el backend rechaza. Esto es exactamente el finding #4.

3. **Sin paginación en `ListarPuestosDisponiblesAsync`**: `IVacanteApiClient.ListarPuestosDisponiblesAsync` devuelve `IReadOnlyList<PuestoDto>` sin paginar. Si el catálogo crece mucho, puede haber presión de memoria. No es issue concreta, solo observación.

4. **Test coverage de `ActualizarObservacionesAsync`**: existe en `VacanteServicioComandosTests` pero cubre solo el happy path y el dominio lanza `ArgumentException` si >500 chars. Los tests de validación de longitud no se verificaron en esta exploración.

---

## Preguntas Abiertas para la fase de Proposal

1. **User Identity (#2)**: ¿El approach preferido es propagar `usuarioId` via DTO (`CambiarEstadoVacanteRequest`) o via `IHttpContextAccessor` en el composition root? ¿Hay preferencia por mantener los request DTOs estables?

2. **ActualizarObservacionesAsync (#3)**: ¿Se debe exponer como endpoint independiente (Opción A) o eliminar la superficie huérfana (Opción B)? ¿Hay roadmap para editar observaciones sin cambiar estado?

3. **VacanteInputModel (#7)**: ¿Se desea eliminar el `ModelState.Remove` via split de modelos o se acepta el workaround como status quo?

4. **Alcance del test de concurrencia (#5)**: ¿El `[MySqlFact]` nuevo debe cubrir el race de `ExistsActiveByVacanteAsync` únicamente, o también el race de doble cobertura de la misma vacante desde distintos usuarios simultáneos?

5. **`MotivoObligatorio` (#8)**: ¿Confirmar que la eliminación de esta constante no afecta ningún consumidor externo (clientes API cacheados, etc.)?

---

## Referencias cruzadas a specs existentes

| Spec | Cobertura relevante |
|---|---|
| `vacante-management/spec.md` | PB-1 (roles), PB-3 (Motivo opcional), escenario de carrera concurrente (solo Crear, NO Cubrir) |
| `vacante-web/spec.md` | PB-1 (roles en UI), PB-2 (Create desde módulo Vacantes), PB-4 (HistorialEstadoVacante en Details) |
| `auditoria-query/spec.md` | Tabla `Auditorias` genérica; NO cubre `HistorialEstadoVacante` |

---

## Dependencias entre hallazgos

```
#2 (usuarioId null)  ──┬── #3 (ActualizarObservacionesAsync huérfano)
                       │     Ambos tocan IVacanteServicioComandos
                       │     y el dominio de HistorialEstadoVacante.
                       │
#7 (ModelState.Remove)─┬── #7 comparte VacanteInputModel con #4
#4 (Edit guard)        │   (Edit.cshtml.cs usa VacanteInputModel
                       │    y OnGetAsync no verifica EsCerrada)

#1 (rol constants)     ├── Aislado de todos los demás
#5 (concurrencia test) ├── Aislado de todos los demás
#6 (FechaApertura)     ├── Aislado de todos los demás
#8 (dead code)         ├── Aislado de todos los demás
```

**Agrupaciones naturales para implementarse en fases independientes**:
- **Fase 1 (aislados triviales)**: #1, #4, #6, #8
- **Fase 2 (UX + tests)**: #5, #7
- **Fase 3 (arquitectura identidad + superficie HTTP)**: #2, #3 (requiere decisión de diseño)
