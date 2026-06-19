# Design: Módulo administrable de Habilidades

## Enfoque técnico

El cambio promueve `Habilidad` de catálogo solo lectura a catálogo administrable, reutilizando la arquitectura existente de Cargos/Puestos: dominio con reglas de ciclo de vida, servicios de aplicación para comandos, repositorio EF Core/Pomelo, controlador en `/api/v1/skills` y pruebas por capa. El alcance se limita al catálogo maestro; no se agregan flujos de `CargoHabilidad` ni `PersonaHabilidad`.

## Decisiones de arquitectura

| Decisión | Alternativa | Resolución y fundamento |
|---|---|---|
| Mantener `/api/v1/skills` | Crear ruta en español | Se conserva la ruta canónica aprobada y el contrato público existente. |
| Servicio de comandos dedicado | CQRS con handlers por comando | Seguir el patrón actual (`CargoServicioComandos`, `PuestoServicioComandos`) reduce fricción y mantiene consistencia. |
| `Codigo` inmutable | Permitir edición con validación | El request de actualización no incluirá `Codigo`; la entidad tendrá método de actualización solo para `Nombre`, `Categoria`, `Descripcion`. |
| Baja lógica + reactivación | Eliminación física | Preserva referencias existentes y compatibilidad con auditoría (`IsActive`, `IsDeleted`, `DeletedAt`). |
| Unicidad activa por estrategia MySQL | Índice filtrado | MySQL no soporta índices filtrados; se preserva `ActiveCodigoUnique` generado sobre `IsDeleted = 0`. |

## Flujo de datos

```text
HTTP /api/v1/skills
  -> SkillsController
  -> IHabilidadServicioConsulta / IHabilidadServicioComandos
  -> IHabilidadRepository + IUnitOfWork
  -> SgvDbContext / Habilidades
  -> HabilidadDto o ProblemDetails
```

Las consultas existentes seguirán devolviendo habilidades activas. Las escrituras cargarán entidades activas para actualizar/desactivar y entidades incluyendo eliminadas para reactivar.

## Cambios de archivos previstos

| Archivo | Acción | Descripción |
|---|---|---|
| `src/SGV.Dominio/Habilidades/Habilidad.cs` | Modificar | Agregar `Actualizar`, `Desactivar`, `Activar`; dejar `Codigo` solo en constructor. |
| `src/SGV.Aplicacion/Habilidades/Comandos/*` | Crear | Requests, interfaz, servicio, resultado tipado y validadores FluentValidation. |
| `src/SGV.Aplicacion/Habilidades/Consultas/IHabilidadRepository.cs` | Modificar | Extender contrato con `AddAsync`, `UpdateAsync`, `DeleteAsync`, `ReactivateAsync`, `GetByIdForUpdateAsync`, `GetByIdIncludingDeletedAsync`, `ExistsActiveCodeAsync`. |
| `src/SGV.Infraestructura/Persistencia/Repositorios/HabilidadRepository.cs` | Modificar | Implementar métodos de escritura y consultas activas/incluyendo eliminadas. |
| `src/SGV.Infraestructura/Persistencia/Mapeos/DomainToPersistenceMapper.cs` | Modificar | Agregar mapeo `Habilidad` ↔ `HabilidadEntity` para escritura. |
| `src/SGV.Infraestructura/DependencyInjection.cs` | Modificar | Registrar `IHabilidadServicioComandos`. |
| `src/SGV.Api/Controllers/SkillsController.cs` | Modificar | Agregar endpoints POST, PUT, DELETE y PATCH reactivar. |
| `tests/SGV.Tests/**/*Habilidad*` | Modificar/crear | Cubrir dominio, aplicación, persistencia y API. |

## Contratos e interfaces

Endpoints bajo `/api/v1/skills`:

| Método | Ruta | Resultado esperado |
|---|---|---|
| `GET` | `/` | `200` con habilidades activas. |
| `GET` | `/{id:guid}` | `200` o `404`. |
| `POST` | `/` | `201 Created`, `400 ValidationProblemDetails`, `409 ProblemDetails`. |
| `PUT` | `/{id:guid}` | `200`, `400`, `404`. No acepta `Codigo`. |
| `DELETE` | `/{id:guid}` | `204` o `404`. Baja lógica. |
| `PATCH` | `/{id:guid}/reactivar` | `200`, `404`, `409` por código activo duplicado. |

Errores de aplicación: `NotFound`, `Conflict`, `Validation`, con `Code`, `Message` y errores por campo en camelCase. Validaciones: `Codigo` requerido/máx. 50 solo al crear; `Nombre` requerido/máx. 200; `Categoria` opcional/máx. 100; `Descripcion` opcional/máx. 1000.

## Persistencia y migración

`HabilidadConfiguracion` ya define longitudes, índice por `Categoria` y columna generada `ActiveCodigoUnique`. La fase de implementación debe verificar si la migración vigente contiene esa columna; si no, generar migración Pomelo/MySQL idempotente. Reactivar debe validar conflicto antes de persistir y dejar que el índice único sea defensa final ante carreras.

## Estrategia de pruebas

| Capa | Qué probar | Enfoque |
|---|---|---|
| Dominio | Inmutabilidad de `Codigo`, actualización, desactivar/reactivar, longitudes. | xUnit unitario. |
| Aplicación | Validación, duplicados activos, no encontrado, reactivación con/sin conflicto. | Servicios con repositorios fake o dobles. |
| Infraestructura | Escritura, baja lógica, reactivación, unicidad activa MySQL. | `MySqlFact` con EF/Pomelo. |
| API | Códigos HTTP, `ProblemDetails`, ruta `/api/v1/skills`, ausencia de asignaciones. | `ApiWebApplicationFactory`. |

## Workflow y tamaño de revisión

Las fases futuras deben usar rama nueva. Riesgo de superar 400 líneas: medio. Si el forecast de tasks excede el presupuesto, separar en slices: dominio/aplicación, persistencia, API/pruebas. No implementar asignaciones en este cambio.

## Preguntas abiertas

- Ninguna bloqueante para diseñar; permisos/autorización quedan fuera de alcance hasta definir seguridad.
