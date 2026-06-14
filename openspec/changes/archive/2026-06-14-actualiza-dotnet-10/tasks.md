# Tareas: Actualización a .NET 10 y reemplazo de SQL Server por MySQL/Pomelo

## Pronóstico de carga de revisión

| Campo | Valor |
|-------|-------|
| Líneas cambiadas estimadas | 900-1600 |
| Riesgo de presupuesto de 400 líneas | Alto |
| Chained PRs recomendados | Sí |
| División sugerida | PR 1 redirección runtime/paquetes → PR 2 proveedor/tests/modelado → PR 3 migraciones/docs |
| Estrategia de entrega | ask-on-risk |
| Estrategia de cadena | pendiente |

Decisión necesaria antes de apply: Sí
Chained PRs recomendados: Sí
Estrategia de cadena: pendiente
Riesgo de presupuesto de 400 líneas: Alto

### Unidades de trabajo sugeridas

| Unidad | Objetivo | PR probable | Notas |
|------|------|-----------|-------|
| 1 | Redirigir SDK/TFMs y fijar EF 9.x | PR 1 | Base única revisable con smoke de restore/Build/test |
| 2 | Probar e implementar comportamiento del proveedor Pomelo/MySQL | PR 2 | Depende del PR 1; mantener tests RED/GREEN/REFACTOR con cambios de modelo |
| 3 | Reemplazar migraciones y actualizar guía de docs/config | PR 3 | Depende del PR 2; el diff de migración generado probablemente domine el tamaño |

## Fase 1: Fundación

- [x] 1.1 Actualizar `global.json`, `src/SGV.Dominio/SGV.Dominio.csproj`, `src/SGV.Aplicacion/SGV.Aplicacion.csproj`, `src/SGV.Infraestructura/SGV.Infraestructura.csproj` y `tests/SGV.Tests/SGV.Tests.csproj` a `net10.0`.
- [x] 1.2 Reemplazar `Microsoft.EntityFrameworkCore.SqlServer` por `Pomelo.EntityFrameworkCore.MySql` en `src/SGV.Infraestructura/SGV.Infraestructura.csproj`, manteniendo paquetes EF/Identity/Design en 9.x.
- [x] 1.3 Verificar resolución de paquetes restore/Build en `SGV.slnx` para que las dependencias EF relational permanezcan en 9.x bajo .NET 10.

## Fase 2: RED

- [x] 2.1 Extender `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` con aserciones fallidas para selección de proveedor Pomelo y comportamiento de unicidad/filtros seguro para MySQL desde los escenarios de spec.
- [x] 2.2 Agregar un test fallido de verificación de migración/script en `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` o en un archivo de test de persistencia hermano para artefactos compatibles con MySQL.
- [x] 2.3 Marcar o condicionar la ruta de test de proveedor real en `tests/SGV.Tests/Persistencia/` para que la verificación MySQL 8 sea explícita cuando el servidor no esté disponible.

## Fase 3: GREEN

- [x] 3.1 Actualizar `src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs` para usar `UseMySql`, `ConnectionStrings:Default` y `Database:ServerVersion` con una versión fija de servidor MySQL 8.
- [x] 3.2 Refactorizar modelado EF específico de SQL Server en `src/SGV.Infraestructura/Persistencia/Configuraciones/CargoConfiguracion.cs`, `PuestoConfiguracion.cs`, `PostulanteConfiguracion.cs`, `UnidadOrganizativaConfiguracion.cs` y archivos relacionados que usan fragmentos SQL `HasFilter`/check.
- [x] 3.3 Reemplazar `src/SGV.Infraestructura/Persistencia/Migraciones/20260613022804_InicialSgvo*.cs`, `20260613022933_AgregarDatosSemillaBase*.cs` y `SgvDbContextModelSnapshot.cs` por una línea base Pomelo nueva.

## Fase 4: REFACTOR + Verificar

- [x] 4.1 Limpiar tests de persistencia e infraestructura de soporte en `tests/SGV.Tests/Persistencia/ModeloPersistenciaTests.cs` y `src/SGV.Infraestructura/Persistencia/SgvDbContext.cs` después de que GREEN pase, sin cambiar comportamiento.
- [x] 4.2 Actualizar `docs/migracion-inicial-sgv.sql`, `docs/decisiones-implementacion.md`, `AGENTS.md` y `openspec/config.yaml` para indicar .NET 10 + MySQL/Pomelo + EF Core 9.x.
- [x] 4.3 Ejecutar un barrido de referencias SQL Server en todo el repositorio y verificar que `dotnet test` cubre los escenarios modificados de `sgv-database` antes de transferir a `sdd-apply`.
