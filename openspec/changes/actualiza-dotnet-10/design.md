# Diseño: Actualización a .NET 10 y reemplazo de SQL Server por MySQL/Pomelo

## Enfoque técnico

Redirigir los cuatro proyectos de SGV (`SGV.Dominio`, `SGV.Aplicacion`, `SGV.Infraestructura`, `SGV.Tests`) a `net10.0`, alinear `global.json` con un SDK de .NET 10 y mantener todos los paquetes relacionados con EF Core en versiones `9.x` compatibles. Reemplazar SQL Server como proveedor de persistencia activo por Pomelo/MySQL en infraestructura, migraciones, documentación SQL generada, OpenSpec y guía del repositorio. El comportamiento de negocio/dominio se mantiene sin cambios.

Evidencia: solo `SGV.Infraestructura` posee paquetes EF y configuración de proveedor; `SgvDbContextFactory` usa `UseSqlServer`; las migraciones/snapshot y `docs/migracion-inicial-sgv.sql` son específicos de SQL Server; varias configuraciones de EF usan sintaxis SQL de índices filtrados/check propia de SQL Server.

## Decisiones de arquitectura

| Decisión | Alternativas consideradas | Justificación |
|---|---|---|
| Apuntar a `net10.0` en todos los proyectos y fijar el SDK en `global.json` a un SDK de .NET 10 aprobado por el equipo. | Mantener `net9.0`; eliminar `global.json`. | El alcance requiere .NET 10 en todos lados; fijar el SDK evita deriva accidental de versiones y mantiene restore/Build reproducibles. |
| Mantener `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` y `Pomelo.EntityFrameworkCore.MySql` en `9.x`. | Actualizar paquetes EF a 10.x. | Pomelo 9 depende de EF relational `>= 9.0.0 && < 9.0.999`; EF 10 rompería la compatibilidad del proveedor. |
| Usar Pomelo como único proveedor activo mediante `UseMySql` y una versión configurada de servidor MySQL 8. | Mantener SQL Server en paralelo; usar autodetección de proveedor en todos lados. | La propuesta rechaza el soporte de SQL Server. Una versión fija de servidor mantiene deterministas las migraciones en tiempo de diseño. |
| Reemplazar las migraciones/snapshot actuales de SQL Server por una línea base nueva de migración MySQL/Pomelo. | Editar las migraciones SQL Server en sitio; agregar una migración de datos cross-provider. | Los artefactos existentes son específicos del proveedor y el repositorio parece estar en preproducción. Una línea base limpia del proveedor es más segura que traducir manualmente anotaciones de SQL Server. Si existen datos productivos, exportar/importar es una tarea operativa fuera de este cambio. |
| Adaptar restricciones únicas filtradas a un modelado compatible con MySQL. | Preservar predicados SQL Server `.HasFilter(...)`. | MySQL no soporta índices filtrados de SQL Server. Preservar reglas de negocio mediante índices sobre columnas generadas o patrones equivalentes específicos del proveedor validados contra MySQL. |

## Flujo de datos

```text
Tests / app host
  -> configuration: ConnectionStrings:Default + Database:ServerVersion
  -> SgvDbContextFactory / DI registration
  -> Pomelo UseMySql
  -> MySQL schema generated from EF Core 9 model and Pomelo migrations
```

## Cambios en archivos

| Archivo | Acción | Descripción |
|---|---|---|
| `global.json` | Modificar | Fijar/seleccionar SDK de .NET 10; mantener la política de roll-forward intencional. |
| `src/*/*.csproj`, `tests/SGV.Tests/SGV.Tests.csproj` | Modificar | Cambiar TFMs a `net10.0`; mantener paquetes de test salvo que la verificación demuestre que se requiere una actualización. |
| `src/SGV.Infraestructura/SGV.Infraestructura.csproj` | Modificar | Eliminar `Microsoft.EntityFrameworkCore.SqlServer`; agregar `Pomelo.EntityFrameworkCore.MySql` 9.x; mantener EF/Identity EF 9.x. |
| `src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs` | Modificar | Reemplazar la cadena localdb de `UseSqlServer` por `UseMySql` usando una cadena de conexión MySQL y una versión fija de servidor MySQL 8. |
| `src/SGV.Infraestructura/Persistencia/Configuraciones/*.cs` | Modificar | Reemplazar fragmentos SQL de filtros/checks de SQL Server por expresiones compatibles con MySQL; preservar las restricciones de dominio existentes. |
| `src/SGV.Infraestructura/Persistencia/Migraciones/*` | Reemplazar | Eliminar migraciones/snapshot específicos de SQL Server y generar artefactos Pomelo/MySQL. |
| `docs/migracion-inicial-sgv.sql`, `docs/decisiones-implementacion.md`, `AGENTS.md` | Modificar | Reemplazar guía de SQL Server/.NET 9 por guía de MySQL/Pomelo/.NET 10. |
| `openspec/config.yaml`, `openspec/specs/sgv-database/spec.md` | Modificar | Actualizar el stack fuente de verdad y el requisito de proveedor. |
| `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` | Modificar | Validar restricciones compatibles con el proveedor y comportamiento del modelo de migración, no sintaxis de SQL Server. |

## Interfaces / Contratos

No cambian contratos públicos de dominio. El contrato de configuración de persistencia pasa a ser:

- `ConnectionStrings:Default`: cadena de conexión MySQL.
- `Database:ServerVersion`: versión de MySQL usada por Pomelo para generación SQL, con valor predeterminado MySQL 8.0 en desarrollo.

## Estrategia de Testing

| Capa | Qué probar | Enfoque |
|---|---|---|
| Build/restore | `net10.0` y grafo de paquetes | `dotnet restore`, `dotnet build`; inspeccionar que las versiones EF/Pomelo resueltas permanezcan en 9.x. |
| Unit | Comportamiento de dominio/aplicación | Tests xUnit existentes sin cambios salvo TFM. |
| Modelo de persistencia | Modelo, índices, restricciones y migraciones compatibles con MySQL | Actualizar tests de modelo; agregar verificación de generación de migración/script contra Pomelo. |
| Integración | Comportamiento real del proveedor | Preferir contenedor/instancia local efímera de MySQL 8 para `dotnet test`; omitir o marcar explícitamente cuando MySQL no esté disponible. |

## Migración / Rollout

1. Redirigir TFMs y referencias de paquetes.
2. Reemplazar configuración de proveedor y modelado de restricciones/índices específicos de MySQL.
3. Regenerar migraciones/snapshot Pomelo y script SQL desde el modelo EF.
4. Actualizar OpenSpec/docs y ejecutar búsqueda en el repositorio de referencias a SQL Server.
5. Verificar restore/Build/tests y, cuando esté disponible, aplicar migraciones sobre una base MySQL limpia.

Rollback: revertir los Commits de implementación para restaurar `net9.0`, paquetes/proveedor SQL Server, migraciones/snapshot previos, docs y specs. Si se aplicó el esquema MySQL, eliminar/recrear la base MySQL desde backup o descartar la base limpia de test.

## Riesgos

- La brecha de índices filtrados de MySQL puede debilitar reglas de unicidad si se modela de manera superficial; los tests deben probar comportamiento equivalente.
- La deriva de versiones Pomelo/EF hacia 10.x romperá la compatibilidad; la verificación de paquetes es obligatoria.
- La versión exacta del servidor MySQL y la disponibilidad de base de datos en CI aún no están definidas; el diseño asume MySQL 8.x y verificación local/efímera.

## Preguntas abiertas

- [ ] ¿En qué versión exacta de MySQL 8.x deberían estandarizarse CI y el desarrollo local?
