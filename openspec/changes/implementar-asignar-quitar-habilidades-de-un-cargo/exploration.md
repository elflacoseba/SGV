# Exploración — Implementar asignar/quitar Habilidades de un Cargo

## Resumen ejecutivo

El codebase de SGV ya tiene **gran parte del backend resuelto** para `CargoHabilidad`: existen la entidad de dominio, el mapeo EF Core, la tabla `CargoHabilidades`, el repositorio, el servicio de aplicación y los endpoints anidados en `CargosController` para listar, asignar/actualizar y quitar habilidades de un cargo. También hay cobertura en aplicación, persistencia y API. El hueco real NO está en la API, sino en `SGV.Web`: hoy la shell web de Cargos no consume el subrecurso `/api/v1/cargos/{cargoId}/skills`, no tiene cliente tipado para esa capacidad y tampoco renderiza una UI para administrar habilidades desde la página de edición o detalle del cargo.

La exploración también detectó dos puntos IMPORTANTES para no derivar mal el cambio. Primero, `Habilidad` como catálogo **no** tiene `NivelId`; el nivel vive en la asociativa `CargoHabilidad.NivelRequeridoId`, y eso está correctamente implementado en backend. Segundo, el backend actual de `CargoSkillServicio` usa un request mínimo (`AsignarCargoSkillRequest` solo con `NivelId`) y, cuando actualiza una asociación existente, la reemplaza por `delete + add`, fijando `Ponderacion = 1.0m` y `EsObligatoria = false`. Eso significa que el caso “asignar/quitar” ya existe, pero la experiencia web todavía no está construida y hay una decisión de producto pendiente sobre si el primer corte debe exponer solo nivel requerido o también `Ponderacion`/`EsObligatoria`.

## Contexto y motivación

El cambio es necesario porque el repositorio ya soporta administrar habilidades requeridas por cargo a nivel API, pero el módulo `SGV.Web` de Cargos sigue limitado al catálogo maestro del cargo (`Codigo`, `Nombre`, `Descripcion`, `Nivel`). Un administrador hoy puede crear, editar, eliminar y reactivar cargos, pero no puede ver ni administrar desde la shell las habilidades asociadas a un cargo.

Además, el repo ya tiene un patrón de referencia sólido en `Persona↔Habilidad`: subrecurso HTTP anidado, servicio de aplicación dedicado, validación de entidad dueña + habilidad + nivel, repositorio con proyección detallada y borrado físico. Eso reduce riesgo técnico: el trabajo nuevo parece más de **integración web + alineación contractual** que de modelado de dominio o migraciones.

## Hallazgos del codebase

### Estado actual de la asociativa `CargoHabilidad`

#### Entidad de dominio

- Archivo: `src/SGV.Dominio/Habilidades/CargoHabilidad.cs`
- `CargoHabilidad` hereda de `EntidadBase`, no de `EntidadAuditable`.
- Campos reales verificados:
  - `CargoId`
  - `HabilidadId`
  - `NivelRequeridoId`
  - `Ponderacion`
  - `EsObligatoria`
- NO existen `FechaAsignacion` ni `AsignadoPor`.
- El constructor exige `ponderacion > 0`.

#### Relación con `Cargo`

- Archivo: `src/SGV.Dominio/Organizacion/Cargo.cs`
- `Cargo` expone `IReadOnlyCollection<CargoHabilidad> Habilidades`.
- Existe `AgregarHabilidad(Guid habilidadId, Guid nivelRequeridoId, decimal ponderacion, bool esObligatoria)`.
- El agregado previene duplicados por `HabilidadId`.
- No existe método de update sobre `CargoHabilidad`; por eso la capa de aplicación hoy resuelve cambios con `delete + add`.

#### Persistencia EF Core

- Archivos:
  - `src/SGV.Infraestructura/Persistencia/Entidades/CargoHabilidadEntity.cs`
  - `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoHabilidadConfiguracion.cs`
- `CargoHabilidadEntity` hereda de `EntityBase`, no de `AuditableEntityBase`.
- Columnas reales persistidas:
  - `CargoId`
  - `HabilidadId`
  - `NivelRequeridoId`
  - `Ponderacion`
  - `EsObligatoria`
- La configuración define:
  - tabla `CargoHabilidades`
  - check constraint `CK_CargoHabilidades_Ponderacion`
  - FK a `Cargos` con cascade
  - FK a `Habilidades` con restrict
  - FK a `NivelesHabilidad` con restrict

#### Migración que la creó

- Archivo: `src/SGV.Infraestructura/Persistencia/Migraciones/20260614183103_InicialSgvo.cs`
- La tabla `CargoHabilidades` se crea ahí con:
  - `Id`
  - `CargoId`
  - `HabilidadId`
  - `NivelRequeridoId`
  - `Ponderacion decimal(5,2)`
  - `EsObligatoria`

#### Índices y unicidad

- Existe `IX_CargoHabilidades_CargoId_HabilidadId` única.
- Existen índices simples en `HabilidadId` y `NivelRequeridoId`.
- No hay unicidad segmentada por activas/eliminadas porque `CargoHabilidad` **no usa soft delete**.

#### Soft delete

- `CargoHabilidad` es **append/delete físico**, no baja lógica.
- No hay `IsDeleted`, `DeletedAt` ni columnas generadas de unicidad activa.
- El repositorio elimina físicamente la fila y los tests lo validan.

### Catálogo Cargos (resumen de lo existente)

#### Dominio, repositorio y consultas

- Entidad: `src/SGV.Dominio/Organizacion/Cargo.cs`
- Repositorio contrato: `src/SGV.Aplicacion/Organizacion/Consultas/ICargoRepository.cs`
- Implementación: `src/SGV.Infraestructura/Persistencia/Repositorios/CargoRepository.cs`

Capacidades verificadas:

- `AddAsync`
- `GetByIdForUpdateAsync`
- `GetByIdIncludingDeletedAsync`
- `UpdateAsync`
- `DeleteAsync` (soft delete)
- `ReactivateAsync`
- `ExistsActiveCodeAsync`
- `HasActivePuestosAsync`
- `QueryAsync(search, page, pageSize, sort, segmento)`

#### API HTTP

- Archivo: `src/SGV.Api/Controllers/CargosController.cs`
- Rutas verificadas:
  - `GET /api/v1/cargos`
  - `GET /api/v1/cargos/consulta`
  - `GET /api/v1/cargos/{id}`
  - `POST /api/v1/cargos`
  - `PUT /api/v1/cargos/{id}`
  - `DELETE /api/v1/cargos/{id}`
  - `PATCH /api/v1/cargos/{id}/reactivar`
  - `GET /api/v1/cargos/{cargoId}/skills`
  - `PUT /api/v1/cargos/{cargoId}/skills/{skillId}`
  - `DELETE /api/v1/cargos/{cargoId}/skills/{skillId}`

#### DTOs relevantes

- `CargoDto` (`src/SGV.Aplicacion/Organizacion/Consultas/Dtos/CargoDto.cs`) expone solo `Id`, `Codigo`, `Nombre`, `Descripcion`, `NivelId`, `NivelNombre`.
- `CargoDto` NO incluye habilidades embebidas.
- Subrecurso skill:
  - `CargoSkillDto` expone `SkillId` y `NivelId`
  - `CargoSkillDetailDto` expone solo `Skill` y `Nivel`

#### Política de autorización

- `CargosController` tiene `[Authorize]` a nivel controller.
- Writes del catálogo y del subrecurso skill usan `[Authorize(Roles = RolesSgv.Administrador)]`.

#### Razor Pages actuales

- Carpeta: `src/SGV.Web/Pages/Organizacion/Cargos/`
- Estructura vigente:
  - `Index.cshtml` / `Index.cshtml.cs`
  - `Create.cshtml` / `Create.cshtml.cs`
  - `Edit.cshtml` / `Edit.cshtml.cs`
  - `Details.cshtml` / `Details.cshtml.cs`
  - `_Form.cshtml`

Hallazgos:

- `Edit.cshtml` renderiza un único formulario de datos maestros usando `_Form.cshtml`.
- `_Form.cshtml` solo tiene `Codigo`, `Nombre`, `Descripcion`, `NivelId`.
- `Details.cshtml` muestra solo metadata del cargo; no hay bloque de habilidades asociadas.
- `Index.cshtml` ya soporta PRG, `TempData`, segmentación `activas|eliminadas` y reactivación, pero no muestra ni administra skills.

### Catálogo Habilidades (referencia)

#### Estado confirmado

- Entidad: `src/SGV.Dominio/Habilidades/Habilidad.cs`
- Repositorio contrato: `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadRepository.cs`
- Repositorio EF: `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs`
- Controller: `src/SGV.Api/Controllers/SkillsController.cs`
- Web shell: `src/SGV.Web/Pages/Organizacion/Habilidades/`
- Cliente tipado: `src/SGV.Web/Integration/Habilidades/`

#### Contratos vigentes

- `GET /api/v1/skills`
- `GET /api/v1/skills/{id}`
- `GET /api/v1/skills/consulta`
- `POST /api/v1/skills`
- `PUT /api/v1/skills/{id}`
- `DELETE /api/v1/skills/{id}`
- `PATCH /api/v1/skills/{id}/reactivar`
- `GET /api/v1/niveles-habilidad`

#### Lección anti-drift verificada

- Test: `tests/SGV.Tests/Web/Habilidad/HabilidadAntiDriftTests.cs`
- Verifica explícitamente que Create/Edit/_Form de `Habilidad` NO tengan select de nivel ni `Input.NivelId`.
- El partial `src/SGV.Web/Pages/Organizacion/Habilidades/_Form.cshtml` documenta esa restricción.

### Patrón `Persona↔Habilidad` (referencia para asignar/quitar)

#### Entidad y mapeo

- Persistencia: `src/SGV.Infraestructura/Persistencia/Entidades/PersonaHabilidadEntity.cs`
- Configuración: `src/SGV.Infraestructura/Persistencia/Configuraciones/PersonaHabilidadConfiguracion.cs`
- Campos reales:
  - `PersonaId`
  - `HabilidadId`
  - `NivelHabilidadId`
  - `VerificadoAt`
  - `Fuente`
- Índice único: `{ PersonaId, HabilidadId }`

#### Servicio de aplicación y endpoints

- Servicio: `src/SGV.Aplicacion/Personas/Comandos/PersonaSkillServicio.cs`
- Endpoints:
  - `GET /api/v1/personas/{personaId}/skills`
  - `PUT /api/v1/personas/{personaId}/skills/{skillId}`
  - `DELETE /api/v1/personas/{personaId}/skills/{skillId}`

Patrón verificado:

1. valida persona existente
2. valida habilidad existente
3. valida nivel existente
4. busca asociación actual por par
5. si existe, hace `DeleteAsync(existente)`
6. crea una nueva asociación
7. guarda con `unitOfWork.SaveChangesAsync`

### Estado real de `Cargo↔Habilidad`

El backend de `Cargo↔Habilidad` YA está implementado con el mismo patrón de `Persona↔Habilidad`:

- Servicio: `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs`
- Contrato: `src/SGV.Aplicacion/Organizacion/Comandos/ICargoSkillServicio.cs`
- Repositorio: `src/SGV.Infraestructura/Persistencia/Repositorios/CargoSkillRepository.cs`
- Request: `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillRequests.cs`
- Controller: subrecurso dentro de `src/SGV.Api/Controllers/CargosController.cs`

Pero hay detalles IMPORTANTES:

- `AsignarCargoSkillRequest` hoy solo lleva `NivelId`.
- `CargoSkillServicio.UpsertAsync(...)` crea la asociación con:
  - `Ponderacion = 1.0m`
  - `EsObligatoria = false`
- Esos campos existen en DB/dominio, pero **no forman parte del contrato HTTP actual**.
- La mutación no pasa por `Cargo.AgregarHabilidad(...)`; instancia `new CargoHabilidad(...)` directamente.
- No hay validadores FluentValidation específicos para `AsignarCargoSkillRequest` ni `AsignarPersonaSkillRequest`.

### Auditoría y convenciones

#### Interceptor EF Core

- Archivo: `src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs`
- El interceptor audita cualquier `EntityBase` que no sea `AuditoriaEntity` y esté en estado `Added`, `Modified` o `Deleted`.
- Eso implica que `CargoHabilidadEntity` **sí genera filas en `Auditorias`**.

#### Limitación importante

- Como `CargoHabilidadEntity` hereda de `EntityBase` y NO de `AuditableEntityBase`:
  - no recibe `CreatedAt`
  - no recibe `UpdatedAt`
  - no recibe `DeletedAt`
  - no soporta soft delete técnico
- Pero sí queda auditada a nivel de evento en `Auditorias`.

#### Errores HTTP

- `CargosController` usa `Problem(...)` para errores del subrecurso skill.
- Mapea:
  - `CargoSkillErrorType.NotFound` → `404`
  - `CargoSkillErrorType.Validation` → `400`
- No hay `409` específico del subrecurso hoy; el upsert evita el duplicado por reemplazo.

### Frontend `SGV.Web` — punto de inserción

#### Punto de inserción principal

- El lugar más natural es `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml` / `Edit.cshtml.cs`.
- Hoy la página ya:
  - carga el cargo
  - carga el catálogo de `NivelCargo`
  - usa PRG con `TempData`
  - preserva retorno al listado
- Técnicamente se puede agregar un panel adicional debajo del `_Form.cshtml` sin romper el flujo actual.

#### Punto de lectura secundaria

- `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` es el lugar natural para una vista readonly de habilidades ya asignadas.
- Hoy no renderiza ningún bloque relacionado a skills.

#### Cliente tipado

- `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` y `CargoApiClient.cs` hoy cubren:
  - CRUD de cargo
  - consulta paginada
  - reactivación
  - catálogo `NivelesCargo`
- NO existe ningún método para:
  - listar `CargoSkillDetailDto`
  - hacer `PUT` de `AsignarCargoSkillRequest`
  - hacer `DELETE` del subrecurso skill

#### Parciales reutilizables

- No encontré una vista parcial reusable para selección múltiple de skills con nivel requerido.
- En `Pages/Shared` no aparece un componente equivalente.
- Los únicos `select` reutilizados hoy son de formularios simples (`NivelCargo`, `TipoUnidadOrganizativa`, `UnidadPadreId`).

#### Convenciones PRG / TempData

- `Cargos/Index.cshtml.cs`, `Create.cshtml.cs` y `Edit.cshtml.cs` ya usan:
  - `TempData["StatusMessage"]`
  - `TempData["StatusKind"]`
- Los errores recuperables se muestran con `ErrorMessage` y `ModelState`.
- La shell sigue patrón PRG consistente; conviene mantenerlo para altas/bajas de skills desde UI.

## Pruebas existentes relevantes

### Cargo↔Habilidad

- Aplicación: `tests/SGV.Tests/Aplicacion/Organizacion/CargoSkillServicioTests.cs`
- Persistencia MySQL: `tests/SGV.Tests/Persistencia/CargoSkillRepositoryTests.cs`
- API: `tests/SGV.Tests/Api/CargoSkillControllerTests.cs`
- Swagger/OpenAPI: `tests/SGV.Tests/Api/SwaggerConfigurationTests.cs`

Cobertura real observada:

- `UpsertAsync` exitoso, cargo inexistente, habilidad inexistente, nivel inexistente.
- `DeleteAsync` exitoso y asociación inexistente.
- repositorio con alta, duplicado por índice único, update vía replace, borrado físico, listados y proyección detallada.
- autorización API del subrecurso (`401`/`403`) y shape Swagger.

### Referencia Persona↔Habilidad

- Aplicación: `tests/SGV.Tests/Aplicacion/Personas/PersonaSkillServicioTests.cs`
- Hay patrón espejo de validación y replace.

### Anti-drift de Habilidad catálogo

- `tests/SGV.Tests/Web/Habilidad/HabilidadAntiDriftTests.cs`
- Es crítico como precedente para no introducir un `NivelId` propio en `Habilidad`.

### Hueco de testing actual

- No encontré tests web para administrar `Cargo↔Habilidad` desde `SGV.Web`.
- Eso es consistente con la ausencia de cliente tipado y de UI.

## Riesgos identificados

1. **Riesgo de scope mal definido**: el nombre del cambio sugiere una feature nueva, pero el backend ya existe. Si proposal no acota el alcance, se puede reimplementar API innecesariamente.
   - Mitigación: declarar explícitamente si el objetivo es `SGV.Web` + contratos faltantes, o si también se quiere refinar el backend.

2. **Riesgo de drift con `Habilidad.NivelId`**: ya existe antecedente real de suposición incorrecta.
   - Mitigación: toda UI de nivel debe bindear contra `CargoHabilidad.NivelRequeridoId`, nunca contra `Habilidad`.

3. **Riesgo contractual en `CargoSkillDetailDto`**: la spec `cargo-skill-query-contract` habla de `skillId` y `nivelId`, pero el DTO actual expone solo `Skill` y `Nivel` anidados.
   - Mitigación: decidir en proposal si se preserva el contrato actual del código/tests o si se corrige la spec/DTO.

4. **Riesgo de semántica incompleta**: dominio y tabla tienen `Ponderacion` y `EsObligatoria`, pero la API actual los fija implícitamente.
   - Mitigación: confirmar si este change solo necesita asignar/quitar con nivel, o si debe exponer también esos campos.

5. **Riesgo de UX**: no existe partial reusable ni flujo web previo para administrar associations dentro de Cargos.
   - Mitigación: diseñar un panel explícito en `Edit` con PRG y mensajes consistentes, en vez de intentar “inyectarlo” en `_Form.cshtml` sin estructura.

## Recomendación de próximo paso

**propose**

Hay suficiente evidencia para pasar a propuesta, pero la propuesta debería arrancar aclarando que el backend ya está implementado y que el cambio probablemente deba enfocarse en `SGV.Web` más una posible alineación contractual menor. Ir directo a `spec` sin esa aclaración corre el riesgo de fijar alcance equivocado.

## Preguntas abiertas para el usuario

1. ¿Querés que este cambio cubra **solo la shell web `SGV.Web`** para administrar las habilidades del cargo, o también querés ajustar el contrato backend existente?
2. ¿El primer corte debe exponer únicamente **habilidad + nivel requerido**, o también necesitás capturar `Ponderacion` y `EsObligatoria`, que hoy existen en dominio/DB pero no en el request HTTP?
3. ¿La administración de skills debe vivir solo en la página **Editar Cargo**, o también querés verla en **Detalle** como lectura readonly?

## Current State

El sistema ya soporta `Cargo↔Habilidad` en dominio, aplicación, persistencia y API, con borrado físico, validación de cargo/habilidad/nivel y unicidad por par `(CargoId, HabilidadId)`. Lo que falta es la integración en `SGV.Web` y la decisión de alcance sobre qué campos del vínculo se van a exponer en la UI.

## Affected Areas

- `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml` — punto natural para agregar el panel de administración de skills.
- `src/SGV.Web/Pages/Organizacion/Cargos/Edit.cshtml.cs` — carga de datos, PRG y handlers nuevos.
- `src/SGV.Web/Pages/Organizacion/Cargos/Details.cshtml` — lectura readonly opcional de skills asignadas.
- `src/SGV.Web/Integration/Organizacion/ICargoApiClient.cs` — hoy no expone operaciones del subrecurso skill.
- `src/SGV.Web/Integration/Organizacion/CargoApiClient.cs` — faltan métodos HTTP para `GET/PUT/DELETE /api/v1/cargos/{cargoId}/skills`.
- `src/SGV.Api/Controllers/CargosController.cs` — backend ya existente; podría requerir solo ajuste contractual si se redefine el shape.
- `src/SGV.Aplicacion/Organizacion/Comandos/CargoSkillServicio.cs` — punto sensible si se decide exponer `Ponderacion`/`EsObligatoria`.
- `tests/SGV.Tests/Web/**` — hoy no hay cobertura web del flujo.

## Approaches

1. **Integrar solo la capacidad existente en `SGV.Web`**
   - Pros: menor riesgo, reutiliza backend ya probado, encaja con el hallazgo principal.
   - Cons: deja intacta la semántica actual de `Ponderacion`/`EsObligatoria` fija y el posible drift entre spec y DTO.
   - Effort: Medium.

2. **Integrar `SGV.Web` y refinar el contrato del vínculo**
   - Pros: permite exponer `Ponderacion`/`EsObligatoria` o corregir el shape de `CargoSkillDetailDto` en el mismo change.
   - Cons: amplía bastante el alcance y aumenta riesgo de review.
   - Effort: High.

## Recommendation

La opción más razonable es la primera, salvo que negocio confirme que `Ponderacion` y `EsObligatoria` ya son obligatorios en esta iteración. La evidencia del repo muestra que la carencia principal es de shell web, no de backend.

## Risks

- Reimplementar backend que ya existe y desperdiciar esfuerzo.
- Reintroducir el error conceptual de poner el nivel en `Habilidad` en vez de `CargoHabilidad`.
- Diseñar una UI sin aclarar si el vínculo debe capturar solo nivel o también ponderación/obligatoriedad.

## Ready for Proposal

Sí, con una advertencia: la proposal debe empezar delimitando alcance. Si el objetivo real es “habilitar la gestión desde `SGV.Web`”, el change está listo para propuesta. Si además querés rediseñar el contrato del vínculo, conviene resolver primero las preguntas abiertas.
