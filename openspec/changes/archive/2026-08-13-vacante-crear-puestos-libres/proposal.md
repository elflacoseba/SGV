# Proposal: vacante-crear-puestos-libres

## Intent

Hoy el dropdown de puestos en `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs:232` consume `GET /api/v1/puestos` — que devuelve **todos los puestos activos** — y delega la validación N1 (`PuestoOcupado`) al POST del formulario. Esto produce un 409 Conflict post-factum, con fricción de UX. La propuesta filtra el dropdown para mostrar exclusivamente **"puestos disponibles"**: aquellos sin Ocupación vigente (`EsVigente = true`, `IsDeleted = 0`) **y** sin Vacante Abierta (`FechaCierre IS NULL`, `IsDeleted = 0`). La validación backend existente (N1 + constraint `ActivePuestoIdUnique`) se mantiene intacta como fuente de verdad; el cambio es mejora proactiva de UX.

## Background

### Estado actual

El call site funcional de `ListarPuestosAsync` (exploration.md §"Hallazgo 1") es **únicamente** `Create.cshtml.cs:232`. Los demás sitios son tests con fakes.

| Componente | Método | Comportamiento |
|-----------|--------|----------------|
| `IVacanteApiClient.ListarPuestosAsync` | `GET /api/v1/puestos` | Todos los activos (`IsActive`, `!IsDeleted`) |
| `PuestosController.GetAll()` | `GET /api/v1/puestos` | Delegado puro → `IPuestoServicioConsulta.ListAsync()` |
| `PuestoServicioConsulta.ListAsync()` | `IPuestoRepository.ListAllAsync()` | Filtra `IsActive`; sin join a Ocupacion |
| `PuestoRepository.ListAllAsync()` | EF Core | Sin filtro de disponibilidad |

**Reglas de negocio afectadas** (`openspec/specs/vacante-management/spec.md`):
- **N1** (`PuestoOcupado`): Rechaza crear Vacante si existe `Ocupacion` con `EsVigente = true` para el `PuestoId`.
- **N4** (`ActivePuestoIdUnique`): Constraint parcial en BD sobre `PuestoId` filtrado por `FechaCierre IS NULL` ∧ `IsDeleted = 0` — rechaza crear segunda Vacante Abierta para el mismo puesto.

Ambas validaciones viven en `VacanteServicioComandos.CrearAsync` y se aplican al POST. Ninguna previene que el dropdown muestre un puesto bloqueado.

### Decisión del usuario (locked)

> **"Puesto libre" = sin Ocupación vigente AND sin Vacante Abierta**

El backend filter aplica AMBAS condiciones:
```sql
WHERE p.IsActive = 1 AND p.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Ocupaciones o WHERE o.PuestoId = p.Id AND o.IsDeleted = 0 AND o.FechaFin IS NULL)  -- sin Ocupación vigente
  AND NOT EXISTS (SELECT 1 FROM Vacantes v WHERE v.PuestoId = p.Id AND v.IsDeleted = 0 AND v.FechaCierre IS NULL)   -- sin Vacante Abierta
```

## Approach

### Backend

1. **`IPuestoRepository`**: nuevo método `ListarDisponiblesAsync(CancellationToken)` que ejecuta la query con los dos `NOT EXISTS`.
2. **`IPuestoServicioConsulta`**: nuevo método `ListarDisponiblesAsync(CancellationToken)` que delega al repository y mapea a `PuestoDto`.
3. **`PuestoServicioConsulta`**: implementación del nuevo método — reutiliza `MapToDto` existente.
4. **`PuestosController`**: nuevo endpoint `GET /api/v1/puestos/disponibles` que consume `ListarDisponiblesAsync`. `GET /api/v1/puestos` **no cambia** — mantiene su comportamiento actual para no romper otros consumers.
5. **`PuestoDto`**: no requiere cambios (el DTO actual no expone estado de Ocupacion; el filtro es query-level).

### Web (integración)

6. **`IVacanteApiClient`**: nuevo método `ListarPuestosDisponiblesAsync(CancellationToken)`.
7. **`VacanteApiClient`**: implementación que consume `GET /api/v1/puestos/disponibles`.
8. **`Create.cshtml.cs`**: cambiar `vacanteApiClient.ListarPuestosAsync()` → `vacanteApiClient.ListarPuestosDisponiblesAsync()` en `LoadPuestosAsync()` (línea 232). `ListarPuestosAsync` queda como está para no romper otros consumers (exploration.md §"Hallazgo 1" confirma que hoy solo Vacantes/Create lo consume funcionalmente).

### Decisiones de diseño (diferidas a fase design)

- Query type vs. bool flag en `PuestoListQuery` — se decide en design.
- Nombre del método en repository (TBD si `ListarDisponiblesAsync` u otro).
- Índices compuestos en BD (ver riesgos).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/SGV.Aplicacion/Organizacion/Consultas/IPuestoServicioConsulta.cs` | Modified | Nuevo método `ListarDisponiblesAsync` |
| `src/SGV.Aplicacion/Organizacion/Consultas/PuestoServicioConsulta.cs` | Modified | Implementación del nuevo método |
| `src/SGV.Dominio/Organizacion/IPuestoRepository.cs` | Modified | Nuevo método en la interfaz del repository |
| `src/SGV.Dominio/Organizacion/PuestoRepository.cs` | Modified | Firma del nuevo método (TBD si interfaz o clase concreta) |
| `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` | Modified | Query EF Core con 2 NOT EXISTS |
| `src/SGV.Api/Controllers/PuestosController.cs` | Modified | Nuevo endpoint `GET /api/v1/puestos/disponibles` |
| `src/SGV.Web/Integration/Vacantes/IVacanteApiClient.cs` | Modified | Nuevo método `ListarPuestosDisponiblesAsync` |
| `src/SGV.Web/Integration/Vacantes/VacanteApiClient.cs` | Modified | Implementación del nuevo método |
| `src/SGV.Web/Pages/Organizacion/Vacantes/Create.cshtml.cs` | Modified | Cambio de call site en `LoadPuestosAsync` |
| `tests/SGV.Tests/Aplicacion/Organizacion/PuestoServicioConsultaTests.cs` | Modified | Tests del nuevo método |
| `tests/SGV.Tests/Persistencia/PuestoRepositoryTests.cs` | Modified | Tests del nuevo método repository |
| `tests/SGV.Tests/Web/Vacantes/VacantesCreateEditForbidTests.cs` | Modified | Actualizar fake si cambia la firma del ApiClient |
| `openspec/specs/vacante-management/spec.md` | Modified | Registrar nuevo requisito de UX (dropdown filtrado) |

## Out of Scope

- Modificar la validación backend existente (N1 `PuestoOcupado` + constraint `ActivePuestoIdUnique`).
- Extender el filtro a otros dropdowns: `Puestos/Create`, `Ocupaciones/Create`, u otros módulos.
- Backfill o migraciones de datos — no se requiere.
- Cambios en el agregado `Puesto` de Dominio (la disponibilidad NO es parte del modelo de dominio; es query-level).
- Tests de integración web que verifiquen contenido del dropdown (smoke test del page alcanza, según estrategia de tests del repo).
- Cambios en `openspec/specs/puesto-management/spec.md` — la capacidad `puesto-management` no cambia; solo se agrega un endpoint nuevo.

## Acceptance Criteria

- [ ] `GET /api/v1/puestos/disponibles` devuelve solo puestos activos que NO tienen Ocupación vigente NI Vacante Abierta.
- [ ] El dropdown de `Vacantes/Create` consume el nuevo endpoint y NO incluye puestos con Ocupación vigente.
- [ ] Tests `[MySqlFact]` cubren los 4 escenarios: (con/sin Ocupación) × (con/sin Vacante Abierta).
- [ ] La validación backend existente (N1, constraint unique `ActivePuestoIdUnique`) NO se modifica.
- [ ] `GET /api/v1/puestos` mantiene su comportamiento actual (todos los activos).
- [ ] `dotnet build SGV.slnx` compila sin errores.
- [ ] Suite `dotnet test SGV.slnx` pasa sin regresión.
- [ ] `ListarPuestosAsync` en `IVacanteApiClient` permanece funcional (no se rompe el contrato existente).

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| **Query cost si hay muchos puestos** (full scan sin índice adecuado) | Medium | Verificar/crear índice compuesto en `Ocupaciones(PuestoId, IsDeleted, FechaFin)` y `Vacantes(PuestoId, IsDeleted, FechaCierre)` — ambos不解而解; confirmar en la fase de diseño. |
| **Drift entre filtro UX y validación backend** | Low | La validación N1 + constraint unique se mantienen intactas. El filtro UX es defense-in-depth; el backend sigue siendo la fuente de verdad. |
| **Fake client de tests desincronizado** | Medium | `FakeVacanteApiClient` (usado en `VacantesCreateEditForbidTests`) requiere el nuevo método `ListarPuestosDisponiblesAsync` para compilar y correr si se cambia el PageModel. |
| **Otro consumer funcional de `ListarPuestosAsync`** | Low | Exploration confirma que solo `Create.cshtml.cs` consume funcionalmente el endpoint. Se preserva `ListarPuestosAsync` para backward compatibility. |
| **Tests con fakes pasan pero el endpoint real falla** | Low | Tests de `PuestoServicioConsultaTests` cubren el path real (sin fake); smoke test del page con `SgvWebApplicationFactory` corre contra la app real. |

## Rollback Plan

1. Revertir el cambio de call site en `Create.cshtml.cs` (volver a `ListarPuestosAsync`).
2. Eliminar el endpoint `GET /api/v1/puestos/disponibles` de `PuestosController`.
3. Eliminar `ListarDisponiblesAsync` de `IPuestoServicioConsulta`, `PuestoServicioConsulta`, `IPuestoRepository` y `PuestoRepository`.
4. Eliminar `ListarPuestosDisponiblesAsync` de `IVacanteApiClient` y `VacanteApiClient`.
5. Eliminar tests nuevos. Sin efecto sobre otros módulos o datos.

## Dependencies

- MySQL 8 con soporte para subqueries `NOT EXISTS`.
- Pomelo.EntityFrameworkCore.MySql 9.x (ya en uso).
- EF Core 9 (ya en uso).
- `dotnet ef` CLI disponible para verificar indices (si se requieren nuevas migraciones de índice).

## Success Criteria

- [ ] El endpoint `GET /api/v1/puestos/disponibles` devuelve solo puestos activos que NO tienen Ocupación vigente NI Vacante Abierta.
- [ ] El dropdown de `Vacantes/Create` consume el nuevo endpoint y NO incluye puestos con Ocupación vigente.
- [ ] Tests `[MySqlFact]` cubren los 4 escenarios (con/sin Ocupación × con/sin Vacante Abierta).
- [ ] La validación backend existente (N1, constraint unique) NO se modifica.
- [ ] `GET /api/v1/puestos` mantiene su comportamiento actual (todos los activos).
- [ ] `dotnet build SGV.slnx` compila sin errores.
- [ ] Suite `dotnet test SGV.slnx` pasa sin regresión.
- [ ] `ListarPuestosAsync` en `IVacanteApiClient` permanece funcional (no se rompe el contrato existente).
