# Propuesta: Actualización a .NET 10 y reemplazo de SQL Server por MySQL/Pomelo

## Intención

Mover SGV a .NET 10 y cambiar la persistencia de SQL Server a MySQL/Pomelo. Los paquetes relacionados con EF Core deben permanecer en 9.x porque Pomelo 9 requiere paquetes relacionales EF Core `< 9.0.999`.

## Alcance

### En alcance
- Redirigir todos los proyectos .NET a `net10.0` y alinear la configuración SDK.
- Reemplazar proveedor SQL Server, configuración, migraciones, docs y specs por MySQL/Pomelo.
- Mantener `Microsoft.EntityFrameworkCore*`, Identity EF Core y paquetes Pomelo en 9.x, no 10.x.

### Fuera de alcance
- Actualizar paquetes EF Core a 10.x.
- Rediseñar el modelo de dominio SGV o cambiar comportamiento de negocio.
- Hosting productivo, automatización de backups o aprovisionamiento cloud.

## No objetivos

- No preservar SQL Server como proveedor activo soportado.
- No reescribir artefactos archivados de auditoría OpenSpec salvo que una fase posterior requiera notas.

## Capacidades

### Nuevas capacidades
- Ninguna.

### Capacidades modificadas
- `sgv-database`: actualizar compatibilidad de SQL Server/.NET 9 a MySQL/Pomelo sobre .NET 10 preservando EF Core 9.x.

## Enfoque

Redirigir proyectos y configuración SDK, reemplazar `Microsoft.EntityFrameworkCore.SqlServer` y `UseSqlServer`, luego regenerar o reemplazar migraciones/snapshot específicos del proveedor. Actualizar configuración/specs activas de OpenSpec y docs para indicar .NET 10, MySQL/Pomelo y EF Core 9.x.

## Áreas afectadas

| Área | Impacto | Descripción |
|------|--------|-------------|
| `global.json`, `*.csproj` | Modificado | Alineación TFM/SDK de .NET 10; paquetes EF permanecen en 9.x. |
| `src/SGV.Infraestructura/` | Modificado | Paquete Pomelo, proveedor DbContext, migraciones. |
| `tests/SGV.Tests/` | Modificado | Redirección TFM y actualizaciones de verificación de persistencia. |
| `openspec/config.yaml`, `openspec/specs/sgv-database/spec.md` | Modificado | Reemplazar supuestos obsoletos de .NET 9/SQL Server. |
| `docs/`, `AGENTS.md` | Modificado | Reemplazar guía actual de SQL Server. |

## Riesgos

| Riesgo | Probabilidad | Mitigación |
|------|------------|------------|
| EF 10 instalado accidentalmente | Media | Fijar paquetes en 9.x y verificar dependencias. |
| La migración de proveedor cambia el esquema | Alta | Revisar migraciones/snapshot y ejecutar tests compatibles con MySQL. |
| Permanecen referencias a SQL Server | Media | Buscar términos de SQL Server/proveedor en el repositorio antes de la verificación. |

## Plan de Rollback

Revertir el/los Commit(s) del cambio: restaurar `net9.0`, paquete/proveedor SQL Server, configuración, migraciones/snapshot previos y referencias OpenSpec/docs anteriores. Si se aplicó la migración MySQL, restaurar desde el backup previo a la migración o recrear desde la línea base previa de SQL Server.

## Dependencias

- SDK .NET 10 disponible en desarrollo y CI.
- `Pomelo.EntityFrameworkCore.MySql` 9.x y paquetes EF Core 9.x compatibles.
- Objetivo de verificación MySQL o estrategia de conexión de test.

## Criterios de éxito

- [ ] Todos los proyectos apuntan a `net10.0`; los paquetes relacionados con EF Core permanecen en 9.x.
- [ ] Supuestos de proveedor/configuración/migración de SQL Server son reemplazados por Pomelo/MySQL.
- [ ] La spec `sgv-database` refleja MySQL/Pomelo sobre .NET 10 con EF Core 9.x.
- [ ] La verificación de Build y tests pasa para la solución actualizada.
