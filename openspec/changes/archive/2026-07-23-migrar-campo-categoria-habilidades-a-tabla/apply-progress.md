# Apply Progress: Migrar campo Categoría de Habilidades a Tabla

## Resumen

Cambio completo implementado en 2 PRs encadenados. Smoke test final: **2,891/2,891 PASS** ✅. Build y bundle frontend verificados.

## PRs Mergeados

| PR | Área | Archivos | Estado |
|----|------|----------|--------|
| [#193 — Backend](https://github.com/SGV/pr/193) | Dominio + Aplicación + Infraestructura + API + Contracts + Tests | ~56 files | ✅ Mergeado 2026-07-23 |
| [#194 — Frontend](https://github.com/SGV/pr/194) | Web clientes tipados + Razor Pages + ViewModels + Tests | ~38 files | ✅ Mergeado 2026-07-23 |

## Estructura del Cambio

### PR #193 — Backend

| Capa | Cambios |
|------|---------|
| **Dominio** | `CategoriaHabilidad.cs` (sealed record + Reconstitute), `Habilidad.cs` (Categoria → CategoriaId + nav), `CategoriaHabilidadRules.cs` |
| **Aplicación** | `ICategoriaHabilidadRepository`, `ICategoriaHabilidadServicioConsulta` + impl, validadores actualizados, `HabilidadServicioComandos` con validación FK, `HabilidadErrorType.CategoriaInexistente` |
| **Infraestructura** | `CategoriaHabilidadEntity`, `CategoriaHabilidadConfiguracion`, mappers, repositorio, `CategoriaHabilidadConstantes`, `DatosSemilla`, DI registration |
| **Migración** | `AddCategoriaHabilidadCatalog` (forward-only): CreateTable → InsertData (4 seeds) → AddColumn → Backfill LOWER() JOIN → Auditoría → FK Restrict → DropIndex/DropColumn legacy → CreateIndex |
| **API** | `CategoriasHabilidadController` read-only autenticado (`GET list` + `GET by id`) |
| **Contracts** | `CategoriaHabilidadDto`, `HabilidadDto` (Categoria → CategoriaId + CategoriaNombre), `HabilidadRequests` (CategoriaId nullable) |
| **Docs** | `docs/decisiones-implementacion.md` actualizado: bloque `72000000-…` + §Variantes opt-in + §Migración CategoriasHabilidad |

### PR #194 — Frontend

| Capa | Cambios |
|------|---------|
| **Cliente tipado** | `ICategoriaHabilidadApiClient` + `CategoriaHabilidadApiClient` (read-only, 10s timeout, CommandResultMapper) |
| **Formularios** | Create/Edit Habilidad: dropdown `<select>` con opciones del catálogo + "Sin categoría" default |
| **Listados** | Index/Details: columna Categoría renderiza `CategoriaNombre` |
| **ViewModel** | `HabilidadListItemViewModel`: `Categoria` → `CategoriaNombre` |
| **Call sites** | Migración completa de `Categoria` string a `CategoriaId`/`CategoriaNombre` en PageModels, Razor Pages, ApiClient y tests |

## Comandos Ejecutados

| Comando | Resultado |
|---------|-----------|
| `dotnet test SGV.slnx` | **2,891/2,891 PASS** (0 Failed, 0 Skipped) |
| `bun install && bun run build` (src/SGV.Web) | ✅ Sin errores (solo warnings de cache) |
| `dotnet build SGV.slnx` | ✅ Build exitoso |

## Criterios de Aceptación (de proposal.md)

| AC | Verificación |
|----|--------------|
| Migración limpia; 7 habilidades resueltas o NULL | Backfill implementado vía LOWER() JOIN + auditoría de transiciones NULL. Tests MySqlFact validan escenarios. |
| Sin `Habilidades.Categoria`; FK e índice presentes | Migración: DropColumn Categoria, FK Restrict, IX_Habilidades_CategoriaId. Tests estructurales post-migración. |
| `dotnet test SGV.slnx` pase | **2,891/2,891 PASS** ✅ |
| `bun run build` pase | ✅ |
| Docs actualizados | `docs/decisiones-implementacion.md` § líneas 116-117 (bloque 72000000-…), §722-769 (Migración CategoriasHabilidad) |
