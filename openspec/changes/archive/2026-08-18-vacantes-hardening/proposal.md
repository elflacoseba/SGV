# Proposal: 2026-08-18-vacantes-hardening

**Fecha**: 2026-08-18
**Change name**: `vacantes-hardening`
**Tipo**: Hardening / Cleanup
**Modo SDD**: Interactive (preguntas respondidas en ronda interactiva)

---

## Contexto

El módulo de Vacantes presenta drift técnico acumulado tras varios cambios archivados. Tres problemas críticos reducen la confiabilidad del sistema: (1) `HistorialEstadoVacante.ChangedByUserId` se persiste como `null` en todas las transiciones de estado, dejando la auditoría de estado huérfana de trazabilidad; (2) `ActualizarObservacionesAsync` existe en la capa de aplicación pero no tieneendpoint HTTP ni cliente tipado, siendo dead code testeado pero unreachable; (3) la superficie wire de `VacanteErrorCodigo.MotivoObligatorio` nunca fue referenciada. En paralelo, cinco hallazgos recomendados generan fricción de UX y debt de test.

El cambio no modifica comportamiento funcional de Vacantes. Espuramente, se encontró que `CambiarEstadoAsync` en `OcupacionServicioComandos` también pasa `usuarioId: null` — misma root cause que el ticket #2.

---

## Intención

Endurecer el módulo Vacantes eliminando surface huérfana, corrigiendo la trazabilidad de usuario en transiciones de estado, quitando dead code, y cerrando gaps de test y UX — sin cambiar el comportamiento funcional existente.

---

## Alcance

### En Scope

**Cluster 1 — Identidad de usuario en transiciones (Critical #2)**
- Resolver `usuarioId` desde `IHttpContextAccessor` en el composition root de `SGV.Api`
- Inyectar abstracción en `VacanteServicioComandos` y `OcupacionServicioComandos`
- Eliminar el `null` hardcodeado en los dos call sites de `CambiarEstado`

**Cluster 2 — Remover superficie huérfana (Critical #3)**
- Quitar `ActualizarObservacionesAsync` de `IVacanteServicioComandos`
- Quitar la implementación de `VacanteServicioComandos`
- Quitar tests asociados en `VacanteServicioComandosTests`
- Auditar que ningún otro módulo la referencia

**Cluster 3 — Modelo de entrada separado (Recommended #7)**
- Separar `VacanteInputModel` en `VacanteCreateInputModel` y `VacanteEditInputModel`
- Eliminar el `ModelState.Remove("Input.EstadoVacanteId")` workaround en `Create.cshtml.cs`
- Actualizar `Create.cshtml` y `Edit.cshtml` para bindear sus tipos respectivos

**Cluster 4 — Test de concurrencia Cubrir (Recommended #5)**
- Escribir `[MySqlFact]` cubriendo race `ExistsActiveByVacanteAsync` (TOCTOU)
- Escribir `[MySqlFact]` cubriendo race de doble cobertura atómica (constraint única)

**Cluster 5 — Constante dead code (Recommended #8)**
- Eliminar `VacanteErrorCodigo.MotivoObligatorio` de `SGV.Contracts`

**Triviales aisladas**
- **#1**: Reemplazar literals `"Administrador"`/`"GestorVacantes"` en `Index.cshtml.cs` por `RolesSgv.Administrador` / `RolesSgv.GestorVacantes`
- **#4**: Agregar guard en `Edit.OnGetAsync` que redirija a Details cuando `current.EsCerrada`
- **#6**: Pre-popular `Input.FechaApertura = DateTime.Today` en `Create.OnGetAsync`

---

## Decisiones Arquitectónicas

### D-1 — Propagación de identidad de usuario

**Decisión**: Resolver el `usuarioId` via `IHttpContextAccessor` inyectado en el composition root de `SGV.Api`, no via el DTO `CambiarEstadoVacanteRequest`.

**Alternativa rechazada**: Transportar `UsuarioId` en el DTO `CambiarEstadoVacanteRequest`. El controller extrae del `HttpContext.User` y lo pone en el request; el servicio lo recibe como parámetro.

**Rationale**: Mantiene los request DTOs wire-stable (sin agregar campos de infraestructura). El servicio es más testeable sin dependencia del HTTP context. La decisión del composition root es inyectar una abstracción (`IUsuarioActual` o directamente `IHttpContextAccessor`) y resolver ahí.的一致; la signatura de `CambiarEstadoVacanteRequest` no cambia.

---

### D-2 — `ActualizarObservacionesAsync`: eliminación

**Decisión**: Eliminar completamente el método de `IVacanteServicioComandos`, su implementación y sus tests.

**Alternativa rechazada A**: Exponer un endpoint `PATCH /{id}/observaciones` y agregarlo a `IVacanteApiClient`. Crearía semántica de idempotencia que no existe y desdibuja la responsabilidad — las observaciones ya viajan como side-effect de `CambiarEstadoAsync`.

**Alternativa rechazada B**: Mantener como huérfano documentado. Genera confusión para futuros desarrolladores y mantenimiento innecesario.

**Rationale**: La UI guarda observaciones exclusivamente via `CambiarEstadoAsync`. No hay roadmap conocido para edición independiente. Eliminar reduce superficie y elimina la posibilidad de drift semántico.

---

### D-3 — Split de `VacanteInputModel`

**Decisión**: Crear `VacanteCreateInputModel` (sin `EstadoVacanteId`) y `VacanteEditInputModel` (con `EstadoVacanteId`).

**Alternativa rechazada**: Mantener el workaround `ModelState.Remove("Input.EstadoVacanteId")` en `Create.cshtml.cs`.

**Rationale**: El workaround existe porque `EstadoVacanteId` tiene `[Required]` necesario para Edit pero innecesario para Create. Splitear modelos elimina el hack y hace que cada formulario tenga exactamente los campos que necesita.

---

### D-4 — Test de concurrencia Cubrir: alcance dual

**Decisión**: Escribir DOS `[MySqlFact]`:
1. Race de `ExistsActiveByVacanteAsync` contra otra cobertura concurrent
2. Race de doble cobertura atómica (la constraint única `IX_Ocupaciones_VacanteIdUnique` rechaza la segunda)

**Rationale**: `ExistsActiveByVacanteAsync` es la defensa TOCTOU en memoria; la constraint única es la defensa final en DB. Ambos paths deben ejercitarse para demostrar que el sistema se comporta correctamente bajo carrera.

---

### D-5 — Eliminar `VacanteErrorCodigo.MotivoObligatorio`

**Decisión**: Quitar la constante del layer público `SGV.Contracts`.

**Rationale**: Fue declarado pero nunca referenciado en src/ ni tests/. El spec de `vacante-management` refleja que `Motivo` es opcional al cerrar (PB-3). Confirmado que no hay consumidores externos. Es dead code.

---

## Approach

### Cluster 1 (Identidad + Cluster 2 Eliminación)
- **Capa Api**: crear abstracción `IUsuarioActual` (o wrapper de `IHttpContextAccessor`). Registrar en DI composition root.
- **Capa Aplicacion**: inyectar `IUsuarioActual` en `VacanteServicioComandos` y `OcupacionServicioComandos`. Reemplazar `usuarioId: null` con `await _usuarioActual.GetUserIdAsync()`.
- **Contracts**: NO modificar `CambiarEstadoVacanteRequest`.
- **Tests**: actualizar los tests existentes que mockean el nuevo servicio.

### Cluster 3 (Split modelo)
- **Contracts**: crear `VacanteCreateInputModel` y `VacanteEditInputModel` en `SGV.Contracts`.
- **Web**: actualizar `Create.cshtml.cs`, `Create.cshtml`, `Edit.cshtml.cs`, `Edit.cshtml`.

### Cluster 4 (Test concurrencia)
- **Tests**: nuevo archivo `VacantesCubrirConcurrencyTests.cs` junto a `VacantesConcurrenciaTests.cs`.

### Cluster 5 (Dead code)
- **Contracts**: eliminar `MotivoObligatorio` de `VacanteErrorCodigo.cs`.

### Triviales
- **Web**: cambios localizados en `Index.cshtml.cs`, `Edit.cshtml.cs`, `Create.cshtml.cs`.

---

## No-goals

- NO se introduce JWT claim changes ni se modifica el pipeline de autenticación.
- NO se cambia el interceptor de auditoría existente (`AuditoriaSaveChangesInterceptor`).
- NO se agregan nuevos endpoints HTTP más allá de los motivados por los hallazgos.
- NO se modifican migraciones de base de datos.
- NO se cambia el comportamiento funcional de `CambiarEstadoAsync` (la signatura de dominio no cambia, solo el `usuarioId` resolvedor).
- NO se introduce paginación en `ListarPuestosDisponiblesAsync`.
- NO se modifica la tabla `HistorialEstadoVacante` ni su esquema.

---

## Cambios Breaking

- **Contrato wire**: `CambiarEstadoVacanteRequest` NO cambia (decisión D-1). No hay breaking en el wire.
- **Contrato de tests**: los tests de `VacanteServicioComandos` que usan `ActualizarObservacionesAsync` se eliminan — si algún test de integración llama este método via mock, se debe actualizar.
- **Contrato `IVacanteServicioComandos`**: se elimina `ActualizarObservacionesAsync`. Si hubiera un mock en algún test existente, debe removerse.
- **Contrato `VacanteErrorCodigo`**: se elimina `MotivoObligatorio`. Si hubiera código legacy cacheado que lo reference, queda invalidado — impacto bajo (nunca se usó).
- **Impacto cruzado con Ocupaciones**: `OcupacionServicioComandos` también corrigeparámetro `usuarioId` en su call a `CambiarEstado` — el flujo Cubrir ahora persiste el usuario correctamente.

---

## Riesgos

| Riesgo | Probabilidad | Mitigación |
|--------|-------------|------------|
| `IUsuarioActual` resuelve `null` en background jobs o contextos sin HTTP | Baja | El composición root de API solo; jobs no involucrados en este change |
| Un caller oculto consume `ActualizarObservacionesAsync` no detectado en auditoría | Media | Auditoría previa con grep; si se encuentra, se来处理 |
| Tests de `VacanteServicioComandosTests` hacen mock de `ActualizarObservacionesAsync` y rompen tras eliminarse | Media | Buscar en la suite de tests antes de implementar; actualizar mocks |
| La constraint única `IX_Ocupaciones_VacanteIdUnique` no existe en todos los ambientes | Baja | Confirmar que la migración que la agregó ya fue aplicada |
| El split de `VacanteInputModel` rompe `ModelState.Remove` residual en `Edit.cshtml.cs` | Baja | Edit.cshtml.cs no tiene el workaround; es solo Create |

---

## Specs a crear/actualizar

**Delta specs** (en `openspec/changes/2026-08-18-vacantes-hardening/specs/`):

| Spec | Tipo | Tema |
|------|------|------|
| `vacante-identity-propagation` | New | D-1: trazabilidad de usuario en transiciones de estado |
| `vacante-remove-actualizar-observaciones` | New | D-2: eliminación de superficie huérfana |
| `vacante-input-model-split` | New | D-3: separación VacanteCreateInputModel / VacanteEditInputModel |
| `vacante-cubrir-concurrency-test` | New | D-4: tests de carrera para Cubrir |
| `vacante-error-codigo-cleanup` | New | D-5: eliminación MotivoObligatorio |

**Specs existentes a sincronizar** (delta entries):

| Spec | Cambio |
|------|--------|
| `vacante-management/spec.md` | Agregar PB-? sobre trazabilidad de usuario en transiciones; actualizar escenario de carrera para incluir Cubrir |
| `vacante-web/spec.md` | Agregar guard Edit terminal; pre-poblamiento FechaApertura; split de input models |

---

## Métricas de éxito

- [ ] Build `dotnet build SGV.slnx` pasa sin errores ni warnings nuevos
- [ ] Suite `dotnet test SGV.slnx` pasa 100% (baseline: 3364 tests)
- [ ] `grep -r "MotivoObligatorio" src/` retorna 0 resultados
- [ ] `grep -r "ActualizarObservacionesAsync" src/` retorna 0 resultados
- [ ] `grep -r "IsInRole(\"Administrador\")" src/SGV.Web` retorna 0 resultados (solo constantes `RolesSgv`)
- [ ] `grep -r "ModelState.Remove" src/SGV.Web` retorna 0 resultados
- [ ] `grep -r "usuarioId: null" src/` retorna 0 resultados en los archivos de comandos
- [ ] El `[MySqlFact]` nuevo de Cubrir corre y pasa contra MySQL real
- [ ] `Create.cshtml.cs.OnGet` setea `Input.FechaApertura = DateTime.Today`
- [ ] `Edit.cshtml.cs.OnGet` redirige a Details cuando `current.EsCerrada`

---

## Referencias

- Exploración: `openspec/changes/2026-08-18-vacantes-hardening/exploration.md`
- Decisiones vigentes: `docs/decisiones-implementacion.md`
- Roles constant: `src/SGV.Contracts/Constantes/RolesSgv.cs`
- Spec `vacante-management`: `openspec/specs/vacante-management/spec.md`
- Spec `vacante-web`: `openspec/specs/vacante-web/spec.md`
- Spec `auditoria-query`: `openspec/specs/auditoria-query/spec.md`
- Archivo `exploration.md` §Dependencias para clustering sugerido en 3 fases
