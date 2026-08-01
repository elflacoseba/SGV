# Exploration: Implementa el módulo de Auditorias

## 1. Hallazgo central: la escritura ya existe, la lectura NO

El sistema ya tiene una infraestructura completa de **escritura** de auditoría (interceptor + servicio explícito), pero carece por completo de cualquier capacidad de **lectura** o consulta. Esto es lo que el change debe construir.

---

## 2. Estado actual del circuito de auditoría (escritura — ya existe)

### 2.1 Modelo persistido: `AuditoriaEntity`

- Ubicación: `src/SGV.Infraestructura/Persistencia/Entidades/AuditoriaEntity.cs`
- Hereda de `EntityBase` (NO de `AuditableEntityBase` — correcto, evita recursión infinita)
- Campos:

| Campo | Tipo | MaxLen | Notas |
|---|---|---|---|
| `Id` | `Guid` | — | PK, heredado de `EntityBase` |
| `UserId` | `string?` | 450 | FK a `AspNetUsers.Id` |
| `OccurredAt` | `DateTime` | — | UTC |
| `EntityName` | `string` | 200 | Nombre lógico sin sufijo `Entity` |
| `EntityId` | `string` | 100 | ID de la entidad afectada |
| `Operation` | `string` | 50 | `Alta`, `Modificacion`, `BajaLogica` |
| `OldValuesJson` | `string?` | `longtext` | Serialización JSON de valores originales |
| `NewValuesJson` | `string?` | `longtext` | Serialización JSON de valores actuales |
| `ChangedPropertiesJson` | `string?` | `longtext` | Array JSON de nombres de propiedades modificadas |
| `CorrelationId` | `Guid?` | — | Para correlacionar operaciones compuestas |

- Tabla: `Auditorias`
- `AuditoriaEntity` NO es `AuditableEntityBase` (correcto — no se аудита a sí misma)

### 2.2 Interceptor: `AuditoriaSaveChangesInterceptor`

- Ubicación: `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs`
- Tipo: `SaveChangesInterceptor` (override de `SavingChanges` y `SavingChangesAsync`)
- Filtro de entidades:
  ```csharp
  .Where(e => e.Entity is EntityBase and not AuditoriaEntity)
  .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
  ```
- Mapeo de operaciones:
  - `EntityState.Added` → `"Alta"`
  - `EntityState.Modified` (IsDeleted=true) → `"BajaLogica"`
  - `EntityState.Modified` → `"Modificacion"`
  - `EntityState.Deleted` → `"BajaLogica"` (soft-delete: el estado se cambia a Modified con IsDeleted=true)
- Campos sensibles excluidos de la serialización JSON:
  ```csharp
  nombre.Contains("Password", OrdinalIgnoreCase)
  || nombre.Contains("Token", OrdinalIgnoreCase)
  || nombre.Contains("SecurityStamp", OrdinalIgnoreCase)
  || nombre.Contains("ConcurrencyStamp", OrdinalIgnoreCase)
  ```
- Obtiene `UserId` y `CorrelationId` de `IUsuarioActual`

### 2.3 Servicio explícito para Identity: `AuditoriaServicio`

- Ubicación: `src/SGV.Infraestructura/Persistencia/AuditoriaServicio.cs`
- Implementa: `IAuditoriaServicio` (`src/SGV.Aplicacion/Auditoria/IAuditoriaServicio.cs`)
- Razón de existencia: Identity users (`SgvIdentityUser`) no heredan de `EntityBase`, entonces el interceptor no los intercepta
- Consumidores actuales: `SetupServicio`, `PersonaServicioComandos`, `UsuarioServicioComandos`
- Patrón: recibe `valoresAnteriores` y `valoresNuevos` como diccionarios, calcula `changedProperties` por diff, serializa a JSON y persiste con `SaveChangesAsync` inmediato (一行 transaction)

### 2.4 Índices existentes en `AuditoriaConfiguracion`

```csharp
builder.HasIndex(e => new { e.EntityName, e.EntityId, e.OccurredAt }); // consultas por entidad+tiempo
builder.HasIndex(e => new { e.UserId, e.OccurredAt });                  // consultas por usuario+tiempo
builder.HasIndex(e => e.CorrelationId);                                  // correlación
```

### 2.5 Auditoría técnica de entidades

`AuditableEntityBase` (y por transitividad todas las entidades de dominio que lo heredan) recibe automáticamente:
- `CreatedAt` / `CreatedByUserId` en `EntityState.Added`
- `UpdatedAt` / `UpdatedByUserId` en `EntityState.Modified`
- `IsDeleted=true`, `DeletedAt`, `DeletedByUserId` en `EntityState.Deleted` (además cambia el estado a `Modified`)

---

## 3. Ausencias confirmadas (lo que NO existe — el módulo de lectura completo)

| Componente | Existe | Evidencia |
|---|---|---|
| `IAuditoriaServicioConsulta` (read port) | **NO** | Solo existe `IAuditoriaServicio.cs` y `NoopAuditoriaServicio.cs` en `src/SGV.Aplicacion/Auditoria/` |
| `AuditoriaServicioConsulta` | **NO** | — |
| `AuditoriasController` | **NO** | Ningún controller en `src/SGV.Api/Controllers/` |
| `SGV.Contracts/Auditoria/` | **NO** | No existe la carpeta; todos los contratos están bajo subdominios (Organizacion, Personas, etc.) |
| `AuditoriaDto` | **NO** | — |
| `AuditoriaListQuery` | **NO** | — |
| `AuditoriaRepository` | **NO** | No hay repositorio de solo lectura para auditorias |
| Página web `Auditorias/Index` | **NO** | No existe `src/SGV.Web/Pages/Auditorias/` |
| Página web `Auditorias/Detail` | **NO** | — |
| Tests de query de auditoría | **NO** | — |

---

## 4. Patrones existentes a seguir

### 4.1 Contrato de query paginada

```csharp
// Patrón en CargoListQuery, PuestoListQuery, HabilidadListQuery, PersonaListQuery, VacanteListQuery
public sealed record XxxListQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Sort,
    XxxSegmentoListado Segmento = XxxSegmentoListado.Activas);
```

Para auditoría el `Segmento` no aplica (no hay soft-delete de registros de auditoría). Se reemplazaría por un filtro de rango de fechas (`DateFrom`, `DateTo`).

### 4.2 `PagedResult<T>` wrapper

```csharp
// src/SGV.Contracts/Organizacion/Consultas/Dtos/PagedResult.cs
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
```

### 4.3 Patrón de controller

```csharp
[ApiController]
[Route("api/v1/auditorias")]
[Authorize]  // cualquier usuario autenticado — o [Authorize(Roles = RolesSgv.Administrador)]
public sealed class AuditoriasController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditoriaDto>>> GetConsulta(
        [FromQuery] AuditoriaListQuery query,
        CancellationToken ct) { ... }
}
```

### 4.4 Web Razor Pages

Estructura esperada:
```
src/SGV.Web/Pages/Auditorias/
├── Index.cshtml
├── Index.cshtml.cs
└── _Filtros.cshtml (partial opcional)
```

PageModel con:
- `LoadAsync(CancellationToken)` → invoca `IAuditoriaApiClient.QueryAsync`
- `PagedResult<AuditoriaDto>` como propiedad del modelo
- `ResolveRedirectPageAsync` para paginación PRG-compliant

### 4.5 Carpeta de contratos

```
src/SGV.Contracts/Auditoria/
├── AuditoriaDto.cs
├── AuditoriaListQuery.cs
└── AuditoriaQuery.cs (parámetros de detalle si aplica)
```

---

## 5. Preguntas de producto abiertas (respuesta requerida antes de propuesta)

### 5.1 Autorización — ¿quién puede ver los logs de auditoría?

| Opción | Descripción | Implicación |
|---|---|---|
| **A — Admin only** | `[Authorize(Roles = RolesSgv.Administrador)]` | Solo administradores ven auditoría. Consistente con mutaciones admin-only. |
| **B — Cualquier autenticado** | `[Authorize]` | Un usuario puede ver toda la auditoría. Información sensible. |
| **C — Rol `Auditor` nuevo** | Nuevo rol + política | Requiere definir el rol y su присвоение. |

**Recomendación exploratoria:** Opción A (Admin only) es el default más seguro y consistente con el patrón del sistema.

### 5.2 Exposición de `OldValuesJson` y `NewValuesJson`

Los JSON de valores pueden contener datos sensibles de personas (nombres, emails, teléfonos, etc.).

| Opción | Descripción | Implicación |
|---|---|---|
| **A — No exponer** | El `AuditoriaDto` NO incluye `OldValuesJson` ni `NewValuesJson` | Máximo nivel de seguridad de datos |
| **B — Solo ChangedPropertiesJson** | Expone qué campos cambiaron, pero no los valores | Útil para auditoría técnica sin exponer datos |
| **C — Exponer todo** | Incluye old/new values en el DTO | Requiere sanitización adicional de campos sensibles |

**Recomendación exploratoria:** Opción B — mantener `ChangedPropertiesJson` en el DTO (el "qué cambió") pero sin los valores old/new. Esto permite аудиторский análisis sin exponer PII.

### 5.3 Filtros de consulta iniciales

Mínimo viable para el query de listado:

| Filtro | Tipo | Notas |
|---|---|---|
| `EntityName` | `string?` | Filtrar por tipo de entidad |
| `Operation` | `string?` | `Alta`, `Modificacion`, `BajaLogica` |
| `DateFrom` | `DateTime?` | Inicio del rango |
| `DateTo` | `DateTime?` | Fin del rango |
| `UserId` | `string?` | Opcional — quién realizó la operación |

### 5.4 ¿Web UI o solo API?

Todos los módulos existentes (`Cargos`, `Puestos`, `Habilidades`, `Personas`, `Vacantes`, `Ocupaciones`) tienen **both** API + Web. No hay precedentes de "solo API" en este proyecto.

**Recomendación exploratoria:** Incluir both para mantener consistencia. La UI de auditoría es valiosa para administradores no-técnicos.

### 5.5 Ubicación del módulo (¿bajo Organización?)

> *"No asumas que la auditoría debe vivir bajo Organización sin comprobar patrones."*

**Confirmado:** No existe `SGV.Contracts/Organización/` como padre de Auditoría. El módulo auditoría es **transversal** (cross-cutting), no pertenece a ningún subdomain de negocio.

**Recomendación:** Carpeta propia top-level:
- `SGV.Contracts/Auditoria/`
- `SGV.Aplicacion/Auditoria/` (se expande la existente)
- `src/SGV.Web/Pages/Auditorias/`
- `src/SGV.Api/Controllers/AuditoriasController.cs`

Esto es análogo a cómo `Seguridad` existe como módulo propio para `Usuarios`.

### 5.6 Retención y purga

La tabla `Auditorias` crece indefinidamente. No existe política de retención.

**Recomendación exploratoria:** Declarar out-of-scope para v1, pero diseñar la arquitectura abierta para futura purga (e.g., columna `RetentionDate` o tabla de archival).

### 5.7 ¿Exportación (CSV/Excel)?

Ningún módulo existente tiene exportación.

**Recomendación exploratoria:** Out-of-scope para v1.

### 5.8 ¿Auditoría de la auditoría?

¿Se deben registrar las consultas de logs de auditoría?

**Recomendación exploratoria:** **No** — evitar recursión infinita y complejidad accidental.

---

## 6. Áreas afectadas por el nuevo módulo

| Archivo / Carpeta | Razón de impacto |
|---|---|
| `src/SGV.Aplicacion/Auditoria/` | Se expande: se agrega `IAuditoriaServicioConsulta.cs` y `AuditoriaServicioConsulta.cs` |
| `src/SGV.Contracts/Auditoria/` | **Nueva carpeta** con `AuditoriaDto.cs`, `AuditoriaListQuery.cs` |
| `src/SGV.Infraestructura/Persistencia/` | Se puede reutilizar `SgvDbContext.Auditorias` ya existente; opcionalmente `AuditoriaRepository` |
| `src/SGV.Api/Controllers/AuditoriasController.cs` | **Nuevo archivo** |
| `src/SGV.Web/Pages/Auditorias/` | **Nueva carpeta** con `Index.cshtml`, `Index.cshtml.cs` |
| `src/SGV.Web/Integration/` | Nuevo `AuditoriaApiClient` y seam tests |
| `tests/SGV.Tests/` | Tests unitarios del servicio, de integración del controller, y seam tests del PageModel |
| `docs/decisiones-implementacion.md` | Se documenta la decisión de implementar el módulo de auditoría |

---

## 7. Enfoques comparados

### Enfoque A — Módulo completo (API + Web, listado + detalle) **[RECOMENDADO]**

| Aspecto | Detalle |
|---|---|
| API | `GET /api/v1/auditorias` (listado paginado) + `GET /api/v1/auditorias/{id}` (detalle) |
| Web | `Pages/Auditorias/Index` con tabla paginada + filtros |
| Autorización | `[Authorize(Roles = RolesSgv.Administrador)]` |
| DTO `AuditoriaDto` | Todos los campos EXCEPTO `OldValuesJson` y `NewValuesJson` |
| Filtros iniciales | `EntityName`, `Operation`, `DateFrom`, `DateTo`, `UserId` |
| Retención/Export | Out of scope v1 |
| Complejidad | Media — sigue patrones existentes |
| Esfuerzo estimado | ~2-3 semanas con tests |

**Pros:** Completo, consistente con el resto del sistema, seguro por default (sin PII expuesta)
**Cons:** Requiere definir autorización y decisiones de producto primero

### Enfoque B — API mínima sin Web

| Aspecto | Detalle |
|---|---|
| API | Solo `GET /api/v1/auditorias` |
| Web | Ninguno |
| Autorización | `[Authorize]` |
| DTO | Solo metadatos, sin JSON old/new |
| Complejidad | Baja |

**Pros:** Rápido de implementar
**Cons:** Rompe el patrón del proyecto (todos los módulos tienen web); útil solo para consumidores API externos

### Enfoque C — Solo lectura de metadata (más restrictivo)

| Aspecto | Detalle |
|---|---|
| API | Solo endpoint de listado |
| DTO | Solo `EntityName`, `EntityId`, `Operation`, `OccurredAt`, `UserId`, `ChangedPropertiesJson` |
| Sin detalle | No hay endpoint individual |
| Complejidad | Baja |

**Pros:** Máxima seguridad de datos
**Cons:** Limitado — no se puede ver el valor anterior/nuevo de un cambio

---

## 8. Riesgos identificados

| # | Riesgo | Severidad | Mitigación |
|---|---|---|---|
| 1 | La tabla `Auditorias` crece indefinidamente; OFFSET pagination degrada en tablas grandes | Alta | Diseñar para cursor pagination futuro; hoy OFFSET es aceptable para MVP |
| 2 | Exposición inadvertida de PII en `OldValuesJson`/`NewValuesJson` | Alta | No incluir estos campos en `AuditoriaDto` en v1 |
| 3 | Performance del deserializado JSON en listados | Media | Enlistar solo metadatos en queries de listado; deserializar solo en detalle |
| 4 | Suite de tests inexistente para query de auditoría | Media | Construir desde cero siguiendo patrones de tests existentes |
| 5 | Decisión de autorización pendiente — afecta diseño del controller | Media | Resolver 5.1 antes de redactar proposal |

---

## 9. Decisiones confirmadas (listas para usar en la proposal)

1. ✅ La escritura de auditoría YA EXISTE y no necesita cambios
2. ✅ El módulo de lectura es NUEVO y sigue patrones existentes de `CargoListQuery` / `PuestoListQuery`
3. ✅ La tabla `Auditorias` ya existe con 3 índices adecuados para queries
4. ✅ La estructura de la carpeta será `SGV.Contracts/Auditoria/`, `SGV.Aplicacion/Auditoria/` (expandida), `AuditoriasController`, `Pages/Auditorias/`
5. ✅ `AuditoriaEntity` NO hereda de `AuditableEntityBase` (correcto — evita recursión)

---

## 10.listo para Proposal

**NO** — las siguientes preguntas de producto deben responderse ANTES de redactar la propuesta:

1. **Autorización** (5.1): ¿Admin-only o cualquier usuario autenticado?
2. **Exposición de JSON** (5.2): ¿Exponer `OldValuesJson`/`NewValuesJson` o solo `ChangedPropertiesJson`?

Las preguntas 5.3–5.8 tienen recomendaciones exploratorias que pueden adoptarse por default si el usuario no especifica lo contrario.
