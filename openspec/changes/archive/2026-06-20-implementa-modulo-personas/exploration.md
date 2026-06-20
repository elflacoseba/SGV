## Exploration: Implementar el módulo de Personas

### Current State

El repositorio ya contiene la base de dominio y persistencia para Personas: `Persona` existe en Dominio con datos personales, documento, teléfono, estado activo y colecciones hacia habilidades y ocupaciones; Infraestructura ya mapea `PersonaEntity` a la tabla `Personas`; `SgvDbContext` expone `DbSet<PersonaEntity>`; y la migración inicial ya creó la tabla con índices únicos activos para `Legajo`, `Email` y documento mediante columnas generadas compatibles con MySQL.

No existe todavía un módulo de aplicación ni API para gestionar Personas: faltan DTOs, requests, validadores, servicios de consulta/comandos, contrato de repositorio, implementación de repositorio, mapeos `Persona`/`PersonaEntity`, controlador, pruebas por capa y especificación OpenSpec dedicada. El alcance solicitado debe excluir explícitamente Postulantes, Ocupaciones y Habilidades; por lo tanto, este módulo debe operar sobre datos propios de `Persona` y no sobre `Postulante`, `Ocupacion` ni `PersonaHabilidad`.

### Affected Areas

- `src/SGV.Dominio/Personas/Persona.cs` — entidad central ya modelada; requiere revisar si el ciclo de vida debe exponer métodos `Desactivar`/`Activar` y mantener reglas de datos personales sin incluir habilidades ni ocupaciones.
- `src/SGV.Infraestructura/Persistencia/Entidades/PersonaEntity.cs` — entidad EF existente con campos personales y navegaciones; será la base del repositorio de Personas.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/PersonaConfiguracion.cs` — configura tabla `Personas`, longitudes, auditoría e índices únicos activos para `Legajo`, `Email` y documento con columnas generadas por MySQL.
- `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` — ya registra `Personas`; no debería requerir cambios estructurales de base de datos para un CRUD inicial.
- `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` — no tiene mapeo de `Persona` a `PersonaEntity`; deberá agregarse en fases posteriores.
- `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` — no tiene mapeo de `PersonaEntity` a `Persona`; deberá agregarse evitando cargar o exponer `Postulantes`, `Ocupaciones` o `Habilidades`.
- `src/SGV.Infraestructura/Persistencia/Repositorios/*` — existen patrones de repositorio para Cargos, Puestos y Habilidades; falta `PersonaRepository` e interfaz en Aplicación.
- `src/SGV.Aplicacion/*` — no existe carpeta `Personas`; se debe crear una estructura equivalente a `Habilidades`/`Organizacion` para consultas, comandos, DTOs, resultados y validadores.
- `src/SGV.Api/Controllers/*` — falta un controlador `PersonasController` con contrato HTTP consumer-safe, probablemente bajo `api/v1/personas`.
- `tests/SGV.Tests/Dominio`, `tests/SGV.Tests/Aplicacion`, `tests/SGV.Tests/Persistencia`, `tests/SGV.Tests/Api` — faltan pruebas específicas de Personas; el proyecto usa xUnit y `strict_tdd: true`.
- `openspec/specs/sgv-database/spec.md` — contiene requisitos relacionados con habilidades de personas, ocupaciones y postulantes, pero no un requisito administrativo específico para Personas como entidad propia.
- `openspec/specs/sgv-readonly-api/spec.md` — actualmente enumera recursos expuestos como unidades, tipos, cargos, puestos y skills; debe ampliarse si Personas tendrá API pública.

### Approaches

1. **CRUD administrativo mínimo de Personas** — crear, consultar, actualizar datos propios, desactivar y reactivar Personas, reutilizando la tabla existente y sin endpoints para Postulantes, Ocupaciones ni Habilidades.
   - Pros: cumple el alcance pedido, aprovecha persistencia existente, mantiene el cambio acotado y replica patrones ya probados de Cargos/Puestos/Habilidades.
   - Cons: requiere definir reglas de unicidad activa a nivel aplicación además de confiar en índices; debe decidir qué campos son editables y cómo responder ante conflictos de `Legajo`, `Email` o documento.
   - Effort: Medium

2. **Solo consultas de Personas** — exponer listado/detalle de Personas activas sin comandos de escritura.
   - Pros: menor riesgo, menor tamaño de PR, evita discutir reactivación y conflictos complejos.
   - Cons: probablemente no satisface “implementar el módulo” si se espera gestión administrativa completa; deja incompleto el ciclo de vida que ya existe en persistencia.
   - Effort: Low

3. **Personas con relaciones operativas** — incluir además habilidades, ocupaciones o vínculo con postulantes.
   - Pros: habilita casos funcionales más ricos de selección y ocupación de puestos.
   - Cons: contradice el alcance solicitado; cruza tres bounded contexts, aumenta acoplamiento y probablemente supera el presupuesto de revisión de 400 líneas.
   - Effort: High

### Recommendation

Avanzar con el enfoque 1: un módulo administrativo mínimo de Personas, con contrato propio `api/v1/personas`, DTO consumer-safe, comandos con FluentValidation, resultado tipado, repositorio dedicado, Unit of Work, baja lógica/reactivación y pruebas TDD por dominio, aplicación, persistencia y API. La propuesta debe declarar como no objetivos: Postulantes, Ocupaciones, Habilidades, `PersonaHabilidad`, asignaciones laborales y procesos de selección.

No parece necesario introducir una migración para el primer corte si se reutiliza la tabla `Personas` existente. Las siguientes fases deberían validar si la migración actual ya refleja todo el contrato esperado y, si se documentan cambios de API, agregar deltas en `sgv-readonly-api` y una nueva especificación `persona-management`.

### Risks

- La entidad `Persona` referencia colecciones de `PersonaHabilidad` y `Ocupacion`; incluirlas accidentalmente en DTOs, repositorios o `Include` rompería el alcance pedido.
- La tabla ya tiene unicidad activa para `Legajo`, `Email` y documento mediante columnas generadas; el servicio debe traducir conflictos previsibles a errores de dominio/API claros, no filtrar excepciones crudas de MySQL.
- `Email` y documento son opcionales; la validación debe permitir ausencia pero rechazar longitudes/formato inválidos si se define esa regla.
- Si se implementa CRUD completo más documentación y pruebas, existe riesgo medio de superar 400 líneas; conviene planificar PRs encadenados si la fase de tareas estima alto volumen.
- OpenSpec no tiene todavía un spec de Personas; implementar sin delta spec dejaría el cambio sin contrato verificable.

### Ready for Proposal

Yes — indicar al usuario que Personas ya existe en dominio y persistencia, pero no como módulo de aplicación/API. La siguiente fase debería proponer un CRUD administrativo mínimo de Personas en español, excluyendo explícitamente Postulantes, Ocupaciones y Habilidades.
