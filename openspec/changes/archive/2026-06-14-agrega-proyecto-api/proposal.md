# Propuesta: Agregar proyecto API

## Intención

Exponer datos SGV mediante API HTTP externa read-only, con controladores tradicionales, Swagger y Clean Architecture simple. Debe levantar contra MySQL local real y consultar `UnidadesOrganizativas`, `Cargos`, `Puestos` y `Skills` sin autenticación.

## Alcance

### Incluido
- `SGV.Api` con ASP.NET Core MVC Controllers; no Minimal API.
- Swagger/OpenAPI.
- Endpoints read-only para `UnidadesOrganizativas`, `Cargos`, `Puestos` y `Skills`.
- DTOs de respuesta; no exponer entidades dominio/EF.
- Servicios, repositorio genérico básico, repositorios por entidad y Unit of Work.
- DI para API, `SgvDbContext`, Pomelo/MySQL, repositorios, servicios e `IUsuarioActual` anónimo/sistema si aplica.
- Pruebas bajo `strict_tdd: true`.

### No objetivos
- Minimal APIs.
- Auth, JWT, policies, Identity endpoints o `[Authorize]`.
- Operaciones create/update/delete.
- Nuevas reglas de negocio o rediseño del esquema de datos.
- Endpoints administrativos o datos sensibles.

## Capacidades

### Nuevas capacidades
- `sgv-readonly-api`: API externa read-only con controllers tradicionales, Swagger, endpoints de lectura para unidades/cargos/puestos/skills, DTOs, datos reales desde MySQL local y ausencia explícita de auth.

### Capacidades modificadas
- Ninguna; `sgv-database` no cambia.

## Enfoque

Flujo Clean: controllers → services → repositories/unit of work → EF Core/Pomelo. La API compone dependencias; controllers delgados; servicios devuelven DTOs; infraestructura usa `SgvDbContext`. El genérico queda para métodos básicos; includes/filtros/proyecciones viven en repositorios específicos.

## Áreas afectadas

| Área | Impacto | Descripción |
|------|---------|-------------|
| `src/SGV.Api/` | Nuevo | API, controllers, contratos, HTTP/Swagger. |
| `src/SGV.Aplicacion/` | Modificado | Interfaces de servicios, repositorios y Unit of Work. |
| `src/SGV.Infraestructura/` | Modificado | Implementaciones EF Core y DI. |
| `tests/SGV.Tests/` | Modificado | Pruebas de contratos, servicios, repositorios y API. |
| `SGV.slnx` | Modificado | Incluir proyecto API. |

## Riesgos

| Riesgo | Probabilidad | Mitigación |
|--------|--------------|------------|
| Swagger expone API externa sin auth | Media | Limitar v1 a read-only no sensible. |
| Auditoría requiere `IUsuarioActual` | Media | Registrar implementación anónima/sistema. |
| Repositorio genérico degrada consultas | Media | Mantenerlo básico y especializar por entidad. |
| Desalineación EF Core 9.x/Pomelo en .NET 10 | Baja | Respetar versiones existentes 9.x. |

## Plan de rollback

Remover `SGV.Api` de la solución y revertir DI, repositorios, Unit of Work, servicios y pruebas. No se esperan migraciones ni cambios de datos.

## Dependencias

- MySQL local migrado con datos reales/seed.
- ASP.NET Core/Swagger compatibles con `net10.0` y Pomelo 9.x.

## Criterios de éxito

- [ ] `dotnet build` y `dotnet test` pasan.
- [ ] La API levanta localmente contra MySQL real.
- [ ] Swagger muestra endpoints read-only para las cuatro entidades.
- [ ] Las respuestas usan DTOs y no entidades EF/domain directas.
- [ ] No hay Minimal API ni autenticación/autorización en v1.
