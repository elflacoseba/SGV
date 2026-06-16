# Tasks: Replace `UnidadOrganizativa.TipoUnidad` (string) with FK to `TipoUnidadOrganizativa` catalog

> **Change:** `cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa`
> **Phase:** `sdd-tasks`
> **Strict TDD:** `true` (red → green for every functional task)

---

## 1. Executive Summary

This file decomposes the change into **3 chained PRs** (Feature Branch Chain over `develop`, tracker branch `feature/tipo-unidad-organizativa`) for a total of ~830 lines. The change promotes `UnidadOrganizativa.TipoUnidad` (free-form `varchar(50)`) into a `Guid` FK referencing a new immutable catalog `TiposUnidadOrganizativa`, with a fail-loud migration, updated application contracts, and a new read-only API endpoint.

- **PR #1** (~400 líneas, ~12 tasks): Foundation — new domain entity, persistence entity + config, mapper updates, migration (expand-contract 6 steps + fail-loud), seed constants, idempotent SQL script regeneration, domain + persistence tests. **Repo queda en estado degradado** (tests de Aplicación fallan por referencias a `string TipoUnidad`).
- **PR #2** (~250 líneas, ~7 tasks): Application layer — requests, DTO, command service (FK validation), query service, new catalog service+repo, DI registration, application tests. `dotnet build` + `dotnet test` verdes.
- **PR #3** (~180 líneas, ~4 tasks): API controller + API tests + 5th spec delta (sgv-persistence-architecture). Chain completo, listo para merge del tracker a `develop`.

**Gate:** Review Workload Guard pre-aprobado (el usuario ya decidió chained PRs, no se detiene).

**Design gaps detected and documented:**
1. `ReadOnlyRepository<TP, TD>` requires `TP : AuditableEntityBase` / `TD : EntidadAuditable`, but `TipoUnidadOrganizativaEntity : EntityBase` / `TipoUnidadOrganizativa : EntidadBase`. The repository must implement `IReadOnlyRepository<TipoUnidadOrganizativa>` directly (see Task 1.3, Open Questions).
2. Migration name per design is `ReemplazarTipoUnidadPorCatalogo`, not `ReemplazarTipoUnidadPorFkASuTablaCatalogo`.

---

## 2. PR #1 — Foundation (Domain + Persistence + Migration)

- **Branch:** `feature/tipo-unidad-organizativa` (desde `develop`)
- **Rama destino del PR:** tracker branch (se crea en sdd-apply)
- **Dependencias:** ninguna (es el primer PR)
- **Estimado:** ~400 líneas
- **Estado post-PR1:** `dotnet build` pasa, `dotnet test` falla en tests de Aplicación que referencian `TipoUnidad` string (los tests de Dominio y Persistencia verdes). El servicio `UnidadOrganizativaServicioComandos` aún tiene el parámetro `string tipoUnidad` — el repo queda en estado degradado hasta PR 2.

### Task 1.1: Crear entidad de dominio `TipoUnidadOrganizativa`

- **RED:** Tests de dominio primero.
- **Archivos:**
  - **NEW** `tests/SGV.Tests/Dominio/Organizacion/TipoUnidadOrganizativaTests.cs`
- **Implementación:**
  - **NEW** `src/SGV.Dominio/Organizacion/TipoUnidadOrganizativa.cs`
- **Contenido:** Clase sellada `TipoUnidadOrganizativa : EntidadBase` con:
  - Ctor público `(string codigo, string nombre)` que valida via `ValidacionesDominio.Requerido`
  - Ctor privado sin parámetros para EF materialization
  - `public string Codigo { get; private set; }` (max 50)
  - `public string Nombre { get; private set; }` (max 100)
  - Sin `CambiarDatos` (inmutable)
- **Tests:**
  - `Crear_ConCodigoYNombreValidos_AsignaPropiedades`
  - `Crear_ConCodigoVacio_ThrowsArgumentException`
  - `Crear_ConNombreVacio_ThrowsArgumentException`
  - `Crear_ConCodigoMayorA50_ThrowsArgumentException`
  - `Crear_ConNombreMayorA100_ThrowsArgumentException`
  - `TipoUnidadOrganizativa_NoExponeSetterPublico` (compile-time assertion)
- **Criterio:** test crea instancia válida, test rechaza argumentos nulos/vacíos/longitud excesiva.

### Task 1.2: Modificar `UnidadOrganizativa` (dominio)

- **RED:** Tests de dominio para la entidad modificada.
- **Archivos:**
  - **NEW** `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs`
- **Implementación:**
  - **MODIFIED** `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs`
- **Contenido:**
  - Reemplazar `public string TipoUnidad { get; private set; }` por `public Guid TipoUnidadOrganizativaId { get; private set; }`
  - Agregar nav: `public TipoUnidadOrganizativa? TipoUnidadOrganizativa { get; private set; }`
  - Constructor: cambiar parámetro `string tipoUnidad` → `Guid tipoUnidadOrganizativaId`
  - `CambiarDatos`: cambiar parámetro `string tipoUnidad` → `Guid tipoUnidadOrganizativaId`
  - Validación inline: `if (tipoUnidadOrganizativaId == Guid.Empty) throw new ArgumentException(...)` (AD-7)
  - Cuerpo del constructor llama a `CambiarDatos(codigo, nombre, tipoUnidadOrganizativaId)`
- **Tests:**
  - `Crear_ConTipoUnidadOrganizativaIdNoVacio_AsignaPropiedad`
  - `Crear_ConTipoUnidadOrganizativaIdVacio_ThrowsArgumentException`
  - `CambiarDatos_ConTipoUnidadOrganizativaIdNoVacio_AsignaPropiedad`
  - `CambiarDatos_ConTipoUnidadOrganizativaIdVacio_ThrowsArgumentException`
- **Criterio:** validar `Guid.Empty` (analogía con `ValidacionesDominio.Requerido`). Mantener el ctor privado sin parámetros para EF.

> **Note:** `tests/SGV.Tests/Dominio/` folder does NOT exist today — PR 1 creates it.

### Task 1.3: Crear entidad EF `TipoUnidadOrganizativaEntity` + configuración + repositorio

- **Archivos:**
  - **NEW** `src/SGV.Infraestructura/Persistencia/Entidades/TipoUnidadOrganizativaEntity.cs` (hereda de `EntityBase`, NO de `AuditableEntityBase`)
  - **NEW** `src/SGV.Infraestructura/Persistencia/Configuraciones/TipoUnidadOrganizativaConfiguracion.cs`
  - **NEW** `src/SGV.Infraestructura/Persistencia/Repositorios/TipoUnidadOrganizativaRepository.cs`
- **Tests:**
  - **NEW** `tests/SGV.Tests/Persistencia/TipoUnidadOrganizativaRepositoryTests.cs` (tests con `[MySqlFact]` — se saltan si no hay MySQL)
- **Contenido Entity:**

  ```csharp
  public sealed class TipoUnidadOrganizativaEntity : EntityBase
  {
      public string Codigo { get; set; } = string.Empty;
      public string Nombre { get; set; } = string.Empty;
  }
  ```

- **Contenido Configuración:** `ToTable("TiposUnidadOrganizativa")`, `ConfigurarId()`, `Codigo` como `varchar(50)` required con collation `ascii_general_ci`, `Nombre` como `varchar(100)` required, `HasIndex(Codigo).IsUnique()`. Sin `IsActive`/`IsDeleted`. CheckConstraint `CK_TiposUnidadOrganizativa_Codigo`.
- **Contenido Repository:** implementa `IReadOnlyRepository<TipoUnidadOrganizativa>` directamente (NO puede extender `ReadOnlyRepository<TP, TD>` porque la constraint exige `AuditableEntityBase`/`EntidadAuditable`). Usa `SgvDbContext` + `AsNoTracking` + `PersistenceToDomainMapper.ToDomain(TipoUnidadOrganizativaEntity)`.
- **Criterio:** schema coincide con `NivelesHabilidad` pattern. Repository retorna datos desde la BD.

> **⚠ Design gap:** `ReadOnlyRepository<TPersistence, TDomain>` has `where TPersistence : AuditableEntityBase` / `where TDomain : EntidadAuditable`. `TipoUnidadOrganizativaEntity : EntityBase` (not `AuditableEntityBase`), so a direct implementation of `IReadOnlyRepository<TipoUnidadOrganizativa>` is needed instead. This is expected to be ~40-50 lines.

### Task 1.4: Crear `internal static class TipoUnidadOrganizativaConstantes`

- **Archivos:**
  - **NEW** `src/SGV.Infraestructura/Persistencia/Catalogos/TipoUnidadOrganizativaConstantes.cs`
- **Tests:**
  - **NEW** `tests/SGV.Tests/Persistencia/TipoUnidadOrganizativaConstantesTests.cs`
- **Contenido:** 7 propiedades estáticas con Guids del bloque `60000000-0000-0000-0000-00000000000{1-7}`:

  | Propiedad | Guid |
  |-----------|------|
  | `InstitucionId` | `60000000-0000-0000-0000-000000000001` |
  | `FacultadId` | `60000000-0000-0000-0000-000000000002` |
  | `SecretariaId` | `60000000-0000-0000-0000-000000000003` |
  | `DireccionId` | `60000000-0000-0000-0000-000000000004` |
  | `DepartamentoId` | `60000000-0000-0000-0000-000000000005` |
  | `DivisionId` | `60000000-0000-0000-0000-000000000006` |
  | `AreaId` | `60000000-0000-0000-0000-000000000007` |

- **Criterio:** el test verifica que las constantes tienen Guids únicos y no vacíos. El folder `Catalogos/` se crea por primera vez.

### Task 1.5: Modificar `UnidadOrganizativaEntity` (persistencia) + `UnidadOrganizativaConfiguracion`

- **Archivos:**
  - **MODIFIED** `src/SGV.Infraestructura/Persistencia/Entidades/UnidadOrganizativaEntity.cs`
  - **MODIFIED** `src/SGV.Infraestructura/Persistencia/Configuraciones/UnidadOrganizativaConfiguracion.cs`
- **Contenido Entity:** Reemplazar `string TipoUnidad { get; set; }` por `Guid TipoUnidadOrganizativaId { get; set; }`. Agregar `public virtual TipoUnidadOrganizativaEntity? TipoUnidadOrganizativa { get; set; }`.
- **Contenido Configuración:**
  - Remover `builder.Property(e => e.TipoUnidad).HasMaxLength(50).IsRequired();`
  - Agregar FK con `OnDelete(DeleteBehavior.Restrict)` (mirar `CargoHabilidadConfiguracion.cs:27-30`)
  - Agregar índice `IX_UnidadesOrganizativas_TipoUnidadOrganizativaId`
- **Dependencias:** Requiere que Task 1.3 (entidad `TipoUnidadOrganizativaEntity`) exista.
- **Criterio:** la entidad tiene `TipoUnidadOrganizativaId` Guid + nav virtual, la configuración tiene FK Restrict + índice.

### Task 1.6: Agregar método `ToDomain(TipoUnidadOrganizativaEntity)` a `PersistenceToDomainMapper`

- **Archivos:**
  - **MODIFIED** `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs`
- **Contenido:** Nuevo método:

  ```csharp
  public static TipoUnidadOrganizativa ToDomain(TipoUnidadOrganizativaEntity entity)
  {
      var tipo = new TipoUnidadOrganizativa(entity.Codigo, entity.Nombre) { Id = entity.Id };
      return tipo;
  }
  ```

  (No hay propiedades de auditoría que mapear — `EntityBase` no tiene `CreatedAt`/`IsDeleted`.)

- **Criterio:** el mapper convierte el entity a dominio correctamente.

### Task 1.7: Actualizar mappers bidireccionales para `UnidadOrganizativa`

- **Archivos:**
  - **MODIFIED** `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs`
  - **MODIFIED** `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs`
- **Contenido `DomainToPersistenceMapper`:**
  - `ToEntity`: cambiar `TipoUnidad = domain.TipoUnidad` → `TipoUnidadOrganizativaId = domain.TipoUnidadOrganizativaId`
  - `UpdateEntity`: cambiar `entity.TipoUnidad = domain.TipoUnidad` → `entity.TipoUnidadOrganizativaId = domain.TipoUnidadOrganizativaId`
- **Contenido `PersistenceToDomainMapper.ToDomain(UnidadOrganizativaEntity)`:**
  - Cambiar ctor de `new UnidadOrganizativa(entity.Codigo, entity.Nombre, entity.TipoUnidad, ...)` a `new UnidadOrganizativa(entity.Codigo, entity.Nombre, entity.TipoUnidadOrganizativaId, ...)`
  - Cambiar `CambiarDatos(entity.Codigo, entity.Nombre, entity.TipoUnidad, ...)` a `CambiarDatos(entity.Codigo, entity.Nombre, entity.TipoUnidadOrganizativaId, ...)`
  - Agregar nav mapping: si `entity.TipoUnidadOrganizativa is not null`, setear nav via `SetProperty(unidad, nameof(UnidadOrganizativa.TipoUnidadOrganizativa), ToDomain(entity.TipoUnidadOrganizativa))` (mirar patrón `UnidadPadre` L69-72)
- **Criterio:** los mappers compilan y los tests de persistencia existentes pasan con el nuevo mapeo.

### Task 1.8: Agregar seed de `TipoUnidadOrganizativa` a `DatosSemilla.cs`

- **Archivos:**
  - **MODIFIED** `src/SGV.Infraestructura/Persistencia/DatosSemilla.cs`
- **Tests:**
  - **NEW** `tests/SGV.Tests/Persistencia/DatosSemillaTests.cs` — test que asserta que los Ids en `DatosSemilla.HasData` coinciden con `TipoUnidadOrganizativaConstantes` (7 Ids, mismos valores)
- **Contenido:** Agregar método interno `ConfigurarTipoUnidadOrganizativa` (o inline block) con:

  ```csharp
  builder.Entity<TipoUnidadOrganizativaEntity>().HasData(
      new TipoUnidadOrganizativaEntity { Id = TipoUnidadOrganizativaConstantes.InstitucionId, Codigo = "Institucion", Nombre = "Institución" },
      // ... 6 más
  );
  ```

- **Criterio:** seed idempotente, Guids iguales entre migración y `HasData`. El test `DatosSemilla_SeedIdsMatchTipoUnidadOrganizativaConstantes` pasa.

### Task 1.9: Actualizar `RepositoryTestData` + tests de persistencia existentes

- **Archivos:**
  - **MODIFIED** `tests/SGV.Tests/Persistencia/RepositoryTestData.cs`
  - **MODIFIED** `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs`
- **Contenido `RepositoryTestData`:** L19 — cambiar `TipoUnidad = "TEST"` por `TipoUnidadOrganizativaId = TipoUnidadOrganizativaConstantes.AreaId`. Agregar `using SGV.Infraestructura.Persistencia.Catalogos;`.
- **Contenido `UnidadOrganizativaRepositoryTests`:** Actualizar 3 referencias a `"TEST"` / `"NUEVO_TIPO"` para usar `TipoUnidadOrganizativaConstantes.AreaId` / `.DireccionId` según el diseño.
- **Criterio:** los tests de persistencia existentes compilan y pasan (los `[MySqlFact]` se saltan si no hay MySQL local).

### Task 1.10: Crear migración EF Core (expand-contract 6 pasos)

- **Comando:**

  ```bash
  dotnet ef migrations add ReemplazarTipoUnidadPorCatalogo \
      --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
      --output-dir Persistencia/Migraciones
  ```

- **Archivos:**
  - **NEW** `<timestamp>_ReemplazarTipoUnidadPorCatalogo.cs` + `.Designer.cs` (generados)
  - **MODIFIED** `SgvDbContextModelSnapshot.cs` (EF lo regenera automáticamente)
- **Contenido `Up` — 6 pasos exactos:**

  1. `CreateTable` para `TiposUnidadOrganizativa` con PK Guid, unique index, check constraint
  2. `InsertData` con 7 filas usando Guids de `TipoUnidadOrganizativaConstantes`
  3. Fail-loud pre-flight: `CREATE TEMPORARY TABLE _DirtyTiposUnidad`, `IF @has_dirty > 0 THEN SIGNAL SQLSTATE '45000'`, `DROP TEMPORARY TABLE`
  4. `AddColumn` nullable `TipoUnidadOrganizativaId` + `CreateIndex`
  5. Backfill `UPDATE UnidadesOrganizativas u INNER JOIN TiposUnidadOrganizativa t ON t.Codigo = u.TipoUnidad SET u.TipoUnidadOrganizativaId = t.Id`
  6. `AlterColumn NOT NULL` + `AddForeignKey Restrict` + `DropColumn TipoUnidad`
- **Contenido `Down` — reversible:**
  1. Re-add `TipoUnidad` nullable + backfill JOIN inverso
  2. `DropForeignKey`, `DropIndex`, `DropColumn TipoUnidadOrganizativaId`
  3. `DropTable TiposUnidadOrganizativa`
- **Tests:**
  - **NEW** `tests/SGV.Tests/Persistencia/MigracionFailLoudTests.cs` con `[MySqlFact]`:
    - `ApplyMigration_LimpiaBackfill_CreaTablaYBackfillea` — migración limpia completa
    - `ApplyMigration_DatosSucios_ThrowsExceptionYAbortaAntesDeAlterar` — datos sucios abortan
- **Criterio:** aplicar migración en BD limpia (seeds presentes), aplicar sobre datos existentes limpios (backfill completo), aplicar sobre datos sucios (fail-loud con `MySqlException`). El `Down` restaura todo.

> **Nota sobre `SIGNAL SQLSTATE '45000':`** EF Core no puede `throw` dentro de un batch SQL. `SIGNAL` es el mecanismo estándar de MySQL para abortar una transacción. Los tests que verifican esto necesitan una BD real (por eso `[MySqlFact]`).

### Task 1.11: Regenerar script SQL idempotente

- **Comando:**

  ```bash
  dotnet ef migrations script \
      --project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
      --startup-project src/SGV.Infraestructura/SGV.Infraestructura.csproj \
      --idempotent \
      --output docs/migracion-inicial-sgv.sql
  ```

- **Archivos:**
  - **MODIFIED** `docs/migracion-inicial-sgv.sql` (regenerado completo)
- **Criterio:** el script regenerado contiene la nueva tabla `TiposUnidadOrganizativa`, la FK `TipoUnidadOrganizativaId` con `OnDelete(Restrict)`, y los seeds con Guids estáticos. Verificar con `grep` por `TiposUnidadOrganizativa` y `TipoUnidadOrganizativaId`. No commitear si hay drift entre el script y la migración.

### Task 1.12: `dotnet build` + `dotnet test` de PR1

- **Verificación:**
  - `dotnet build` pasa sin errores.
  - `dotnet test` — tests de Dominio (Task 1.1, 1.2) verdes, tests de Persistencia (Task 1.3, 1.9, 1.10) verdes o skip graceful.
  - Tests de Aplicación pueden fallar por referencias a `string TipoUnidad` en `UnidadOrganizativaServicioComandosTests.cs` y `UnidadOrganizativaServicioConsultaTests.cs` (estado degradado esperado).
- **Commit messages:** `feat(org): add TipoUnidadOrganizativa catalog entity, EF migration, and persistence tests` + tasks individuales como commits separados.

---

## 3. PR #2 — Application Layer

- **Branch:** desde branch de PR #1
- **Rama destino del PR:** branch de PR #1
- **Dependencias:** PR #1 mergeado al tracker
- **Estimado:** ~250 líneas
- **Estado post-PR2:** `dotnet build` + `dotnet test` verdes. API aún no expone el endpoint GET del catálogo.

### Task 2.1: Modificar `CrearUnidadOrganizativaRequest` y `ActualizarUnidadOrganizativaRequest`

- **RED:** Los tests existentes del command service fallan porque los requests cambian de signatura.
- **Archivos:**
  - **MODIFIED** `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaRequests.cs`
- **Contenido:**
  - `CrearUnidadOrganizativaRequest`: reemplazar `string TipoUnidad` por `Guid TipoUnidadOrganizativaId`
  - `ActualizarUnidadOrganizativaRequest`: reemplazar `string TipoUnidad` por `Guid TipoUnidadOrganizativaId`
  - `CambiarUnidadPadreRequest` no se toca
- **Criterio:** los requests cambian de `string` a `Guid` para el tipo de unidad.

### Task 2.2: Modificar `UnidadOrganizativaServicioComandos`

- **RED:** Test nuevo para FK inexistente.
- **Archivos:**
  - **MODIFIED** `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs`
- **Tests:**
  - **MODIFIED** `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs`
- **Contenido:**
  - Agregar `ITipoUnidadOrganizativaRepository _tipoRepo` como dependencia del constructor
  - En `CrearAsync`: después del check de código duplicado y antes de construir `UnidadOrganizativa`, resolver el `TipoUnidadOrganizativaId` contra el catálogo. Si no existe, retornar `Validation` con error `TipoUnidadNoExiste`
  - En `ActualizarAsync`: mismo check antes de `CambiarDatos`
  - Actualizar `MapToDto` para exponer `TipoUnidadOrganizativaId` y `TipoUnidadNombre`
  - 4 referencias a `string TipoUnidad` cambiar a `TipoUnidadOrganizativaId`
- **Tests modificados:**
  - L20, L78: `"Dirección"` → `TipoUnidadOrganizativaConstantes.DireccionId` (Guid)
  - L96, L114, L129: `"Área"`, `"T"` → `TipoUnidadOrganizativaConstantes.AreaId`
  - L230-235: helper `CrearUnidadActiva` actualizado para recibir `Guid tipoUnidadId`
  - **Nuevo test:** `CrearAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar`
  - **Nuevo test:** `ActualizarAsync_TipoUnidadNoExiste_RetornaValidacionYSinGuardar`
- **Criterio:** las llamadas existentes usan Guids de las constantes. Nuevos tests verifican que FK inexistente da `400`/`Validation`.

### Task 2.3: Modificar `UnidadOrganizativaDto` y `UnidadOrganizativaServicioConsulta`

- **Archivos:**
  - **MODIFIED** `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/UnidadOrganizativaDto.cs`
  - **MODIFIED** `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs`
- **Tests:**
  - **MODIFIED** `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs`
  - **MODIFIED** `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs`
- **Contenido DTO:** Reemplazar `string TipoUnidad` por `Guid TipoUnidadOrganizativaId` + `string TipoUnidadNombre`. Nuevo record signature ordena: `Id, Codigo, Nombre, TipoUnidadOrganizativaId, TipoUnidadNombre, Descripcion, VigenteDesde, VigenteHasta, UnidadPadreId`.
- **Contenido Servicio Consulta:** `MapToDto` lee `entity.TipoUnidadOrganizativa?.Nombre ?? string.Empty` (defensivo contra nav no cargada).
- **Tests modificados:**
  - `UnidadOrganizativaServicioConsultaTests`: cambiar aserción de `Assert.Equal(unidad.TipoUnidad, dto.TipoUnidad)` a aserción sobre `TipoUnidadOrganizativaId` y `TipoUnidadNombre`. El FakeRepository debe poblar la nav.
  - `PuestoServicioConsultaTests.L17`: `new UnidadOrganizativa("GER", "Gerencia General", "Dirección")` → `new UnidadOrganizativa("GER", "Gerencia General", TipoUnidadOrganizativaConstantes.DireccionId)`
- **Criterio:** el DTO nuevo tiene los 2 campos, el viejo `TipoUnidad` desapareció. La consulta retorna `TipoUnidadNombre` denormalizado.

### Task 2.4: Modificar `UnidadOrganizativaRepository` (eager-load Include)

- **Archivos:**
  - **MODIFIED** `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs`
- **Contenido:** Extender la propiedad `Query` (L12-14) para agregar `.Include(u => u.TipoUnidadOrganizativa)` — mirror de `PuestoRepository.Query` L15-16.
- **Criterio:** el `Include` asegura que la nav `TipoUnidadOrganizativa` se carga en la misma consulta JOIN, evitando N+1.

### Task 2.5: Crear `ITipoUnidadOrganizativaRepository` + modificar `TipoUnidadOrganizativaRepository` para implementarla

- **Archivos:**
  - **NEW** `src/SGV.Aplicacion/Organizacion/Consultas/ITipoUnidadOrganizativaRepository.cs`
  - **MODIFIED** `src/SGV.Infraestructura/Persistencia/Repositorios/TipoUnidadOrganizativaRepository.cs` (agregar `: ITipoUnidadOrganizativaRepository`)
- **Contenido Interface:**

  ```csharp
  public interface ITipoUnidadOrganizativaRepository : IReadOnlyRepository<TipoUnidadOrganizativa>
  {
  }
  ```

- **Criterio:** `TipoUnidadOrganizativaRepository` implementa `ITipoUnidadOrganizativaRepository` (la declaración de clase se actualiza).

### Task 2.6: Crear `ITipoUnidadOrganizativaServicioConsulta` + `TipoUnidadOrganizativaServicioConsulta`

- **RED:** Test para el nuevo servicio de consulta.
- **Archivos:**
  - **NEW** `src/SGV.Aplicacion/Organizacion/Consultas/ITipoUnidadOrganizativaServicioConsulta.cs`
  - **NEW** `src/SGV.Aplicacion/Organizacion/Consultas/TipoUnidadOrganizativaServicioConsulta.cs`
  - **NEW** `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/TipoUnidadOrganizativaDto.cs`
- **Tests:**
  - **NEW** `tests/SGV.Tests/Aplicacion/Organizacion/TipoUnidadOrganizativaServicioConsultaTests.cs`
- **Contenido DTO:** `TipoUnidadOrganizativaDto(Guid Id, string Codigo, string Nombre)` — tres campos, sin auditoría.
- **Contenido Interface:** `ListAsync` + `GetByIdAsync`.
- **Contenido Servicio:** Copia estructura de `CargoServicioConsulta` con tipos intercambiados. Mapea `entity.Codigo` y `entity.Nombre`.
- **Tests:** Mirror de `CargoServicioConsultaTests` con un `FakeTipoUnidadOrganizativaRepository` de 7 filas.
- **Criterio:** `ObtenerTodosAsync()` retorna la lista completa; `ObtenerPorIdAsync()` retorna el elemento o null.

### Task 2.7: Registrar DI para nuevo repositorio y servicio

- **Archivos:**
  - **MODIFIED** `src/SGV.Infraestructura/DependencyInjection.cs`
- **Contenido:**
  - En la sección de Repositories (L25-28): `services.AddScoped<ITipoUnidadOrganizativaRepository, TipoUnidadOrganizativaRepository>();`
  - En la sección de Query services (L30-34): `services.AddScoped<ITipoUnidadOrganizativaServicioConsulta, TipoUnidadOrganizativaServicioConsulta>();`
  - El command service `UnidadOrganizativaServicioComandos` se resuelve automáticamente (nueva dependencia via ctor).
- **Criterio:** `dotnet build` no da error de resolución de dependencias.

### Task 2.8: `dotnet build` + `dotnet test` de PR2

- **Verificación:**
  - `dotnet build` pasa.
  - `dotnet test` — todos los tests de Aplicación pasan (incluyendo los nuevos tests de FK inexistente).
  - Tests de Dominio y Persistencia siguen verdes (no se modificaron en PR2).

---

## 4. PR #3 — API + persistence-architecture delta

- **Branch:** desde branch de PR #2
- **Rama destino del PR:** branch de PR #2
- **Dependencias:** PR #2 mergeado al tracker
- **Estimado:** ~180 líneas
- **Estado post-PR3:** ✅ `dotnet build` + `dotnet test` verdes (144 tests). Los 3 PRs listos para merge final del tracker a `develop`.

### Task 3.1: Crear `TipoUnidadesOrganizativasController` ✅

- **RED:** Tests de API primero.
- **Archivos:**
  - **NEW** `tests/SGV.Tests/Api/TipoUnidadesOrganizativasControllerTests.cs`
- **Implementación:**
  - **NEW** `src/SGV.Api/Controllers/TipoUnidadesOrganizativasController.cs`
- **Contenido Controller:**
  - `[Route("api/v1/tipos-unidad-organizativa")]`, `[ApiController]`, `[AllowAnonymous]`
  - Inyecta `ITipoUnidadOrganizativaServicioConsulta`
  - `[HttpGet]` → `Ok(_servicio.ListAsync(ct))`
  - `[HttpGet("{id:guid}")]` → `_servicio.GetByIdAsync(id, ct)` → `Ok(dto)` o `NotFound()`
  - Sin POST/PUT/PATCH/DELETE (405 implícito por ASP.NET Core)
- **Tests:**
  - `GetAll_Returns200With7SeedDtos` (REQ-TUO-002)
  - `GetAll_NoAuth_Returns200` (sin `[Authorize]`)
  - `GetById_Existing_Returns200WithDto` (REQ-TUO-003)
  - `GetById_NonExistent_Returns404`
  - `GetById_InvalidGuid_Returns400`
  - `Dto_Shape_OnlyIdCodigoNombre` (REQ-TUO-004)
- **Criterio:** 200 con lista vacía/poblada, 200/404/400 según el caso. Response body tiene solo `id`, `codigo`, `nombre`.

### Task 3.2: Modificar `ApiWebApplicationFactory.cs` y `UnidadesOrganizativasControllerTests.cs` ✅

- **Archivos:**
  - **MODIFIED** `tests/SGV.Tests/Api/ApiWebApplicationFactory.cs`
  - **MODIFIED** `tests/SGV.Tests/Api/UnidadesOrganizativasControllerTests.cs`
- **Contenido `ApiWebApplicationFactory`:**
  - L122, L134: el `FakeUnidadOrganizativaServicioComandos` debe aceptar `request.TipoUnidadOrganizativaId` y sintetizar un `TipoUnidadNombre` (ej. `"Facultad"`)
  - Registrar `FakeTipoUnidadOrganizativaServicio` que retorna los 7 seed DTOs
- **Contenido `UnidadesOrganizativasControllerTests`:**
  - L128: `tipoUnidad = "Área"` → `tipoUnidadId = TipoUnidadOrganizativaConstantes.AreaId`
  - L195: `tipoUnidad = "Dirección"` → `tipoUnidadId = TipoUnidadOrganizativaConstantes.DireccionId`
  - Assert `dto.TipoUnidadOrganizativaId` y `dto.TipoUnidadNombre`
- **Criterio:** todos los tests de API existentes pasan con el nuevo contrato.

### Task 3.3: Verificar/crear 5° delta `sgv-persistence-architecture/spec.md` ✅

- **Archivos:**
  - **VERIFY** `openspec/changes/cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/specs/sgv-persistence-architecture/spec.md` (debe existir — creado por `sdd-design`)
- **Contenido esperado:** `REQ-SPA-EVOLUTION-001` con las 4 condiciones del carve-out (catalog read-only, FK `OnDelete(Restrict)`, migration fail-loud, static Guid constants). Si el archivo no existe en PR3, crearlo con el contenido del diseño.
- **Criterio:** el delta existe, referencia el change por nombre, y tiene `Status: ADDED`. Si el orquestador ya lo creó, solo verificar. Si no, crearlo.

### Task 3.4: `dotnet build` + `dotnet test` de PR3 ✅

- **Verificación:**
  - `dotnet build` pasa.
  - `dotnet test` — todos los tests pasan (API, Aplicación, Dominio, Persistencia).
  - El chain está completo. Tracker listo para merge a `develop`.

---

## 5. Review Workload Forecast

| Item | Value |
|------|-------|
| **Total estimated lines** | ~830 (PR1 ~400, PR2 ~250, PR3 ~180) |
| **Chained PRs recommended** | Yes (pre-approved by user) |
| **400-line budget risk** | PR1 borderline (~400). If `wc -l` real > 400, move Task 1.8 (seed) + Task 1.9 (test data) to PR2. |
| **Decision needed before apply** | No (pre-approved: 3-PR chain, feature-branch-chain over `develop`, tracker `feature/tipo-unidad-organizativa`) |

---

## 6. Dependencies & Sequencing

```
develop ──┬── feature/tipo-unidad-organizativa (tracker, draft/no-merge)
          │         │
          │         ├── PR #1 branch ────▶ PR #1 ────▶ merge to tracker
          │         │         │
          │         │         └── PR #2 branch ────▶ PR #2 ────▶ merge to tracker
          │         │                        │
          │         │                        └── PR #3 branch ────▶ PR #3 ────▶ merge to tracker
          │         │                                                      │
          │         └──────────────────────────────────────────────────── tracker merge ──▶ develop
          │
          └── (other work)
```

- **PR1** → **PR2** → **PR3** (estrictamente secuencial, cada PR mergea al tracker).
- **Tracker merge a `develop`** solo cuando los 3 PRs están verdes y mergeados.
- **Rollback** individual por PR vía revert (el PR individual revierte sin afectar los demás).

---

## 7. Risks & Open Questions

### Risks

| # | Risk | Mitigation |
|---|------|------------|
| R1 | **PR1 en el límite de 400 líneas.** | Si `wc -l` real da > 400, rebalancear moviendo Task 1.8 (seed constants test) + Task 1.9 (test data update) a PR2. |
| R2 | **`dotnet test` sin MySQL local salta `[MySqlFact]`.** | Los tests de persistencia no dan señal completa sin BD. Documentar en commit message de PR1. CI matrix ejecuta `mysql:8` como service. |
| R3 | **Migración fail-loud con `SIGNAL SQLSTATE '45000'`** no es capturable desde C# en tests unitarios. | El test `ApplyMigration_DatosSucios_ThrowsExceptionYAborta` es `[MySqlFact]` y necesita BD real. Sin BD, se salta. |
| R4 | **Chained PRs: PR1 deja el repo degradado** (la API y comandos no compilan hasta PR2). | Tracker branch protege `develop`. No mergear tracker hasta que PR3 esté listo. |
| R5 | **Drift entre migration seed y `DatosSemilla`.** | `TipoUnidadOrganizativaConstantes` es single source of truth. Test `DatosSemilla_SeedIdsMatchTipoUnidadOrganizativaConstantes` asserta igualdad. |

### Open Questions

| # | Question | Status |
|---|----------|--------|
| OQ-1 | **Repository base class constraint gap.** `ReadOnlyRepository<TPersistence, TDomain>` requires `TPersistence : AuditableEntityBase` and `TDomain : EntidadAuditable`. `TipoUnidadOrganizativaEntity` inherits `EntityBase`, and `TipoUnidadOrganizativa` inherits `EntidadBase`. The design assumes `TipoUnidadOrganizativaRepository` can extend `ReadOnlyRepository`, but this is not possible. | **Resolved in tasks:** Repository directly implements `IReadOnlyRepository<TipoUnidadOrganizativa>` without the base class. ~40-50 lines instead of ~25. |
| OQ-2 | **Migration name discrepancy.** The instructions template mentions `ReemplazarTipoUnidadPorFkASuTablaCatalogo`, but the design (line 309) and proposal (line 52) both use `ReemplazarTipoUnidadPorCatalogo`. | **Resolved:** Use design/proposal name `ReemplazarTipoUnidadPorCatalogo`. |
| OQ-3 | **Does `SwaggerConfigurationTests.cs` need modification?** The design mentions it conditionally (line 868). Swagger discovers controllers automatically via reflection, but if a test asserts specific paths, it may need updating. | Verify during apply. If no path-assertion test exists, no change needed. |
| OQ-4 | **Is `TipoUnidadOrganizativaConstantesTests` strictly needed?** The design doesn't mention a specific test file for constants uniqueness, but it's a safety net. | **Resolved in tasks:** Added `TipoUnidadOrganizativaConstantesTests.cs` (Task 1.4) — simple uniqueness check. |
| OQ-5 | **Does `PersistenceToDomainMapper` need a `ToDomain(TipoUnidadOrganizativaEntity)` method?** The design focuses on modifying the `UnidadOrganizativa` mapping but doesn't explicitly add this method, which is needed by the repository. | **Resolved in tasks:** Added Task 1.6 for the new mapper method. |

---

## References cruzadas con proposal/specs/design

- **Proposal:** `openspec/changes/cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/proposal.md`
- **Design:** `openspec/changes/cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/design.md`
- **Specs (5 deltas):**
  - `openspec/changes/cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/specs/tipo-unidad-organizativa-catalog/spec.md`
  - `openspec/changes/cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/specs/unidad-organizativa-crud/spec.md`
  - `openspec/changes/cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/specs/sgv-database/spec.md`
  - `openspec/changes/cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/specs/sgv-readonly-api/spec.md`
  - `openspec/changes/cambiar-campo-tipounidad-a-tabla-tipounidadorganizativa/specs/sgv-persistence-architecture/spec.md`
- **Precedentes en repo:**
  - `src/SGV.Dominio/Habilidades/NivelHabilidad.cs` — catalog entity precedent
  - `src/SGV.Infraestructura/Persistencia/Configuraciones/NivelHabilidadConfiguracion.cs` — catalog config precedent
  - `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoHabilidadConfiguracion.cs:27-30` — FK `OnDelete(Restrict)` precedent
  - `src/SGV.Infraestructura/Persistencia/Repositorios/ReadOnlyRepository.cs` — base repository (constraint gap documented)
  - `src/SGV.Api/Controllers/CargosController.cs` — read-only API controller precedent
