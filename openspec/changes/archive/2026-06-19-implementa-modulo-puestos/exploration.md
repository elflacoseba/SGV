## Exploration: Implementar el módulo de Puestos

### Current State

El sistema ya contiene una porción funcional de Puestos como recurso de solo lectura. El Dominio define `Puesto` en `SGV.Dominio.Organizacion` con referencias obligatorias a `UnidadOrganizativa` y `Cargo`, jerarquía opcional mediante `PuestoSuperiorId`, baja lógica/auditoría heredada y navegaciones existentes hacia `Ocupaciones` y `Vacantes`. Infraestructura ya tiene `PuestoEntity`, `PuestoConfiguracion`, `SgvDbContext.Puestos`, `PuestoRepository`, mapeo `PuestoEntity -> Puesto`, DI, servicio de consulta y `PuestosController` con `GET /api/v1/puestos` y `GET /api/v1/puestos/{id:guid}`.

No existe todavía un módulo de escritura para Puestos: faltan requests, validadores, servicio de comandos, resultado de comandos, métodos write en `IPuestoRepository`/`PuestoRepository`, mapeo `Puesto -> PuestoEntity`, endpoints `POST/PUT/DELETE/PATCH`, pruebas de dominio específicas y especificaciones OpenSpec dedicadas. Además, `openspec/specs/sgv-readonly-api/spec.md` declara que positions/puestos deben permanecer read-only en esta versión, por lo que implementar CRUD exige una delta spec explícita.

### Affected Areas

- `src/SGV.Dominio/Organizacion/Puesto.cs` — entidad central; validar invariantes de código/nombre, referencias obligatorias, jerarquía, activación/desactivación y no acoplar comportamiento de Ocupaciones/Vacantes.
- `src/SGV.Aplicacion/Organizacion/Consultas/*Puesto*` — consulta existente a preservar; DTO actual expone resumen de unidad y cargo.
- `src/SGV.Aplicacion/Organizacion/Comandos/` — debería incorporar requests, validadores, result/error type y servicio de comandos para crear, actualizar, desactivar y reactivar Puestos.
- `src/SGV.Infraestructura/Persistencia/Entidades/PuestoEntity.cs` — entidad EF existente con FK a UnidadOrganizativa, Cargo y Puesto superior, más navegaciones a Ocupaciones/Vacantes.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/PuestoConfiguracion.cs` — ya configura tabla `Puestos`, check anti-autorreferencia, FK `Restrict`, índice único activo por columna generada e índices FK compatibles con MySQL.
- `src/SGV.Infraestructura/Persistencia/Repositorios/PuestoRepository.cs` — hoy solo consulta activos con `Include` de UnidadOrganizativa y Cargo; requiere métodos write y validaciones de existencia/duplicado/jerarquía.
- `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` — falta mapeo `Puesto` hacia `PuestoEntity` para persistencia de comandos.
- `src/SGV.Api/Controllers/PuestosController.cs` — hoy expone solo GET; los endpoints de escritura deberían seguir el estilo de `CargosController` y `UnidadesOrganizativasController`.
- `openspec/specs/sgv-readonly-api/spec.md` — debe modificarse para permitir escritura de Puestos y mantener Ocupaciones/Vacantes fuera de alcance.
- `openspec/specs/sgv-database/spec.md` — ya contiene un requisito mínimo de Puestos concretos; requiere delta para ciclo de vida, unicidad, jerarquía y reglas de independencia.
- `tests/SGV.Tests/` — existen pruebas de consulta/API/persistencia para Puestos, pero faltan pruebas de dominio y comandos; los patrones a seguir son Cargos y Unidades Organizativas.

### Approaches

1. **Promover Puestos a módulo CRUD independiente** — conservar el modelo/tabla existente y agregar create, update, soft-delete/desactivar, reactivar y reglas de jerarquía, sin gestionar Ocupaciones ni Vacantes.
   - Pros: satisface el sentido de “módulo de Puestos”, reutiliza la arquitectura ya probada en Cargos/Unidades y mantiene el alcance controlado.
   - Cons: requiere cambiar la restricción OpenSpec read-only para Puestos; hay que definir reglas de eliminación sin implementar Ocupaciones/Vacantes.
   - Effort: Medium

2. **Formalizar solo lectura de Puestos** — documentar y completar la porción read-only actual sin agregar escritura.
   - Pros: mínimo riesgo y alinea con la especificación read-only vigente.
   - Cons: probablemente no cumple la intención de implementar el módulo, porque el recurso ya existe como lectura.
   - Effort: Low

3. **CRUD parcial sin jerarquía completa** — agregar escritura básica, pero postergar validación profunda de ciclos/descendientes de `PuestoSuperiorId`.
   - Pros: reduce complejidad inicial.
   - Cons: deja una deuda peligrosa en una estructura jerárquica; puede persistir ciclos que luego sean costosos de corregir.
   - Effort: Medium

### Recommendation

Avanzar con el enfoque 1. La propuesta y especificaciones deberían convertir Puestos en un módulo gestionado, modificando explícitamente la regla read-only solo para Puestos y manteniendo como no objetivos la gestión de Ocupaciones y Vacantes. El diseño debería seguir los patrones ya estabilizados de Cargos y Unidades Organizativas: requests inmutables, FluentValidation antes de reglas de repositorio, `CommandResult` tipado, `ProblemDetails`/`ValidationProblemDetails`, Unit of Work, repositorio con mapeos explícitos y pruebas por capa.

Reglas recomendadas para próximas fases: `Codigo` único entre Puestos activos y no editable tras creación; `UnidadOrganizativaId` y `CargoId` deben existir y estar activos; `PuestoSuperiorId` debe existir, estar activo y no generar autorreferencia ni ciclos; desactivar un Puesto debe hacer baja lógica y excluirlo de consultas activas; reactivar debe validar conflicto de código y referencias activas. Ocupaciones y Vacantes deben permanecer fuera de API/servicios de Puestos por ahora, aunque existan como navegaciones y FKs en el modelo actual.

### Risks

- La especificación vigente de API declara Puestos como read-only; implementar escritura sin delta spec rompería el contrato SDD.
- `Puesto` ya está acoplado por navegación a `Ocupaciones` y `Vacantes`; aunque no se implementen, las reglas de desactivación deben evitar prometer comportamiento que dependa de esos módulos.
- La jerarquía de `PuestoSuperiorId` necesita validación de ciclos en aplicación/repositorio; el check actual de base de datos solo evita autorreferencia directa.
- Los servicios de consulta actuales asumen que `UnidadOrganizativa` y `Cargo` vienen incluidos; cualquier cambio de repositorio debe preservar ese contrato para no provocar `NullReferenceException` al mapear DTOs.
- MySQL no soporta índices filtrados; la unicidad de código activo debe seguir usando la columna generada `ActiveCodigoUnique` o una estrategia equivalente.
- Como el esquema de Puestos ya existe, una migración nueva puede no ser necesaria; si se agregan índices o constraints adicionales, deben justificarse y probarse contra MySQL/Pomelo.

### Ready for Proposal

Yes — indicar al usuario que Puestos ya existe como lectura y persistencia base. La siguiente fase debería crear una propuesta para convertirlo en módulo CRUD independiente, en español, excluyendo explícitamente Ocupaciones y Vacantes salvo por límites defensivos y reglas de no acoplamiento.
