## Exploración: Actualizar proyectos .NET a .NET 10 manteniendo EF Core 9 por compatibilidad con Pomelo

### Estado actual
La solución usa `SGV.slnx` con cuatro proyectos .NET: `SGV.Dominio`, `SGV.Aplicacion`, `SGV.Infraestructura` y `SGV.Tests`. Todos los proyectos actualmente apuntan a `net9.0`. `global.json` fija el SDK `9.0.100` con `rollForward: latestMajor`, pero la máquina local solo tiene instalados SDKs/runtimes .NET 10 (`10.0.203`, `10.0.300`; runtimes `10.0.7`, `10.0.8`). El comando actual `dotnet test --no-restore --no-build` falla porque el testhost existente de `net9.0` requiere `Microsoft.NETCore.App 9.0.0`, que no está instalado.

`SGV.Infraestructura` es el único proyecto con referencias a paquetes relacionados con EF. Hoy usa paquetes EF Core 9.0.0 y paquetes del proveedor SQL Server: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer` y `Microsoft.EntityFrameworkCore.Design`. Pomelo no está referenciado actualmente. La metadata NuGet de `Pomelo.EntityFrameworkCore.MySql 9.0.0` muestra que depende de `Microsoft.EntityFrameworkCore.Relational >= 9.0.0 && < 9.0.999`, por lo que actualizar paquetes EF a 10.x rompería la compatibilidad con Pomelo 9. Mantener paquetes EF en 9.x mientras se apunta a `net10.0` es la dirección correcta de compatibilidad.

OpenSpec actualmente describe el stack como `.NET 9`, `Entity Framework Core 9.0` y `SQL Server`, y la spec de base de datos tiene un requisito que dice explícitamente SQL Server y EF Core sobre .NET 9. Una fase posterior de spec/propuesta debería decidir si este cambio es solo una actualización runtime/TFM o si también inicia la migración de proveedor hacia Pomelo/MySQL.

### Áreas afectadas
- `global.json` — el SDK fijado es `9.0.100`; debería moverse a una versión SDK de .NET 10 disponible para el equipo o eliminar el pinning de forma intencional.
- `SGV.slnx` — incluye los cuatro proyectos que deben permanecer alineados para Build/test.
- `src/SGV.Dominio/SGV.Dominio.csproj` — apunta a `net9.0`; sin implicancias de paquetes.
- `src/SGV.Aplicacion/SGV.Aplicacion.csproj` — apunta a `net9.0`; referencia el proyecto de dominio.
- `src/SGV.Infraestructura/SGV.Infraestructura.csproj` — apunta a `net9.0`; posee versiones de paquetes EF/Identity/proveedor que deberían permanecer en 9.x por compatibilidad con Pomelo.
- `tests/SGV.Tests/SGV.Tests.csproj` — apunta a `net9.0`; actualmente el testhost no puede ejecutar sin runtime .NET 9. Redirigir a `net10.0` elimina ese bloqueo local de runtime.
- `src/SGV.Infraestructura/Persistencia/SgvDbContextFactory.cs` — actualmente configura `UseSqlServer`, que no está relacionado con la actualización TFM pero sí es relevante si la migración Pomelo/MySQL entra en alcance.
- `src/SGV.Infraestructura/Persistencia/Migraciones/` — las migraciones existentes y el model snapshot contienen anotaciones específicas de SQL Server; la migración de proveedor requeriría trabajo separado de diseño/spec.
- `openspec/config.yaml` y `openspec/specs/sgv-database/spec.md` — actualmente documentan supuestos de .NET 9 y SQL Server que quedarían obsoletos después de este cambio.
- `docs/decisiones-implementacion.md` — documenta la decisión previa de apuntar a `net9.0` porque localmente solo estaba instalado SDK 10.

### Enfoques
1. **Actualización mínima de TFM/SDK, manteniendo proveedor de persistencia sin cambios** — Redirigir todos los proyectos a `net10.0`, actualizar `global.json` a un SDK de .NET 10 y mantener paquetes EF/Identity/SQL Server en 9.x.
   - Pros: Cambio seguro más pequeño; resuelve el bloqueo local de tests por runtime .NET 9; preserva migraciones y comportamiento SQL Server; mantiene compatibilidad futura con Pomelo 9 evitando EF 10.
   - Contras: OpenSpec y docs deben actualizarse para dejar de indicar `.NET 9`; el proveedor SQL Server permanece, por lo que la compatibilidad con Pomelo queda protegida pero no implementada.
   - Esfuerzo: Bajo

2. **Actualización TFM/SDK más preparación de proveedor Pomelo** — Redirigir a `net10.0`, mantener paquetes EF en 9.x, agregar `Pomelo.EntityFrameworkCore.MySql 9.0.0` y empezar a reemplazar configuración específica del proveedor SQL Server.
   - Pros: Avanza hacia el objetivo declarado de Pomelo.
   - Contras: Cambio de comportamiento mayor; las migraciones/snapshots SQL Server existentes pasan a ser deuda específica del proveedor; la spec de base de datos actualmente dice SQL Server, por lo que esto necesita propuesta/spec/diseño explícitos antes de implementar.
   - Esfuerzo: Medio/Alto

3. **Actualizar todo a .NET/EF 10** — Redirigir a `net10.0` y mover paquetes EF a 10.x.
   - Pros: Usa la línea más reciente de EF Core.
   - Contras: Entra en conflicto con el rango de dependencias de Pomelo 9.0.0 (`< 9.0.999`); no está alineado con la solicitud del usuario.
   - Esfuerzo: Bajo técnicamente, pero no viable para la restricción indicada

### Recomendación
Avanzar con el Enfoque 1 para este cambio: actualizar todos los TFMs de proyecto y la configuración SDK a .NET 10, fijando intencionalmente todos los paquetes relacionados con EF en `SGV.Infraestructura` a EF Core/Identity EF Core 9.x. Tratar la incorporación de Pomelo y el cambio de SQL Server a MySQL como un cambio SDD separado, salvo que la propuesta amplíe explícitamente el alcance de este cambio, porque la migración de proveedor toca configuración, migraciones, anotaciones de modelo, estrategia de testing y requisitos OpenSpec.

### Riesgos
- EF Core 9 puede referenciarse desde `net10.0`, pero las actualizaciones de paquetes deben evitar upgrades accidentales de `Microsoft.EntityFrameworkCore*` a 10.x.
- La fuente de verdad actual de OpenSpec dice SQL Server y .NET 9; las fases posteriores deben actualizar specs/docs o excluirlas intencionalmente del alcance.
- Las migraciones SQL Server existentes y `UseSqlServer` siguen siendo incompatibles con un cambio real a proveedor Pomelo/MySQL.
- Los paquetes de test son antiguos (`Microsoft.NET.Test.Sdk 17.12.0`, xUnit 2.9.2); podrían ejecutar bajo .NET 10, pero la verificación debe confirmarlo y actualizar paquetes de test solo si es necesario.
- `global.json` debe elegir una versión SDK disponible en CI/máquinas de desarrollo; fijar `10.0.300` coincide con esta máquina, pero puede no coincidir con otros entornos.

### Listo para propuesta
Sí — la propuesta debería acotar esto como una actualización TFM/SDK a .NET 10 con paquetes EF Core mantenidos intencionalmente en 9.x por compatibilidad con Pomelo. Debe marcar explícitamente la migración completa de proveedor Pomelo/MySQL como no objetivo, salvo que el usuario confirme que esa migración debe incluirse ahora.
