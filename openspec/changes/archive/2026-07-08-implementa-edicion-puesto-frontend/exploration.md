# Exploración: Implementa la edición de un Puesto en el Frontend

> **⚠ ALCANCE REPLANTEADO**. La intención declarada del change ("agregar la página Edit") ya está satisfecha por el change archivado `2026-07-06-implementa-modulo-puestos-en-frontend` (PRs #92/#93/#94 mergeados en `develop`, `verify-report.md` PASS 100/100). Esta exploración confirma ese estado y reduce el alcance real a una **brecha de UI acotada**: el botón "Editar" por fila en el listado, que el spec canónico vigente ya exige pero que la implementación de PR 2 omitió y que PR 3B/3C no actualizaron. Ver sección **Recomendación** para el slice propuesto.

## Contexto actual

### Lo que YA existe en `develop` (post-archive `2026-07-06`)

| Pieza | Estado | Fuente verificada |
|---|---|---|
| Página `Edit` Razor | ✅ Implementada y testeada | `src/SGV.Web/Pages/Organizacion/Puestos/Edit.cshtml` (65 líneas) + `Edit.cshtml.cs` (344 líneas) mergeadas en PR #93 (`8fb25552`) |
| Página `Details` Razor | ✅ Implementada y testeada | `src/SGV.Web/Pages/Organizacion/Puestos/Details.cshtml` (95 líneas) + `Details.cshtml.cs` (230 líneas) mergeadas en PR #94 (`ad55fee6`) |
| Página `Create` Razor | ✅ Implementada y testeada | `src/SGV.Web/Pages/Organizacion/Puestos/Create.cshtml` (45 líneas) + `Create.cshtml.cs` (271 líneas) mergeadas en PR #92 (`263e051d`) |
| Página `Index` Razor | ✅ Implementada parcialmente (sin botón Editar por fila) | `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml` (257 líneas) mergeada en PR #91 (`8774a5f0`); commit posterior `4e3fbad5` agregó botón "Crear puesto" en la cabecera pero NO tocó las acciones por fila |
| Partial `_Form.cshtml` compartido | ✅ Implementado con flag `IsEdit` | `src/SGV.Web/Pages/Organizacion/Puestos/_Form.cshtml` (74 líneas) |
| Endpoint API `PUT /api/v1/puestos/{id}` | ✅ Implementado | `src/SGV.Api/Controllers/PuestosController.cs:87-104` → `_comandos.ActualizarAsync` |
| Cliente `IPuestosApiClient.UpdateAsync` | ✅ Implementado | `src/SGV.Web/Integration/Organizacion/PuestosApiClient.cs:55-66` mapea 200/400/404/409 vía `ToCommandResultAsync` |
| `ActualizarPuestoRequest` (3 campos) | ✅ Implementado | Espejo exacto de `ActualizarCargoRequest` (Nombre/Descripcion?/PuestoSuperiorId?) |
| Tests Edit | ✅ 9/9 PASS | `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs` incluye el test RED obligatorio `Get_Edit_HtmlRenderizado_NoContieneCodigoUnidadOrganizativaNiCargo` |
| Tests Details | ✅ 5/5 PASS | `tests/SGV.Tests/Web/Puesto/PuestoDetailsPageTests.cs` |
| Tests Create | ✅ 9/9 PASS | `tests/SGV.Tests/Web/Puesto/PuestoCreatePageTests.cs` |
| Tests Index | ✅ 17/17 PASS pero **NO** assertan presencia de botón Editar | `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs:31-54` (`Get_Index_WhenAuthenticated_RendersActivePuestosTable`) solo verifica `Puestos`, `Listado de puestos activos`, `Codigo`, `Nombre`, `Ventas`, `Vendedor` |
| Suite completa web | ✅ 406/406 PASS sin regresión | `verify-report.md` archivado |

### La brecha real (no detectada por el verify-report)

El spec canónico vigente `openspec/specs/puesto-web-listado-detalle-baja/spec.md` (línea 27, requisito "Listado plano con toggle deshabilitado") dice textualmente:

> #### Scenario: Carga inicial con columnas locked
> - AND cada fila MUST ofrecer `Detalle`, `Editar` y `Eliminar`.

Pero `Index.cshtml` (líneas 181-237, bloque `if (!Model.IsDeletedView)`) solo renderiza:

- `Detalle` → `<a class="btn btn-info btn-icon btn-sm rounded-circle" href="@Model.BuildDetailsUrl(item.Id)">` (líneas 189-194)
- `Eliminar` → `<form ... data-puesto-delete-form>` (líneas 195-213)

**Falta el botón "Editar"**, contraviniendo el spec canónico. El comment obsoleto en `Index.cshtml:183-186` dice:

```
@* PR 2 — solo Detalle y Eliminar (Editar y
   Habilidades NO existen en este PR; viven en
   PR 3A/3B). PR 2 también excluye Crear.
   Index recibe su navegación desde el sidenav. *@
```

Esto era cierto cuando se escribió en PR 2, pero quedó **desactualizado** después de que PR 3B (`8fb25552`) mergeó la página `Edit` y PR 3C (`ad55fee6`) mergeó `Details`. El `Index` nunca se actualizó para reflejar la paridad con Cargos.

### Espejo Cargos (precedente directo)

`src/SGV.Web/Pages/Organizacion/Cargos/Index.cshtml:169` resuelve exactamente esta brecha con el helper `BuildEditRouteValues(Guid id)` declarado en `CargosIndexModel.cs:237-244`:

```csharp
public object BuildEditRouteValues(Guid id) => new
{
    id,
    p = CurrentPage,
    search = Search,
    sort = Sort,
    returnStatus = Segmento
};
```

Y lo renderiza así (líneas 169-171):

```html
<a class="btn btn-warning btn-icon btn-sm rounded-circle"
   href="@Url.Page("/Organizacion/Cargos/Edit", Model.BuildEditRouteValues(item.Id))"
   data-bs-toggle="tooltip" data-bs-title="Editar"
   aria-label="Editar @item.Nombre">
    <i class="ti ti-edit fs-lg"></i>
</a>
```

El test que blinda esto en Cargos (`tests/SGV.Tests/Web/Cargo/CargoIndexPageTests.cs:50-53`):

```csharp
// Las filas deben exponer las acciones Detalle, Editar y Eliminar
Assert.Contains($"/organizacion/cargos/detalles/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
Assert.Contains($"/organizacion/cargos/editar/{first.Id}", content, StringComparison.OrdinalIgnoreCase);
Assert.Contains("data-bs-title=\"Editar\"", content, StringComparison.OrdinalIgnoreCase);
Assert.Contains("data-cargo-delete-form", content, StringComparison.OrdinalIgnoreCase);
Assert.Contains("data-cargo-delete-button", content, StringComparison.OrdinalIgnoreCase);
```

**El espejo Puestos no tiene estas aserciones para Editar** (líneas 31-54 del test, solo cubre Detalle y la presencia de la fila).

### Estado del working tree (no relacionado)

`git status --short` reporta cambios sin commitear:

```
 M src/SGV.Infraestructura/Persistencia/DatosSemilla.cs
?? src/SGV.Infraestructura/Persistencia/Migraciones/20260706221558_AgregarDatosSemillaPuestos.Designer.cs
?? src/SGV.Infraestructura/Persistencia/Migraciones/20260706221558_AgregarDatosSemillaPuestos.cs
```

Estos archivos son **de otro trabajo en curso** (datos semilla para Puestos, probablemente del propio `2026-07-06-implementa-modulo-puestos-en-frontend` que aún no se commiteó), **NO** deben tocarse en este change.

### Out-of-scope confirmado (no tocar)

- **Dominio / Aplicación / Infraestructura / API**: el slice anterior archivado ya cerró backend + integración. No hay nuevos commands, validators ni endpoints por agregar.
- **`PuestosController` `[Authorize(Roles=Administrador)]`**: follow-up independiente `puestos-crear-autorizacion-admin` (paralelo a `2026-07-01-cargos-crear-autorizacion-admin`); `AGENTS.md` "Decisiones Técnicas que NO conviene romper" lo confirma como fuera de scope.
- **Endpoint `GET /api/v1/puestos/consulta?status=activas|eliminadas`**: follow-up `puestos-filtro-activos-eliminados`; el toggle "Eliminadas" sigue renderizado `disabled` con tooltip "Próximamente".
- **Migraciones**: el spec de unicidad activa (`ActiveCodigoUnique`) y la columna generada ya están materializadas en la migración inicial. No aplica cambio de schema.
- **Sidenav**: la convención cross-módulo es `Listado` + `Nuevo` (verificado en `Cargos`/`Habilidades`/`Puestos` sidenav `127-145`). Edit se accede vía fila. No se agrega sub-item "Editar".
- **Catálogo `IUnidadOrganizativaApiClient` con `GetAllAsync()`**: ya documentado como follow-up SUGGESTION-1 del archive anterior; no es regresión.

## Áreas afectadas

Cambio quirúrgico de **~30-60 LoC** en 3 archivos del módulo web de Puestos (más 2-3 asserts nuevos en tests):

- `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml.cs` — agregar `public object BuildEditRouteValues(Guid id)` que devuelva `{ id, p, CurrentPage, search = Search, sort = Sort, returnStatus = Segmento }`. Espejo 1:1 del helper de Cargos (`CargosIndexModel.cs:237-244`).
- `src/SGV.Web/Pages/Organizacion/Puestos/Index.cshtml` — agregar el `<a class="btn btn-warning btn-icon btn-sm rounded-circle" ...>` entre el botón `Detalle` y el `<form data-puesto-delete-form>` (líneas 189-214). Borrar el comment obsoleto líneas 183-186.
- `tests/SGV.Tests/Web/Puesto/PuestoIndexPageTests.cs` — extender `Get_Index_WhenAuthenticated_RendersActivePuestosTable` con `Assert.Contains($"/organizacion/puestos/editar/{first.Id}", ...)` + `Assert.Contains("data-bs-title=\"Editar\"", ...)` (espejo exacto del patrón Cargos líneas 52-53). Agregar un test RED que verifique que el botón **NO** aparece en la vista Eliminadas (paridad con Cargos `if (!Model.IsDeletedView)`).

Archivos **NO** afectados (confirmado por re-lectura):

- `Edit.cshtml`, `Edit.cshtml.cs`, `Details.cshtml`, `Details.cshtml.cs`, `Create.cshtml`, `Create.cshtml.cs`, `_Form.cshtml` — intactos.
- `PuestosController.cs`, `PuestosApiClient.cs`, `IPuestosApiClient.cs` — intactos.
- `_Sidenav.cshtml` — intacto (sidenav sigue exponiendo solo `Listado` + `Nuevo`).
- `tests/SGV.Tests/Web/Puesto/PuestoEditPageTests.cs`, `PuestoDetailsPageTests.cs`, `PuestoCreatePageTests.cs`, `PuestosApiClientTests.cs`, `IPuestosApiClientContractTests.cs`, `PuestoWebSeamTests.cs`, `PuestoWebTestFixture.cs`, `FakePuestosApiClient.cs`, `FakeUnidadOrganizativaApiClient.cs`, `PuestoFormHelpersTests.cs`, `PuestoPostResultMapperTests.cs` — intactos.

## Enfoques considerados

### 1. Replicar exactamente el patrón Cargos en `Index.cshtml.cs` + `Index.cshtml`

- **A favor**: paridad operativa ya consolidada en el repo (Cargos y Habilidades ya lo hacen así); 0 riesgo de divergencia con el resto del módulo; test espejo de `CargoIndexPageTests.cs:50-53` garantiza cobertura; cambio pequeño (~30-60 LoC) entra en 1 PR dentro del budget de 400.
- **En contra**: requiere tocar el comment obsoleto (correcto hacerlo, pero es trabajo extra mínimo); ningún downside técnico.
- **Esfuerzo**: **Bajo**.

### 2. Cambiar el nombre del change a "Exponer botón Editar en Index de Puestos" y reducir alcance a ese único fix

- **A favor**: refleja fielmente la brecha real; el orchestrator puede usar el mismo slug o renombrarlo en la propuesta.
- **En contra**: requiere actualizar el `proposal.md` y posiblemente el `tasks.md` cuando lleguen; el orchestrator debe aprobar el rename o tratarlo como refinamiento.
- **Esfuerzo**: **Bajo** (es una decisión de gobernanza, no de código).

### 3. Crear página Edit desde cero (lo que el orchestrator asumió)

- **A favor**: ninguno técnico.
- **En contra**: ya existe, re-implementarlo duplicaría archivos y rompería el merge con PR #93. Generaría conflictos masivos con `Edit.cshtml(.cs)`, `_Form.cshtml`, `PuestosApiClient.UpdateAsync`, `PuestoEditPageTests` y el contrato `ActualizarPuestoRequest`.
- **Esfuerzo**: **Alto** y rompe el repositorio. **Descartado**.

### 4. Agregar Edit a sidenav + omitir el botón por fila

- **A favor**: paridad con cómo Cargos NO lo hace (Cargos tampoco lo agrega al sidenav).
- **En contra**: contradice la convención cross-módulo; el spec canónico exige el botón por fila; peor UX (un click extra).
- **Esfuerzo**: **Bajo** pero **descartado** por spec y por patrón.

## Recomendación

**Adoptar el enfoque 1 + 2 combinados**: ejecutar el fix quirúrgico (espejo Cargos) y aclararle al usuario que la página Edit ya existe. La propuesta debe declarar explícitamente que el alcance real es "exponer el botón Editar por fila en el Index de Puestos" y que este change **corrige un drift de spec** detectado en el verify-report archivado (no detectado antes porque el test del Index no assertaba presencia del botón).

**Por qué este enfoque:**

1. **Spec compliance**: el requisito canónico `puesto-web-listado-detalle-baja/spec.md:27` exige el botón por fila; el slice actual viola el spec. Cerrar este gap es trabajo legítimo.
2. **Paridad operativa**: Cargos y Habilidades ya lo hacen; el módulo Puestos está incompleto operativamente sin ese botón.
3. **Slice único chiquito**: 1 PR de ~30-60 LoC + 2-3 asserts nuevos. Sin chained PRs, sin budget risk, sin tocar backend.
4. **Strict TDD friendly**: los 2-3 tests nuevos siguen el patrón exacto de `CargoIndexPageTests.cs:50-53` y se triangulan naturalmente (presencia en Activas + ausencia en Eliminadas + preservación de contexto).
5. **Sin nuevos delta specs**: el spec canónico vigente ya cubre el requisito; no hace falta `## ADDED Requirements` ni `## MODIFIED Requirements`. Esto evita trabajo innecesario en la fase de archive.

**No requiere tocar:**

- `PuestoEditPageTests` (la página Edit está cubierta al 100%).
- `PuestosController` ni `[Authorize(Roles=Administrador)]` (follow-up independiente).
- `IPuestosApiClient.UpdateAsync` (ya implementado y testeado).
- `_Sidenav.cshtml` (la convención cross-módulo es fija).
- Migraciones o dominio.

## Riesgos

| Riesgo | Likelihood | Mitigación |
|---|---|---|
| El orchestrator esperaba un change grande (PRs encadenados como el archive anterior) y propone tareas innecesarias | Med | El exploration.md deja explícito que el alcance es ~30-60 LoC; el orchestrator debe adaptar `proposal.md` y `tasks.md` al slice real antes de `sdd-propose`. |
| Working tree tiene cambios sin commitear de `DatosSemilla.cs` y migración nueva que se mezclan con el fix | Alta | El agente de `sdd-apply` debe aislar SOLO los archivos del slice (Index.cshtml, Index.cshtml.cs, PuestoIndexPageTests.cs). `git diff -- src/SGV.Web/...` antes de commitear y abortar si aparecen archivos no listados. |
| Spec canónico exige "Editar" pero nadie lo notó porque el test del Index no lo assertaba | Ya ocurrido | Los 2-3 asserts nuevos en `PuestoIndexPageTests.Get_Index_WhenAuthenticated_RendersActivePuestosTable` previenen la regresión. Considerar también agregar `Assert.DoesNotContain(">Editar<", content)` en el segmento Eliminadas (siguiendo `CargoIndexPageTests` que sí excluye `data-cargo-reactivate-button` para activas). |
| Renombrar el slug del change genera ruido en `openspec/changes/` | Baja | Mantener el slug `2026-07-08-implementa-edicion-puesto-frontend` (es la intención del usuario). El alcance se aclara en `proposal.md` y `tasks.md` sin tocar el nombre de carpeta. |
| El usuario insiste en "implementar Edit" pensando que falta la página | Baja | El exploration está siendo devuelto al orchestrator con evidencia (PRs #92/#93/#94 mergeados, 100/100 tests PASS, verify-report archivado) para que el orchestrator lo aclare antes de pasar a `sdd-propose`. |

## Listo para propuesta

**Sí, con refinamiento de alcance obligatorio.**

El orchestrator debe informarle al usuario, antes de lanzar `sdd-propose`, que:

1. **La página Edit ya existe** (mergeada en `develop` vía PR #93) y que la suite `PuestoEditPageTests` la cubre al 100% (9/9 PASS).
2. **La brecha real** es el botón "Editar" por fila en el `Index` (espejo del patrón Cargos) y que corregirla ocupa ~30-60 LoC en un solo PR.
3. **No hace falta tocar backend, dominio, aplicación, infraestructura ni API**; el cambio es frontend-only sobre 3 archivos.
4. **No hace falta nuevo delta spec** porque el spec canónico vigente `puesto-web-listado-detalle-baja/spec.md:27` ya exige el botón.

**Open questions para el usuario** (el orchestrator debería consultarlas antes de `sdd-propose`):

- ¿Desea mantener el slug `2026-07-08-implementa-edicion-puesto-frontend` aunque el alcance terminó siendo más acotado que el título sugiere? (Recomendación: sí, mantenerlo; el alcance se aclara en la propuesta.)
- ¿Confirmar que NO se aprovechará este slice para meter el `[Authorize(Roles=Administrador)]` en `PuestosController` ni el endpoint segmentado `?status=activas|eliminadas`? (Recomendación: NO; son follow-ups separados ya documentados en el archive anterior.)
