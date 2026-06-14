# Delta para base de datos SGV

## Requisitos MODIFICADOS

### Requisito: Compatibilidad MySQL/Pomelo y EF Core

El diseño de base de datos DEBE apuntar a MySQL mediante Pomelo Entity Framework Core mientras la aplicación apunta a .NET 10. Los paquetes relacionados con EF Core, incluidos `Microsoft.EntityFrameworkCore*`, Identity EF Core y Pomelo, DEBEN permanecer en versiones 9.x compatibles. SQL Server NO DEBE permanecer como proveedor activo soportado para configuración, migraciones ni documentación actuales.
(Anteriormente: el diseño de base de datos apuntaba a SQL Server y Entity Framework Core sobre .NET 9.)

#### Escenario: Configurar el proveedor de base de datos soportado

- DADO que el proyecto de infraestructura SGV está configurado para persistencia
- CUANDO la aplicación configura el proveedor de base de datos
- ENTONCES DEBE usar el proveedor MySQL/Pomelo
- Y NO DEBE configurar SQL Server como proveedor activo.

#### Escenario: Preservar compatibilidad de paquetes EF Core 9.x sobre .NET 10

- DADO que todos los proyectos SGV apuntan a .NET 10
- CUANDO se restauran las dependencias
- ENTONCES los paquetes relacionados con EF Core DEBEN resolverse a versiones 9.x compatibles
- Y Pomelo DEBE permanecer compatible con el rango del paquete relacional EF Core resuelto.

#### Escenario: Preservar identificadores de entidades existentes

- DADO que el modelo de dominio SGV actual usa identificadores GUID para entidades de aplicación
- CUANDO se crean entidades de aplicación
- ENTONCES sus claves primarias DEBEN seguir usando identificadores GUID salvo que un rediseño de esquema separado cambie ese comportamiento.

#### Escenario: Eliminar supuestos de migración de SQL Server

- DADO que se usan migraciones de persistencia o model snapshots para verificación
- CUANDO se revisan artefactos específicos del proveedor
- ENTONCES los supuestos específicos de SQL Server DEBEN reemplazarse por artefactos compatibles con MySQL.

## Requisitos RENOMBRADOS

### Requisito: Compatibilidad con SQL Server y EF Core → Compatibilidad MySQL/Pomelo y EF Core

(Motivo: el proveedor soportado cambia de SQL Server sobre .NET 9 a MySQL/Pomelo sobre .NET 10.)
(Migración: actualizar referencias, tests, documentación, configuración y artefactos activos de migración al nuevo nombre de requisito y objetivo de proveedor.)
