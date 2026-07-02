# Archive Report: cargos-filtro-activos-eliminados

## 1. Resumen ejecutivo

El cambio `cargos-filtro-activos-eliminados` cierra el ciclo SDD incorporando al dominio de Cargos el mismo patrón ya archivado para Unidades Organizativas: consulta segmentada `activas`/`eliminadas`, paginación server-side, reactivación contextual y documentación Swagger consistente.
Se sincronizaron los tres delta specs del cambio hacia `openspec/specs/` sin perder el contenido previo de cada capacidad.
La verificación final quedó en **READY FOR ARCHIVE**, sin hallazgos CRITICAL abiertos y con evidencia de aplicación, persistencia MySQL, API, web y frontend build.
El archivo preserva la trazabilidad completa del cambio, incluyendo el `size:exception` aprobado por el usuario para entregar todo en una sola PR.
Queda registrado un conflicto documental parcial en `cargo-web-listado-detalle-baja`, donde el requisito histórico del slice inicial sigue coexistiendo con los nuevos requisitos segmentados para no perder contexto previo.

## 2. Specs sincronizados

- `openspec/specs/cargo-management/spec.md` — **mergeado** con REQ-CM-01, REQ-CM-02, REQ-CM-03 y REQ-CM-04.
- `openspec/specs/cargo-web-listado-detalle-baja/spec.md` — **mergeado** con REQ-CW-01, REQ-CW-02, REQ-CW-03, REQ-CW-04, REQ-CW-05 y REQ-CW-06.
- `openspec/specs/sgv-readonly-api/spec.md` — **mergeado** con REQ-SRA-01.

## 3. Requisitos agregados

- `REQ-CM-01` — consulta segmentada de cargos eliminados sin mezclar activos.
- `REQ-CM-02` — activas por defecto y normalización de `status` desconocido en el borde HTTP.
- `REQ-CM-03` — `TotalCount` y `TotalPages` provenientes del repositorio segmentado.
- `REQ-CM-04` — reactivación de cargo preservando unicidad activa de `Codigo`.
- `REQ-CW-01` — toggle binario Activas/Eliminadas con reset de página y preservación de búsqueda/orden.
- `REQ-CW-02` — vista Eliminadas con acciones contextuales de reactivación por fila.
- `REQ-CW-03` — redirección y feedback correcto tras `?handler=Reactivate`.
- `REQ-CW-04` — preservación de `status`, `search`, `sort`, `p` y `LastDeletedId` en navegación y PRG.
- `REQ-CW-05` — confirmación JS de reactivación con SweetAlert2 y `data-cargo-reactivate-*`.
- `REQ-CW-06` — CTA rápido para la última baja solo en la vista Activas.
- `REQ-SRA-01` — Swagger documenta `GET /api/v1/cargos/consulta` y mantiene visible `PATCH /api/v1/cargos/{id}/reactivar`.

## 4. Cambios arquitectónicos

- Nuevo endpoint `GET /api/v1/cargos/consulta` con `status`, `search`, `sort`, `page` y `pageSize`.
- Consulta server-side paginada desde repositorio segmentado, sin ordenar ni paginar en memoria desde `GetAllAsync`.
- Nuevo enum de aplicación `CargoSegmentoListado` para encapsular `Activas`/`Eliminadas` fuera del borde HTTP.
- `CargoApiClient` y `IndexModel` preservan `status`, `search`, `sort`, `p` y `LastDeletedId` end-to-end.
- `Index.cshtml` y `cargos-index.js` agregan reactivación contextual con confirmación SweetAlert2.
- Swagger documenta la consulta segmentada de cargos sin alterar los contratos públicos ajenos al slice.

## 5. Tests agregados

- `tests/SGV.Tests/Aplicacion/Organizacion/CargoListQueryTests.cs` — 3 tests nuevos del query/enum.
- `tests/SGV.Tests/Aplicacion/Organizacion/CargoServicioConsultaTests.cs` — +7 tests focalizados de segmento, conteo y sort server-side.
- `tests/SGV.Tests/Persistencia/CargoRepositoryTests.cs` — +7 tests `[MySqlFact]` para segmentos, total count y sort cross-page.
- `tests/SGV.Tests/Api/CargosControllerTests.cs` — +6 tests del endpoint `consulta`, normalización de `status` y propagación de `sort`.
- `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs` — +2 tests de documentación Swagger.
- `tests/SGV.Tests/Web/Cargo/CargoApiClientTests.cs` — +6 tests de serialización `status`/`sort` y `ReactivateAsync`.
- `tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs` — +18 tests focalizados del toggle, CTA `LastDeletedId`, PRG y confirmación JS.

## 6. Métricas finales

- LOC acumulado del cambio: ~1390.
- Archivos tocados del cambio: 26.
- Commits en la branch del cambio: 14.
- Tests añadidos: 100 (acumulado reportado en `apply-progress.md`).
- Decisión final de verify: **READY FOR ARCHIVE**.

## 7. Decisiones del usuario

- El usuario aprobó **single PR con `size:exception`** para este cambio.
- La excepción mantiene el budget de review de 400 líneas como referencia, pero acepta el acumulado real (~1390 LOC) en una sola PR.

## 8. Conflictos documentales preservados

- `openspec/specs/cargo-web-listado-detalle-baja/spec.md` conserva el requisito histórico **"Listado visible de cargos activos"** del slice inicial, cuyo alcance original decía que la UI no debía exponer create, edit, eliminados ni reactivación. Los nuevos REQ-CW-01..06 reflejan el comportamiento vigente y amplían ese alcance.
- Se preservó ese requisito previo para no perder trazabilidad histórica, por lo que la capacidad queda con una tensión documental menor que el orchestrator puede resolver más adelante si quiere consolidar la narrativa del spec.
- `verify-report.md` marca además como warning residual que `apply-progress.md` no lista el commit documental `fa1ddc33` en su tabla, aunque sí incorpora su contenido.

## 9. Próximos pasos sugeridos

1. Crear la PR con la skill `branch-pr` usando la branch `feat/cargos-filtro-activos-eliminados`.
2. Esperar review humano sobre el cambio archivado y el `size:exception` aprobado.
3. Mergear la branch una vez aprobada, manteniendo fuera de scope los 12 fallos conocidos de `OcupacionRepositoryTests` (issue #59).
