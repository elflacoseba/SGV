## Exploration: Implementar el módulo de Habilidades

### Current State

El repositorio ya contiene una base funcional para Habilidades, pero principalmente como catálogo de lectura y soporte de compatibilidad. Dominio define `Habilidad`, `NivelHabilidad`, `CargoHabilidad` y `PersonaHabilidad`; Infraestructura ya mapea `Habilidades`, `NivelesHabilidad`, `CargoHabilidades` y `PersonaHabilidades` en EF Core/Pomelo; Aplicación expone `IHabilidadServicioConsulta`/`HabilidadServicioConsulta`; y API publica `GET /api/v1/skills` y `GET /api/v1/skills/{id:guid}` sin autorización.

No existe todavía un módulo completo de gestión de Habilidades: faltan comandos, requests, validadores, resultado de comandos, métodos write en `IHabilidadRepository`/`HabilidadRepository`, endpoints de escritura, pruebas de dominio para ciclo de vida y una especificación OpenSpec dedicada. Además, la especificación vigente de API indica que `skills` debe permanecer read-only en esta versión, por lo que cualquier gestión CRUD requiere una delta spec explícita.

### Affected Areas

- `src/SGV.Dominio/Habilidades/Habilidad.cs` — entidad central del catálogo; ya valida `Codigo`, `Nombre`, `Categoria`, `Descripcion` y mantiene `IsActive`, pero no expone métodos de activar/desactivar ni regla explícita de inmutabilidad de `Codigo`.
- `src/SGV.Dominio/Habilidades/NivelHabilidad.cs` — catálogo inmutable de niveles 1 a 4; debe preservarse como referencia para requerimientos y habilidades de personas.
- `src/SGV.Dominio/Habilidades/CargoHabilidad.cs` — relación de cargo a habilidad requerida con `NivelRequeridoId`, `Ponderacion` y `EsObligatoria`; ya tiene restricción de ponderación positiva.
- `src/SGV.Dominio/Personas/PersonaHabilidad.cs` — relación de persona a habilidad poseída; es punto de integración para compatibilidad, pero no necesariamente parte del primer CRUD de catálogo.
- `src/SGV.Dominio/Organizacion/Cargo.cs` — ya contiene `AgregarHabilidad(...)`; el alcance debe decidir si el módulo solo gestiona catálogo de habilidades o también requisitos por cargo.
- `src/SGV.Aplicacion/Habilidades/Consultas/*` — capa de consulta existente a preservar; DTO actual expone `id`, `codigo`, `nombre`, `descripcion`, `categoria`.
- `src/SGV.Aplicacion/Compatibilidad/ServicioCompatibilidadHabilidades.cs` — calcula compatibilidad por requisitos/persona usando niveles 1..4 y ponderaciones; integración futura si el cambio incluye asignaciones.
- `src/SGV.Infraestructura/Persistencia/Entidades/HabilidadEntity.cs` — entidad EF auditable con `IsActive`; base para escritura si se implementa CRUD.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/HabilidadConfiguracion.cs` — tabla `Habilidades`, auditoría, longitudes, índice por `Categoria` y unicidad activa por columna generada `ActiveCodigoUnique` compatible con MySQL.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/NivelHabilidadConfiguracion.cs` — tabla `NivelesHabilidad` con check `ValorNumerico BETWEEN 1 AND 4`, índices únicos por `Codigo` y `ValorNumerico`.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoHabilidadConfiguracion.cs` y `PersonaHabilidadConfiguracion.cs` — relaciones con FK `Restrict` hacia Habilidades/Niveles y unicidad por par.
- `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs` — hoy solo lista habilidades activas ordenadas por `Codigo`; requiere métodos write si el módulo será administrable.
- `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` — no tiene mapeo `Habilidad -> HabilidadEntity`; debe agregarse para comandos.
- `src/SGV.Api/Controllers/SkillsController.cs` — actualmente read-only, con ruta en inglés `api/v1/skills`, a diferencia de otros módulos en español como `cargos` y `puestos`.
- `openspec/specs/sgv-readonly-api/spec.md` — declara que `skills` permanece read-only; debe modificarse si se agregan operaciones de escritura.
- `openspec/specs/sgv-database/spec.md` — ya define requisitos de habilidades requeridas, habilidades de personas y compatibilidad; falta precisar gestión del catálogo de Habilidades.
- `tests/SGV.Tests/Aplicacion/Habilidades/HabilidadServicioConsultaTests.cs`, `tests/SGV.Tests/Api/SkillsControllerTests.cs`, `tests/SGV.Tests/Persistencia/HabilidadRepositoryTests.cs` — patrones existentes para lectura; faltan pruebas de comandos, validadores y dominio.

### Approaches

1. **Promover Habilidades a catálogo CRUD administrable** — conservar el modelo y tabla existentes, agregar creación, actualización de campos editables, baja lógica/reactivación y validación de unicidad activa de `Codigo`.
   - Pros: reutiliza la arquitectura ya estabilizada en Cargos/Puestos, mantiene alcance controlado, aprovecha índices y auditoría existentes.
   - Cons: requiere cambiar explícitamente la regla read-only de `skills`; hay que decidir si `Codigo` será inmutable y cómo nombrar rutas nuevas.
   - Effort: Medium

2. **Gestionar además requisitos por Cargo y habilidades de Persona** — incluir endpoints/servicios para `CargoHabilidad` y/o `PersonaHabilidad`, conectando el catálogo con compatibilidad.
   - Pros: acerca el módulo al valor funcional de selección y compatibilidad.
   - Cons: aumenta el alcance y cruza límites de Organización, Personas y Selección; mayor riesgo de superar el presupuesto de revisión de 400 líneas.
   - Effort: High

3. **Formalizar solo lectura del catálogo** — mantener `skills` como catálogo consultable y completar únicamente documentación/specs alrededor de lo existente.
   - Pros: mínimo riesgo y alinea con la especificación vigente.
   - Cons: probablemente no cumple la intención de “implementar módulo”, porque la porción read-only ya existe.
   - Effort: Low

### Recommendation

Avanzar con el enfoque 1 para la primera propuesta: convertir Habilidades en un módulo CRUD de catálogo, preservando `NivelHabilidad`, `CargoHabilidad`, `PersonaHabilidad` y compatibilidad como integraciones existentes pero fuera de alcance salvo por reglas defensivas. El diseño debería reflejar Cargos/Puestos: requests inmutables, FluentValidation, `CommandResult` tipado, `ProblemDetails`/`ValidationProblemDetails`, Unit of Work, repositorio con mapeos explícitos, baja lógica, reactivación y pruebas por capa.

La propuesta debe declarar explícitamente que modifica la restricción read-only solo para Habilidades si se agregan endpoints de escritura. Para proteger revisión, se recomienda una rama nueva para las siguientes fases, por ejemplo `feature/implementa-modulo-habilidades`, y evaluar PR encadenados si se incluye algo más que CRUD de catálogo.

### Risks

- La especificación actual de API exige que `skills` sea read-only; implementar escritura sin delta spec rompería el contrato SDD.
- Ya existe una ruta pública en inglés (`/api/v1/skills`) mientras el dominio y otros controladores usan español; la propuesta debe decidir si se conserva por compatibilidad o si se agrega una ruta española.
- `Habilidad` ya participa en `CargoHabilidad`, `PersonaHabilidad` y compatibilidad; desactivar una habilidad referenciada puede afectar cargos, personas y cálculos existentes.
- MySQL no soporta índices filtrados; la unicidad de `Codigo` activo debe preservar la columna generada `ActiveCodigoUnique` o una estrategia equivalente.
- Si el alcance incluye asignaciones a cargos/personas, el cambio probablemente exceda el presupuesto de revisión y debe partirse en PRs encadenados.

### Ready for Proposal

Yes — indicar al usuario que Habilidades ya existe como catálogo read-only con persistencia e integración de compatibilidad. La siguiente fase debería proponer un CRUD de catálogo en español, con no objetivos claros para asignaciones a cargos/personas salvo que el usuario confirme que forman parte del alcance inicial.
