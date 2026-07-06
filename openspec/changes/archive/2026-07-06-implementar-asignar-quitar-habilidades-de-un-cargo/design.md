# Diseño — Implementar asignar/quitar Habilidades de un Cargo

## Resumen del enfoque
El cambio extiende el backend existente de `CargoHabilidad` sin reemplazarlo: se ajustan contrato HTTP, validación de aplicación y proyección de lectura para exponer `NivelRequeridoId`, `Ponderacion` y `EsObligatoria`. No se rediseña dominio, no se introduce soft delete y no se agrega migración mientras se preserve el esquema actual (`decimal(5,2)`, FK e índice único por `{CargoId,HabilidadId}`).

En `SGV.Web` se agrega una página dedicada `Pages/Organizacion/Cargos/Habilidades.cshtml` con PRG, antiforgery y mensajes vía `TempData`. La página reutiliza `ICargoApiClient` para el subrecurso `Cargo↔Habilidad` y `IHabilidadApiClient` para catálogos (`skills`, `niveles-habilidad`), evitando duplicar contratos o mover `Nivel` al catálogo `Habilidad`.

## Cambios por capa

### Dominio (`src/SGV.Dominio/`)
- **Modificar:** ninguno.
- **Mantener:** `CargoHabilidad` sigue siendo inmutable después de crearla; update = `delete + add`. `Nivel` sigue en `CargoHabilidad.NivelRequeridoId` (memoria #569).

### Aplicación (`src/SGV.Aplicacion/`)
- **Modificar:**
  - `Organizacion/Comandos/CargoSkillServicio.cs`: inyectar `IValidator<AsignarCargoSkillRequest>`, validar antes de consultar repositorios, aplicar defaults (`Ponderacion=1.00`, `EsObligatoria=false`) y devolver DTO con valores persistidos.
  - `Organizacion/Comandos/CargoSkillRequests.cs`: request con `NivelRequeridoId`, `Ponderacion?`, `EsObligatoria?`.
  - `Organizacion/Comandos/CargoSkillCommandResult.cs`: agregar `FieldErrors` para producir `ValidationProblemDetails` por campo.
  - `Organizacion/Consultas/Dtos/CargoSkillDto.cs`: reflejar respuesta de write con `skillId`, `nivelRequeridoId`, `ponderacion`, `esObligatoria`.
  - `Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs`: enriquecer GET con `skillId`, `nivelRequeridoId`, `ponderacion`, `esObligatoria`, `skill`, `nivel`.
  - `Organizacion/Consultas/ICargoSkillRepository.cs` y `Infraestructura/.../CargoSkillRepository.cs`: proyección única del GET enriquecido, sin N+1.
- **Crear:** `Organizacion/Comandos/Validaciones/AsignarCargoSkillRequestValidator.cs` con reglas FluentValidation: `NivelRequeridoId != Guid.Empty`, `Ponderacion > 0`, `Ponderacion <= 100.00`, máximo 2 decimales.

### Infraestructura (`src/SGV.Infraestructura/`)
- **Modificar:** `Persistencia/Repositorios/CargoSkillRepository.cs` para proyectar ids + flags además de objetos anidados.
- **Mantener:** `Persistencia/Configuraciones/CargoHabilidadConfiguracion.cs` sin cambios funcionales; el `CHECK Ponderacion > 0`, `HasPrecision(5,2)` e índice único existente siguen correctos.
- **Migración:** no requerida; el tope `100.00` vive solo en aplicación por decisión explícita.
- **Auditoría:** `AuditoriaSaveChangesInterceptor.cs` ya audita `CargoHabilidadEntity` por heredar de `EntityBase`; no requiere activación adicional.

### API (`src/SGV.Api/`)
- **Modificar:** `Controllers/CargosController.cs`.
  - `GET /api/v1/cargos/{cargoId}/skills`: devolver contrato enriquecido con `nivelRequeridoId` (sin alias `nivelId`).
  - `PUT /api/v1/cargos/{cargoId}/skills/{skillId}`: aceptar payload ampliado y devolver write DTO completo.
  - `ToSkillProblemResult(...)`: bifurcar validación general vs. `ValidationProblemDetails` cuando existan `FieldErrors`.
- **Mantener:** autorización actual (`[Authorize]` + rol `Administrador` para write).

### Web (`src/SGV.Web/`)
- **Crear:** `Pages/Organizacion/Cargos/Habilidades.cshtml` y `.cshtml.cs`.
  - PageModel con `[Authorize]` y chequeo explícito de rol `Administrador` en GET/POST.
  - Handlers: `OnGetAsync`, `OnPostAsignarAsync`, `OnPostActualizarAsync`, `OnPostQuitarAsync`.
  - Carga: `ICargoApiClient.GetByIdAsync`, `GetSkillsAsync`; `IHabilidadApiClient.GetAllAsync`, `GetNivelesHabilidadAsync`.
  - PRG con `TempData["StatusMessage"]`/`["StatusKind"]`; errores recuperables sin stack trace.
- **Modificar:** `Integration/Organizacion/ICargoApiClient.cs` y `CargoApiClient.cs` para `GetSkillsAsync`, `UpsertSkillAsync`, `DeleteSkillAsync`, parseando `ValidationProblemDetails` igual que `CargoApiClient.UpdateAsync`.
- **No modificar en este corte:** `Pages/Organizacion/Cargos/Edit.cshtml`, `Index.cshtml`, `Details.cshtml`.

## Flujo de datos
`Habilidades.cshtml` → `ICargoApiClient.UpsertSkillAsync/DeleteSkillAsync` → `CargosController` → `CargoSkillServicio` → `CargoSkillRepository` → MySQL

`Habilidades.cshtml` → `ICargoApiClient.GetSkillsAsync` + `IHabilidadApiClient.GetAllAsync/GetNivelesHabilidadAsync` → tabla editable rehidratada

## Estrategia de pruebas (strict TDD)
- **Aplicación:** extender `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs` con defaults, rechazo por `Ponderacion` inválida, replace idempotente y DTO completo.
- **Persistencia:** extender `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs` para verificar proyección enriquecida y persistencia exacta de `Ponderacion`/`EsObligatoria` con `[MySqlFact]`.
- **API:** extender `tests/SGV.Tests/Api/CargoSkillControllerTests.cs` y `SwaggerConfigurationTests.cs` para payload ampliado, `ValidationProblemDetails`, `nivelRequeridoId` sin alias y schema Swagger actualizado.
- **Web:** crear `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs`; modificar `FakeCargoApiClient.cs` y `CargoApiClientTests.cs`. Cubrir GET anónimo, acceso sin rol, estado vacío, asignar, editar, quitar, error 4xx/5xx y anti-drift de no postear `Habilidad.NivelId`.
- **Anti-drift:** extender `tests/SGV.Tests/Web/Habilidad/HabilidadAntiDriftTests.cs` con aserción cruzada: la nueva UI usa `NivelRequeridoId` del vínculo, no agrega nivel al catálogo `Habilidad`.

## Archivos a crear / modificar
**Crear:**
- `src/SGV.Aplicacion/Organizacion/Comandos/Validaciones/AsignarCargoSkillRequestValidator.cs`
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml`
- `src/SGV.Web/Pages/Organizacion/Cargos/Habilidades.cshtml.cs`
- `tests/SGV.Tests/Web/Cargo/CargoHabilidadesPageTests.cs`

**Modificar:**
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs`
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillRequests.cs`
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillCommandResult.cs`
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDto.cs`
- `src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoSkillDetailDto.cs`
- `src/SGV.Aplicacion/Organizacion/Consultas/ICargoSkillRepository.cs`
- `src/SGV.Infraestructura/Persistencia/Repositorios/CargoSkillRepository.cs`
- `src/SGV.Api/Controllers/CargosController.cs`
- `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs`
- `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs`
- tests existentes de aplicación, API, persistencia y fakes web.

## Orden de implementación sugerido
1. Ajustar DTOs/resultados/request + validator.
2. Extender `CargoSkillServicio` y tests de aplicación.
3. Extender repositorio/proyección y tests MySQL.
4. Ajustar controller + tests API/Swagger.
5. Extender `ICargoApiClient`/`CargoApiClient` + tests del cliente.
6. Crear Razor Page + tests web.

## Candidatos a chained PR
- **PR1:** contratos + validator + servicio + tests de aplicación.
- **PR2:** repositorio + controller + API/Swagger.
- **PR3:** cliente web + Razor Page + tests web.

## Riesgos y mitigaciones
| Riesgo | Mitigación |
|---|---|
| Drift con `Habilidad.NivelId` | Bind y DTOs usan solo `NivelRequeridoId`. |
| Error de campo no mapeado en UI | `CargoSkillCommandResult.FieldErrors` + `ValidationProblemDetails`. |
| Scope creep tocando páginas existentes | Página nueva dedicada; `Edit/Index/Details` quedan fuera. |

## Compatibilidad y migración
- No se introduce soft delete en `CargoHabilidad`.
- No se introduce `Habilidad.NivelId`.
- No se agrega `CHECK` para `Ponderacion <= 100.00`.
- No se mantiene alias legado `nivelId` en el GET del subrecurso.

## Referencias
- `proposal.md`, `exploration.md`, `specs/**/spec.md`
- `docs/decisiones-implementacion.md`
- `openspec/specs/cargo-skill-query-contract/spec.md`
- `openspec/specs/cargo-web-crear-editar/spec.md`
