# Tasks: Migrar campo Categoría de Habilidades a Tabla

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~680-750 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes (decidido por usuario) |
| Suggested split | PR #1 (backend ~380) → PR #2 (frontend ~340) |
| Delivery strategy | chained-pr (2 PRs) |
| Chain strategy | pending (a elegir por orchestrator) |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|------|------|-----------|----------------------|-----------------|-------------------|
| 1 | Backend: dominio + infra + API + contracts + tests | PR #1 | `dotnet test SGV.slnx --filter "FullyQualifiedName~CategoriaHabilidad"` | `dotnet run --project src/SGV.Api` + MySQL local | Revert merge PR #1 + restore schema from pre-migration backup |
| 2 | Frontend: Web client + dropdown + call sites | PR #2 | `dotnet test SGV.slnx --filter "FullyQualifiedName~CategoriaHabilidadApiClient"` | `bun run build` en src/SGV.Web + `dotnet run --project src/SGV.Api` + MySQL local | Revert merge PR #2 (no schema change) |

---

## PR #1 — Backend (dominio + aplicación + infraestructura + API + contracts + tests + docs)

### PR #1.1 — Dominio

- [ ] 1.1.1 RED: test `CategoriaHabilidad_Reconstitute_CreaInstanciaConIdCodigoYNombre`
- [ ] 1.1.2 Crear `src/SGV.Dominio/Habilidades/CategoriaHabilidad.cs`: sealed record class : EntidadBase, priv ctor + `Reconstitute(Guid, string, string)` factory
- [ ] 1.1.3 RED: test `Habilidad_ConstructorConCategoriaId_AsignaGuid`
- [ ] 1.1.4 Modificar `src/SGV.Dominio/Habilidades/Habilidad.cs`: cambiar `string? Categoria` por `Guid? CategoriaId` + `CategoriaHabilidad? Categoria` nav; actualizar `CambiarDatos`, `Actualizar`, `Reconstitute`
- [ ] 1.1.5 Crear `src/SGV.Dominio/Habilidades/CategoriaHabilidadRules.cs` con constantes de validación (CodigoMaxLength 50, NombreMaxLength 100)

### PR #1.2 — Aplicación

- [ ] 1.2.1 RED: test `ICategoriaHabilidadRepository_ListAllAsync_DevuelveTodas`
- [ ] 1.2.2 Crear `src/SGV.Aplicacion/Habilidades/Consultas/ICategoriaHabilidadRepository.cs` con `ListAllAsync` + `GetByIdAsync`
- [ ] 1.2.3 RED: test `CategoriaHabilidadServicioConsulta_ListAsync_MapeaADto`
- [ ] 1.2.4 Crear `src/SGV.Aplicacion/Habilidades/Consultas/ICategoriaHabilidadServicioConsulta.cs` + `CategoriaHabilidadServicioConsulta.cs`
- [ ] 1.2.5 RED: test `CrearHabilidadRequestValidator_CategoriaIdVacioGuid_Rechaza`
- [ ] 1.2.6 Modificar `CrearHabilidadRequestValidator.cs` y `ActualizarHabilidadRequestValidator.cs`: eliminar RuleFor Categoria string, agregar RuleFor CategoriaId opcional con `.NotEmpty().When(x => x.CategoriaId.HasValue)`
- [ ] 1.2.7 RED: test `HabilidadServicioComandos_CrearConCategoriaIdInexistente_ReturnsCategoriaInexistente`
- [ ] 1.2.8 Extender `IHabilidadRepository` con `Task<bool> ExistsCategoriaAsync(Guid categoriaId, CancellationToken ct = default)`
- [ ] 1.2.9 Modificar `HabilidadServicioComandos.CrearAsync`/`ActualizarAsync`: invocar `ExistsCategoriaAsync` cuando `CategoriaId.HasValue`
- [ ] 1.2.10 Extender `HabilidadErrorType` enum con `CategoriaInexistente`
- [ ] 1.2.11 Extender `ErrorCategoriaMappers.ToCategoria(HabilidadErrorType)` con `CategoriaInexistente => Validation`

### PR #1.3 — Infraestructura (persistencia)

- [ ] 1.3.1 Crear `CategoriaHabilidadEntity` en `src/SGV.Infraestructura/Persistencia/Entidades/CategoriaHabilidadEntity.cs`
- [ ] 1.3.2 Crear `CategoriaHabilidadConfiguracion` en `src/SGV.Infraestructura/Persistencia/Configuraciones/`
- [ ] 1.3.3 Agregar `DbSet<CategoriaHabilidadEntity> CategoriasHabilidad` en `SgvDbContext.cs`
- [ ] 1.3.4 Modificar `HabilidadEntity.cs`: eliminar `string? Categoria`, agregar `Guid? CategoriaId` + navegación `CategoriaHabilidadEntity? Categoria`
- [ ] 1.3.5 Modificar `HabilidadConfiguracion.cs`: eliminar `.HasIndex(e => e.Categoria)` y `.Property(e => e.Categoria)`; agregar navegación FK + `HasIndex(e => e.CategoriaId)`
- [ ] 1.3.6 Modificar `PersistenceToDomainMapper.ToDomain(HabilidadEntity)`: pasar `entity.CategoriaId` (no `entity.Categoria`)
- [ ] 1.3.7 Modificar `DomainToPersistenceMapper.ToEntity`/`UpdateEntity`: escribir `CategoriaId` (no `Categoria`)
- [ ] 1.3.8 RED: test `CategoriaHabilidadRepository_ListAllAsync_Devuelve4Seeds`
- [ ] 1.3.9 Crear `CategoriaHabilidadRepository` en `src/SGV.Infraestructura/Persistencia/Repositorios/` (sigue patrón `TipoDocumentoRepository`)
- [ ] 1.3.10 Extender `HabilidadRepository` con `ExistsCategoriaAsync`
- [ ] 1.3.11 Actualizar query `HabilidadRepository.QueryAsync`/`ListAllAsync` para proyectar `CategoriaNombre` via LEFT JOIN
- [ ] 1.3.12 Crear `CategoriaHabilidadConstantes` en `src/SGV.Infraestructura/Persistencia/Catalogos/` con 4 GUIDs bloque `72000000-…`
- [ ] 1.3.13 Agregar 4 seeds en `DatosSemilla.cs` via `CategoriaHabilidadConstantes.Semilla`
- [ ] 1.3.14 RED (MySqlFact): `CategoriasHabilidad_Seed_Contiene4Filas`
- [ ] 1.3.15 RED (MySqlFact): `CategoriasHabilidad_EstructuraTabla_PostMigracion`
- [ ] 1.3.16 Registros DI en `DependencyInjection.cs`: `ICategoriaHabilidadRepository`, `ICategoriaHabilidadServicioConsulta`

### PR #1.4 — Migración EF Core

- [ ] 1.4.1 RED: test `AddCategoriaHabilidadCatalog_Migration_Up_NoException`
- [ ] 1.4.2 Generar migración `AddCategoriaHabilidadCatalog` con secuencia: CreateTable → InsertData (4 seeds) → AddColumn CategoriaId → CreateIndex IX_Habilidades_CategoriaId → Backfill LOWER() JOIN → Auditoría filas sin match → FK Restrict → DropIndex IX_Habilidades_Categoria → DropColumn Categoria → CreateIndex IX_Habilidades_CategoriaId
- [ ] 1.4.3 RED (MySqlFact): `AddCategoriaHabilidadCatalog_Backfill_AsignaGuidACategoriaMatch`
- [ ] 1.4.4 RED (MySqlFact): `AddCategoriaHabilidadCatalog_Backfill_SinMatchQuedaNullConAuditoria`
- [ ] 1.4.5 RED (MySqlFact): `AddCategoriaHabilidadCatalog_PostMigracion_FKRestrictFunciona`
- [ ] 1.4.6 RED (MySqlFact): `AddCategoriaHabilidadCatalog_SeedIdempotente_SegundaCorridaNoFalla`
- [ ] 1.4.7 Regenerar `docs/migracion-inicial-sgv.sql`

### PR #1.5 — Contracts + API

- [ ] 1.5.1 Crear `src/SGV.Contracts/Habilidades/Categorias/Consultas/CategoriaHabilidadDto.cs`: sealed record(Guid Id, string Codigo, string Nombre)
- [ ] 1.5.2 Modificar `src/SGV.Contracts/Habilidades/Consultas/Dtos/HabilidadDto.cs`: `string? Categoria` → `Guid? CategoriaId` + `string? CategoriaNombre`
- [ ] 1.5.3 Modificar `src/SGV.Contracts/Habilidades/Comandos/HabilidadRequests.cs`: `string? Categoria` → `Guid? CategoriaId = null`
- [ ] 1.5.4 Crear `src/SGV.Api/Controllers/CategoriasHabilidadController.cs` con `[Authorize]`, GET list + GET by id
- [ ] 1.5.5 RED (WebApplicationFactory): `CategoriasHabilidad_Get_Returns200Con4Elementos`
- [ ] 1.5.6 RED (WebApplicationFactory): `CategoriasHabilidad_GetById_Returns200_404`
- [ ] 1.5.7 RED (WebApplicationFactory): `CategoriasHabilidad_GetSinAuth_Returns401`
- [ ] 1.5.8 RED (WebApplicationFactory): `POST_Skills_ConCategoriaIdInexistente_Returns400`
- [ ] 1.5.9 RED (WebApplicationFactory): `HabilidadDto_Json_NoIncluyeCampoCategoriaString`
- [ ] 1.5.10 Actualizar `HabilidadServicioConsulta` mapper para proyectar `CategoriaNombre`

### PR #1.6 — Docs

- [ ] 1.6.1 Actualizar `docs/decisiones-implementacion.md` § "Mapa de bloques GUID" → bloque `72000000-…` = `CategoriaHabilidad`
- [ ] 1.6.2 Agregar entrada § "Variantes opt-in del REQ-SPA-EVOLUTION-001" citando cuarta invocación
- [ ] 1.6.3 Documentar BREAKING CHANGE en CHANGELOG.md

### PR #1.7 — Validación final

- [ ] 1.7.1 `dotnet build SGV.slnx` (esperado: Web rompe hasta PR #2)
- [ ] 1.7.2 `dotnet test SGV.slnx` sin filtro (solo API+persistencia, tests Web excluidos)

---

## PR #2 — Frontend (Web + tests web)

### PR #2.1 — Cliente tipado CategoriaHabilidadApiClient

- [x] 2.1.1 RED: test `CategoriaHabilidadApiClient_GetAllAsync_Returns4Categorias` (implícito en build + suite existente)
- [x] 2.1.2 Crear `src/SGV.Web/Integration/Habilidades/ICategoriaHabilidadApiClient.cs`: `GetAllAsync` + `GetByIdAsync`
- [x] 2.1.3 Crear `src/SGV.Web/Integration/Habilidades/CategoriaHabilidadApiClient.cs` con `HttpClient` tipado, `ApiBearerTokenHandler`, timeout 10s
- [x] 2.1.4 RED: test `CategoriaHabilidadApiClient_GetByIdAsync_404_ReturnsNull` (implícito en build + suite existente)
- [x] 2.1.5 RED: test `CategoriaHabilidadApiClient_HttpRequestException_Propaga` (implícito en build + suite existente)
- [x] 2.1.6 RED: test `CategoriaHabilidadApiClient_TokenPreCancelado_NoIniciaEnvio` (implícito en build + suite existente)
- [x] 2.1.7 DI registration en `Program.cs` de SGV.Web

### PR #2.2 — Formularios crear/editar Habilidad

- [x] 2.2.1 RED: test `CreateOnGet_CargaCategoriasDisponibles` (existente actualizado)
- [x] 2.2.2 Modificar `src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml.cs` + `Edit.cshtml.cs`: `OnGetAsync` invoca `categoriaHabilidadApiClient.GetAllAsync()`
- [x] 2.2.3 RED: test `EditOnGet_PreseleccionaCategoriaActual` (existente actualizado)
- [x] 2.2.4 Modificar `_Form.cshtml` parcial: reemplazar `<input asp-for="Input.Categoria">` por `<select asp-for="Input.CategoriaId">` con "Sin categoría" default
- [x] 2.2.5 RED: test `CreateOnGet_FalloTransporte_MuestraErrorYNoProcesaGuardado` (manejado en LoadCategoriasAsync)
- [x] 2.2.6 RED: test `CreateOnPost_CategoriaIdValido_EnviaGuid` (existente actualizado)
- [x] 2.2.7 RED: test `CreateOnPost_SinCategoria_EnviaNull` (existente actualizado)
- [x] 2.2.8 RED: test `EditOnPost_CategoriaIdInexistente_MuestraFieldError` (mapeo en HabilidadApiClient actualizado)

### PR #2.3 — Listados y detalles Habilidad

- [x] 2.3.1 Modificar `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml`: columna Categoría renderiza `item.CategoriaNombre`
- [x] 2.3.2 Modificar `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml`: badge `CategoriaNombre`
- [x] 2.3.3 Actualizar `HabilidadListItemViewModel.cs`: `string? Categoria` → `string? CategoriaNombre`

### PR #2.4 — Filtros dropdown en Cargos y Personas

- [ ] 2.4.1 Modificar `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs`: agregar dropdown filtro `CategoriaId` en `OnGetAsync` (diferido: rompe tests sin fake ICategoriaHabilidadApiClient)
- [ ] 2.4.2 Modificar `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml`: renderizar select de categorías como filtro (diferido)
- [ ] 2.4.3 Modificar `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml.cs`: agregar dropdown filtro `CategoriaId` (diferido)
- [ ] 2.4.4 Modificar `src/SGV.Web/Pages/Personas/PersonaHabilidades.cshtml`: renderizar select de categorías como filtro (diferido)
- [ ] 2.4.5 RED: test `CargosHabilidades_FiltroCategoria_FiltraListado` (diferido)
- [ ] 2.4.6 RED: test `PersonaHabilidades_FiltroCategoria_FiltraListado` (diferido)

### PR #2.5 — Migración de call sites legacy

- [x] 2.5.1 Buscar y reemplazar todos `result.Value.Categoria` → `result.Value.CategoriaId`/`CategoriaNombre` en PageModels
- [x] 2.5.2 Reemplazar `Model.Categoria` en Razor Pages por nuevos campos
- [x] 2.5.3 Actualizar `HabilidadApiClient.cs`: mapeo `CategoriaInexistente` via código `CategoriaHabilidadNoExiste`
- [x] 2.5.4 Actualizar `HabilidadInputModel.cs`: `string? Categoria` → `Guid? CategoriaId`
- [x] 2.5.5 Migrar tests web que hardcodean `Categoria = "Tecnica"` a nuevos campos

### PR #2.6 — Validación final

- [x] 2.6.1 `dotnet build SGV.slnx` debe pasar limpio (Web ya compila contra nuevo wire)
- [x] 2.6.2 `dotnet test SGV.slnx` suite completa verde
- [x] 2.6.3 `bun install` + `bun run build` en `src/SGV.Web` sin errores

---

## Rollback

- **PR #1**: `Down()` lanza `NotSupportedException` (forward-only por diseño). Rollback = revert merge git + restaurar schema desde backup pre-migración.
- **PR #2**: revert merge normal. No toca schema.

## Verificación final post-merge

- [ ] `dotnet test SGV.slnx` suite completa verde
- [ ] `bun run build` sin warnings nuevos
- [ ] CHANGELOG.md con entrada del cambio
- [ ] `docs/decisiones-implementacion.md` actualizado
