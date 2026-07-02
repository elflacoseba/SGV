# Verify Report: cargos-filtro-activos-eliminados

## 1. Resumen ejecutivo

La re-validación independiente confirma que los tres hallazgos CRITICAL del verify anterior quedaron resueltos en código y en runtime: `sort` ahora viaja end-to-end y se aplica server-side antes de paginar, el banner de `LastDeletedId` volvió a funcionar en Activas y el placeholder vacío fue reemplazado por tests reales. El cambio compila, la suite focalizada nueva pasa y el build frontend también.

La suite completa sigue cerrando con **1132 passed / 12 failed**, pero los 12 fallos son los preexistentes de `OcupacionRepositoryTests` documentados en el issue #59, sin evidencia de regresión atribuible a este cambio. También confirmé al menos un `[MySqlFact]` clave de orden cross-page, así que la corrección de F-001 no quedó solo en tests de capa alta.

Queda un residuo menor de trazabilidad: `apply-progress.md` ya refleja la segunda pasada, pero su tabla de commits no incluye el commit documental `fa1ddc33`. No bloquea archive. Con el estado actual, mi decisión es **READY FOR ARCHIVE**.

## 2. Comparativa con verify anterior

### F-001 — `sort` server-side end-to-end
- **Antes**: ❌ CRITICAL.
- **Ahora**: ✅ Resuelto.
- **Evidencia**:
  - `src/SGV.Api/Controllers/CargosController.cs` → `GetConsulta(..., sort, ...)` propaga `sort` al `CargoListQuery`.
  - `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` → `BuildQueryUri(...)` serializa `sort`.
  - `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs` → `ApplySort(...)` corre antes de `Skip/Take`.
  - `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` → `LoadAsync()` ya no reordena localmente.
  - Tests runtime: `GetConsulta_PropagaSortAlServicio`, `QueryAsync_WithSort_SerializesSortInUri`, `QueryAsync_ConSortNombreDesc_OrdenaServidorAntesDePaginar`, `QueryAsync_MySql_SortNombreDesc_SeAplicaAntesDePaginar`.

### F-002 — REQ-CW-06 (`LastDeletedId` + CTA banner)
- **Antes**: ❌ CRITICAL.
- **Ahora**: ✅ Resuelto.
- **Evidencia**:
  - `IndexModel.LastDeletedId` es `Guid?`, se puebla desde `TempData` y `HasLastDeleted` deriva de `LastDeletedId.HasValue`.
  - `OnPostDeleteAsync()` redirige con `deletedId = id` tras éxito.
  - `OnPostReactivateAsync()` limpia `TempData[nameof(LastDeletedId)]` con `ClearLastDeleted()`.
  - `Index.cshtml` renderiza el CTA solo cuando `Model.HasLastDeleted && !Model.IsDeletedView`.
  - Tests runtime: `Post_Delete_AlmacenaLastDeletedId_PermiteReactivarEnBanner`, `Post_Delete_CuandoSegmentoEsEliminadas_NoMuestraCtaReactivar`, `Post_Reactivate_Exito_LimpiaLastDeletedId_BannerDesaparece`.

### F-003 — placeholder test sin asserts
- **Antes**: ❌ CRITICAL.
- **Ahora**: ✅ Resuelto.
- **Evidencia**:
  - `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs:412+` ya no contiene `await Task.CompletedTask;` como placeholder.
  - El test ahora ejecuta flujo real `POST Delete -> PRG -> GET Index` y aserta CTA/banner/contexto.

### F-006 — test cross-page sort
- **Antes**: 💡 SUGGESTION.
- **Ahora**: ✅ Implementado.
- **Evidencia**:
  - `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` → `QueryAsync_MySql_SortNombreDesc_SeAplicaAntesDePaginar`.

### F-004 y F-005 del verify anterior
- **F-004 (`apply-progress` desalineado)**: ⚠️ Mejora parcial. El artefacto ya incluye la segunda pasada, pero la tabla de commits todavía no lista `fa1ddc33`.
- **F-005 (cobertura/ramas flojas en flujos delicados)**: ✅ deja de ser hallazgo de gate. La segunda pasada agregó runtime tests justamente sobre los caminos que estaban ciegos (`sort` + banner/TempData).

## 3. Comandos ejecutados y resultados

| Comando | Resultado |
|---|---|
| `dotnet build SGV.slnx` | ✅ OK — 0 warnings, 0 errors |
| `dotnet test SGV.slnx --no-build` | ✅/⚠️ 1132 passed, 12 failed, 0 skipped — los 12 fallos corresponden a `OcupacionRepositoryTests` issue #59 |
| `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~SGV.Tests.Web.Cargo.CargoIndexPageTests"` | ✅ 18/18 |
| `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~SGV.Tests.Api.CargosControllerTests"` | ✅ 41/41 |
| `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~SGV.Tests.Api.SwaggerConfigurationTests.Cargos_ConsultaEndpoint_DocumentaParametroStatus\|FullyQualifiedName~SGV.Tests.Api.SwaggerConfigurationTests.Cargos_ReactivarEndpoint_SigueDocumentado"` | ✅ 2/2 |
| `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~SGV.Tests.Persistencia.CargoRepositoryTests.QueryAsync_MySql_"` | ✅ 7/7 |
| `dotnet test SGV.slnx --no-build --filter "FullyQualifiedName~SGV.Tests.Api.CargosControllerTests.GetConsulta_PropagaSortAlServicio\|FullyQualifiedName~SGV.Tests.Api.CargosControllerTests.GetConsulta_SortInvalido_NoLanzaYLlegaAlServicio\|FullyQualifiedName~SGV.Tests.Web.Cargo.CargoApiClientTests.QueryAsync_WithSort_SerializesSortInUri\|FullyQualifiedName~SGV.Tests.Persistencia.CargoRepositoryTests.QueryAsync_MySql_SortNombreDesc_SeAplicaAntesDePaginar\|FullyQualifiedName~SGV.Tests.Web.Cargo.CargoIndexPageTests.Post_Delete_AlmacenaLastDeletedId_PermiteReactivarEnBanner\|FullyQualifiedName~SGV.Tests.Web.Cargo.CargoIndexPageTests.Post_Delete_CuandoSegmentoEsEliminadas_NoMuestraCtaReactivar\|FullyQualifiedName~SGV.Tests.Web.Cargo.CargoIndexPageTests.Post_Reactivate_Exito_LimpiaLastDeletedId_BannerDesaparece\|FullyQualifiedName~SGV.Tests.Aplicacion.Organizacion.CargoServicioConsultaTests.QueryAsync_ConSortNombreDesc_OrdenaServidorAntesDePaginar"` | ✅ 8/8 |
| `bun run build` (en `src/SGV.Web`) | ✅ OK — solo warnings preexistentes de Browserslist / baseline-browser-mapping |

### Nota sobre MySQL

En este entorno sí hubo MySQL disponible. Verifiqué explícitamente el regression test cross-page `QueryAsync_MySql_SortNombreDesc_SeAplicaAntesDePaginar` y la batería `QueryAsync_MySql_*` de `CargoRepositoryTests` pasó completa.

## 4. Mapeo requisito → evidencia

| Requisito | Test que lo cubre | Implementación verificada | Veredicto |
|---|---|---|---|
| REQ-CM-01 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs` → `QueryAsync_ConSortNombreDesc_OrdenaServidorAntesDePaginar`; `tests/SGV.Tests/Api/CargosControllerTests.cs` → `GetConsulta_PropagaSortAlServicio`; `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` → `QueryAsync_WithSort_SerializesSortInUri`; `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` → `QueryAsync_MySql_SortNombreDesc_SeAplicaAntesDePaginar` | `src/SGV.Api/Controllers/CargosController.cs` → `GetConsulta`; `src/SGV.Aplicacion/Organizacion/Consultas/CargoServicioConsulta.cs` → `QueryAsync`; `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs` → `QueryAsync` / `ApplySort`; `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` → `BuildQueryUri`; `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` → `LoadAsync` | ✅ |
| REQ-CM-02 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoListQueryTests.cs` → `Default_SegmentoEsActivas`; `tests/SGV.Tests/Api/CargosControllerTests.cs` → `GetConsulta_StatusInvalido_CaeA_Activas`, `GetConsulta_SinStatus_RetornaActivas` | `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoListQuery.cs`; `src/SGV.Api/Controllers/CargosController.cs` → normalización de `status` | ✅ |
| REQ-CM-03 | `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs` → `QueryAsync_TotalCountProvieneDelRepositorio`; `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` → `QueryAsync_MySql_Paginacion_TotalCountProvieneDelRepositorio` | `src/SGV.Aplicacion/Organizacion/Consultas/CargoServicioConsulta.cs` → `QueryAsync`; `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs` → `CountAsync` antes del page slice | ✅ |
| REQ-CM-04 | `tests/SGV.Tests/Api/CargosControllerTests.cs` → `PatchReactivar_Conflict_Returns409WithProblemDetails`; `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` → `Post_Reactivate_Falla_ConservaSegmentoEliminadas`; `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` → `QueryAsync_MySql_ActivaYEliminada_MismoCodigo_RetornaAmbasEnDistintosSegmentos` | `src/SGV.Api/Controllers/CargosController.cs` → `Reactivate`; `src/SGV.Aplicacion/Organizacion/Comandos/CargoServicioComandos.cs` → `ReactivarAsync` | ✅ |
| REQ-CW-01 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` → `Get_Index_Default_MuestraVistaActivas`, `Get_Index_StatusEliminadas_MuestraToggleActivoEnEliminadas`, `Get_Index_SinStatus_CaeA_Activas` | `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` → toggle + hidden `status`; `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` → `BuildToggleSegmentoRouteValues`, `NormalizeSegmento` | ✅ |
| REQ-CW-02 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` → `Get_Index_WhenAuthenticated_RendersActiveCargosTable`, `Get_Index_StatusEliminadas_MuestraToggleActivoEnEliminadas`, `Post_Reactivate_Exito_RedirigeAActivas`; `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` → `ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce` | `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` → ramas de acciones activas/eliminadas; `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` → `OnPostReactivateAsync` | ✅ |
| REQ-CW-03 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` → `Post_Reactivate_Exito_RedirigeAActivas`, `Post_Reactivate_Falla_ConservaSegmentoEliminadas` | `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` → `OnPostReactivateAsync` | ✅ |
| REQ-CW-04 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` → `Post_Reactivate_Exito_RedirigeAActivas`, `Post_Reactivate_Falla_ConservaSegmentoEliminadas`, `Post_Delete_AlmacenaLastDeletedId_PermiteReactivarEnBanner` | `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` → links/hidden inputs con `status`, `search`, `sort`, `page`; `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` → redirects y `deletedId` PRG | ✅ |
| REQ-CW-05 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` → `ReactivateConfirmationScript_WhenCancelled_DoesNotSubmitForm`, `ReactivateConfirmationScript_WhenConfirmed_SubmitsFormOnce` | `src/SGV.Web/wwwroot/js/pages/cargos-index.js` → `wireCargoReactivateConfirmation` | ✅ |
| REQ-CW-06 | `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` → `Post_Delete_AlmacenaLastDeletedId_PermiteReactivarEnBanner`, `Post_Delete_CuandoSegmentoEsEliminadas_NoMuestraCtaReactivar`, `Post_Reactivate_Exito_LimpiaLastDeletedId_BannerDesaparece` | `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml.cs` → `LastDeletedId`, `HasLastDeleted`, `OnPostDeleteAsync`, `OnPostReactivateAsync`, `ClearLastDeleted`; `src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml` → CTA banner | ✅ |
| REQ-SRA-01 | `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` → `Cargos_ConsultaEndpoint_DocumentaParametroStatus`, `Cargos_ReactivarEndpoint_SigueDocumentado` | `src/SGV.Api/Controllers/CargosController.cs` → XML docs + `[HttpGet("consulta")]` + `[HttpPatch("{id:guid}/reactivar")]` | ✅ |

## 5. Hallazgos nuevos

### CRITICAL

- Ninguno.

### WARNING

#### W-001 — `apply-progress.md` todavía omite el commit documental `fa1ddc33`
- **Evidencia**:
  - `git log --oneline -15` incluye `fa1ddc33 docs(apply): merge second-pass progress into apply-progress.md`.
  - La tabla `Commits (segunda pasada)` de `openspec/changes/cargos-filtro-activos-eliminados/apply-progress.md` lista `061219e0` y `284881e1`, pero no `fa1ddc33`.
- **Impacto**: no afecta el comportamiento validado ni bloquea archive, pero deja trazabilidad incompleta del cierre documental.

### SUGGESTION

#### S-001 — fortalecer la evidencia web de REQ-CW-02 con asserts explícitos de hide/show en vista Eliminadas
- Hoy la implementación está correcta y el flujo de reactivación está probado, pero una prueba adicional que aserte explícitamente ausencia de `Detalle/Editar/Eliminar/Crear` y presencia de `data-cargo-reactivate-button` en `status=eliminadas` reduciría futuros falsos verdes.

## 6. Decisión final

**READY FOR ARCHIVE**

Razones:
- Los CRITICAL previos **F-001, F-002 y F-003** quedaron resueltos con evidencia de código + ejecución.
- La sugerencia **F-006** ya quedó materializada en un regression test MySQL cross-page.
- Build, batería focalizada, tests web/API/aplicación/persistencia relevantes y frontend build pasaron.
- La suite completa no introdujo fallos nuevos; solo persisten los 12 ya conocidos de `OcupacionRepositoryTests` (issue #59).

## 7. Riesgos residuales

- **medio** — La trazabilidad documental de `apply-progress.md` no está 100 % alineada con `git log` por la omisión de `fa1ddc33` en la tabla de commits.
- **bajo** — La vista Eliminadas está validada funcionalmente, pero podría beneficiarse de un assert web más explícito sobre el set exacto de acciones visibles.

## 8. Próximos pasos sugeridos

1. Pasar a `sdd-archive` para cerrar el cambio.
2. Si se quiere dejar el rastro impecable, registrar en archive que `apply-progress.md` no lista `fa1ddc33` en la tabla de commits aunque sí incorpora su contenido.
3. Mantener fuera del scope de este cambio los 12 fallos conocidos de `OcupacionRepositoryTests` y tratarlos por separado bajo el issue #59.
