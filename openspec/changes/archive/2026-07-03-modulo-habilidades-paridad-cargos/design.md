# Design: Implementa el módulo de Habilidades en SGV.Web con paridad completa con el módulo Cargos

## Resumen arquitectónico

El change agrega dos piezas coordinadas: (1) backend mínimo nuevo para consulta segmentada/autorizada de habilidades y catálogo HTTP de niveles, y (2) módulo Razor Pages en `SGV.Web` siguiendo el seam probado de `Cargos` (`Index/Create/Edit/Details`, parcial `_Form`, cliente tipado, PRG y SweetAlert). La paridad es **de flujo**, no de copia literal: `Habilidad` no tiene `NivelId`, por lo que el frontend NO mostrará dropdown de nivel.

## Decisiones de arquitectura

| Decisión | Elección | Rationale |
|---|---|---|
| Paridad con Cargos | Reusar nombres, rutas y flujos de `CargosController`/`CargoApiClient`/Razor Pages | Minimiza drift y baja costo cognitivo de review. |
| Query de habilidades | Nuevo `HabilidadListQuery` + `HabilidadSegmentoListado` + `IHabilidadServicioConsulta.QueryAsync` | Replica el patrón real de Cargos; evita introducir `ListByFiltrosAsync`, nombre no usado en el repo. |
| Catálogo de niveles | Nuevo `INivelHabilidadServicioConsulta`/`NivelHabilidadServicioConsulta` y orden por `Orden` | El repo actual ordena por `Codigo`; eso contradice la spec y debe corregirse. |
| Auth web | Mantener patrón actual: páginas `[Authorize]`, API arbitra `RolesSgv.Administrador` | `SGV.Web` hoy no hace chequeo visual de rol para Cargos; las acciones deben degradar con feedback ante `403`. |
| Sidebar | Confirmar `ti ti-star` | Es consistente con iconografía existente (`home`, `building`, `briefcase`) y comunica catálogo/capacidad sin invadir branding. |
| Migraciones | No crear migración en el baseline | `Habilidades` ya tiene índice único activo por código y un índice por `Categoria`; solo considerar índice extra si MySQL/EXPLAIN lo justifica. |

## Flujo de datos

```text
Razor Page -> IHabilidadApiClient -> SGV.Api/SkillsController
          -> IHabilidadServicioConsulta / INivelHabilidadServicioConsulta
          -> IHabilidadRepository / INivelHabilidadRepository
          -> MySQL (EF Core / Pomelo)
```

`Index` consume `/api/v1/skills/consulta`; `Create/Edit/Details` consumen `GET/POST/PUT/DELETE/PATCH` legacy; `GET /api/v1/niveles-habilidad` se publica para discoverability futura pero NO se consume en el catálogo maestro web.

## Vista por capas y archivos

### Dominio

Sin cambios directos. `src/SGV.Dominio/Habilidades/Habilidad.cs` ya prueba que `Codigo` es inmutable post-create y que no existe `NivelId` propio.

### Aplicación

| Archivo | Acción | Justificación |
|---|---|---|
| `src/SGV.Aplicacion/Habilidades/Consultas/Dtos/HabilidadListQuery.cs` | Crear | Query normalizada (`page`, `pageSize`, `search`, `sort`, `segmento`). |
| `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadServicioConsulta.cs` | Modificar | Agregar `QueryAsync`. |
| `src/SGV.Aplicacion/Habilidades/Consultas/HabilidadServicioConsulta.cs` | Modificar | Mapear `Habilidad -> HabilidadDto` en listado segmentado. |
| `src/SGV.Aplicacion/Habilidades/Consultas/INivelHabilidadServicioConsulta.cs` | Crear | Paridad con `INivelCargoServicioConsulta`. |
| `src/SGV.Aplicacion/Habilidades/Consultas/NivelHabilidadServicioConsulta.cs` | Crear | Mapear `NivelHabilidad -> NivelHabilidadDto`. |
| `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadRepository.cs` | Modificar | Agregar `QueryAsync(...)`. |

### Infraestructura

| Archivo | Acción | Justificación |
|---|---|---|
| `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs` | Modificar | Implementar query segmentada con búsqueda por `Codigo/Nombre/Categoria/Descripcion`, sort server-side y paginación. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/NivelHabilidadRepository.cs` | Modificar | Cambiar orden `Codigo` -> `Orden`. |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/HabilidadConfiguracion.cs` | Consultar | Ya existen `ActiveCodigoUnique` y `IX_Habilidades_Categoria`; `Nombre` queda pendiente de medición, no por defecto. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/*` | Sin cambios esperados | No hay cambio de esquema previsto. |

### API

| Archivo | Acción | Justificación |
|---|---|---|
| `src/SGV.Api/Controllers/SkillsController.cs` | Modificar | Agregar `[Authorize]`, `GetConsulta`, `GetNivelesHabilidad` y roles admin en mutaciones. |

### Web

| Archivo | Acción | Justificación |
|---|---|---|
| `src/SGV.Web/Program.cs` | Modificar | Registrar `IHabilidadApiClient`. |
| `src/SGV.Web/Integration/Habilidades/IHabilidadApiClient.cs` | Crear | Cliente tipado del módulo. |
| `src/SGV.Web/Integration/Habilidades/HabilidadApiClient.cs` | Crear | Traducción HTTP + 400/403/404/409. |
| `src/SGV.Web/Integration/Habilidades/*ViewModel*.cs` | Crear | `HabilidadListItemViewModel`, `HabilidadListQuery`, `HabilidadDeleteResult`, `HabilidadInputModel`, helpers de retorno. |
| `src/SGV.Web/Pages/Shared/Partials/_Sidenav.cshtml` | Modificar | Entrada `Habilidades` debajo de `Cargos`, submenú `Listado` + `Nueva`. |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Index.cshtml(.cs)` | Crear | Listado segmentado + baja/reactivación. |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Create.cshtml(.cs)` | Crear | Alta con PRG, sin campo nivel. |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Edit.cshtml(.cs)` | Crear | Edición con `Codigo` readonly/disabled. |
| `src/SGV.Web/Pages/Organizacion/Habilidades/Details.cshtml(.cs)` | Crear | Detalle readonly. |
| `src/SGV.Web/Pages/Organizacion/Habilidades/_Form.cshtml` | Crear | Parcial compartida; NO incluye select de nivel. |
| `src/SGV.Web/wwwroot/js/pages/habilidades-index.js` | Crear | Confirmaciones SweetAlert de baja/reactivación. |

## Contratos HTTP

| Método | Ruta | Auth | Respuesta | Errores |
|---|---|---|---|---|
| GET | `/api/v1/skills` | autenticado | `IReadOnlyList<HabilidadDto>` activas | `401` |
| GET | `/api/v1/skills/{id}` | autenticado | `HabilidadDto` activa | `401`, `404` |
| GET | `/api/v1/skills/consulta` | autenticado | `PagedResult<HabilidadDto>` | `401` |
| GET | `/api/v1/niveles-habilidad` | autenticado | `IReadOnlyList<NivelHabilidadDto>` ordenada por `Orden` | `401` |
| POST | `/api/v1/skills` | admin | `201 + HabilidadDto` | `400`, `401`, `403`, `409` |
| PUT | `/api/v1/skills/{id}` | admin | `200 + HabilidadDto` | `400`, `401`, `403`, `404`, `409` |
| DELETE | `/api/v1/skills/{id}` | admin | `204` | `401`, `403`, `404` |
| PATCH | `/api/v1/skills/{id}/reactivar` | admin | `200 + HabilidadDto` | `401`, `403`, `404`, `409` |

## Contrato de paginación

**Paridad real con Cargos hoy:** `PagedResult<T>` existente solo tiene `Items`, `TotalCount`, `Page`, `PageSize`. `TotalPages` se calcula en `SGV.Web`; `search`, `sort` y `status` viven en la query string y en route values, no en el body. Cambiar eso implicaría romper/ajustar también `Cargos` y sus tests, por lo que este design NO amplía el contrato compartido.

Normalización del request en `HabilidadListQuery`: `page<1 => 1`, `pageSize<1 => 20`, `pageSize>100 => 100`, `status` desconocido => `activas`, `sort` desconocido => `codigo_asc`.

## Adaptación del patrón Cargos

| Patrón Cargos | Decisión en Habilidades |
|---|---|
| `/consulta` + segmentos activas/eliminadas | Copiar tal cual |
| PRG + `TempData` para feedback | Copiar tal cual |
| SweetAlert para baja/reactivación | Copiar tal cual |
| `Details` readonly | Copiar tal cual |
| `Codigo` editable en create | Copiar tal cual |
| `Codigo` editable en edit | **NO copiar**: en Habilidad es readonly por dominio |
| Dropdown de nivel en create/edit | **NO copiar**: `Habilidad` no tiene `NivelId` propio |
| Acciones visibles sin role-check UI | Copiar patrón actual; manejar `403` con mensaje recuperable |
| Catálogo tipado (`ICargoApiClient`) | Replicar como `IHabilidadApiClient` |

## Estrategia de pruebas (xUnit)

| Comportamiento | Pruebas previstas |
|---|---|
| Query normalizada y segmentos | `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadListQueryTests.cs`, `HabilidadServicioConsultaTests.cs` |
| Query MySQL por activas/eliminadas + sort/search | `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` (`[MySqlFact]`, idealmente con `EXPLAIN` capturado si se agrega índice) |
| Catálogo `/niveles-habilidad` ordenado por `Orden` | `NivelHabilidadRepositoryTests.cs`, nuevo `NivelHabilidadServicioConsultaTests.cs`, `SkillsControllerTests.cs` |
| Auth/roles controller | `SkillsControllerTests.cs` (401/403 + atributo `[Authorize]`) |
| Cliente HTTP tipado | `tests/SGV.Tests/Web/Habilidad/HabilidadApiClientTests.cs` |
| Registro DI web | `tests/SGV.Tests/Web/Habilidad/HabilidadWebSeamTests.cs` |
| Sidebar y páginas anónimas/autenticadas | `HabilidadCreatePageTests.cs`, `HabilidadIndexPageTests.cs`, `HabilidadDetailsPageTests.cs`, `HabilidadEditPageTests.cs` |
| Guard anti-drift “sin nivel en form” | tests web de Create/Edit que afirmen ausencia de `Input.NivelId`, `<select>` y texto “Nivel” |

## Plan de entrega por slices

| Slice | Alcance | Líneas estimadas* | Boundary |
|---|---|---:|---|
| 1 | Aplicación + infraestructura + API + tests backend | 350-550 | PR independiente |
| 2 | `IHabilidadApiClient`, DI web, sidenav, seams/tests del cliente | 180-280 | PR independiente |
| 3 | Razor Pages, JS y tests web | 700-1000 | **Dividir** en 3A (`Index+JS`) y 3B (`Create/Edit/Details+_Form`) |

\*Estimación gruesa basada en el tamaño actual del módulo `Cargos`; no es cifra cerrada.

## Migraciones

No se requiere migración si el diseño se mantiene en query/read-model. Si profiling MySQL exige un índice adicional (por ejemplo `Nombre`), debe salir en un slice reversible separado con validación previa por `issue #59` y rollback explícito (`DROP INDEX`).

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Reintroducir dropdown de nivel por copia de Cargos | Tests web de ausencia explícita en Create/Edit + revisión de `_Form.cshtml`. |
| `/consulta` lenta en MySQL 8 | Reusar filtro por segmento simple, medir con `[MySqlFact]`, no agregar índice sin evidencia. |
| Drift entre skills y cargos | Nombrado/parámetros idénticos a `Cargos`; documentar diferencias obligatorias (`Codigo` readonly, sin nivel). |
| `403` poco claro en web | `HabilidadApiClient` debe traducir `403` a resultado recuperable con mensaje de permisos. |
| Cambio de contrato paginado compartido | Mantener `PagedResult<T>` actual; no expandir scope a Cargos en este change. |

## Out of scope técnico

- Asignaciones `habilidad↔cargo` y `habilidad↔persona`.
- Refactors generales de repositorios fuera de lo mínimo para `/consulta`.
- Nueva migración/esquema salvo evidencia fuerte de índice faltante.
- Cambios globales de autorización más allá de `SkillsController`.
