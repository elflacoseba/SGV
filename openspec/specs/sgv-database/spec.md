# Especificación de Base de Datos SGV

## Requisitos AGREGADOS

### Requisito: Jerarquía de Unidades Organizativas

El sistema DEBE persistir unidades organizativas en una jerarquía padre-hijo que permita representar un árbol organizacional completo. La aplicación o la base de datos DEBE rechazar relaciones que generen ciclos.
(Anteriormente: la jerarquía exigía persistir relaciones padre-hijo y evitar padre propio, pero no explicitaba ciclos por descendientes.)

#### Escenario: Crear unidad hija

- **DADO** que existe una unidad organizativa
- **CUANDO** se crea una nueva unidad con esa unidad como padre
- **ENTONCES** la nueva unidad DEBE referenciar a la unidad padre
- **Y** debe ser posible recorrer el árbol desde el padre hacia sus hijos.

#### Escenario: Evitar padre propio

- **CUANDO** una unidad se guarda con ella misma como padre
- **ENTONCES** la base de datos o la aplicación DEBE rechazar el cambio.

#### Escenario: Evitar padre descendiente

- **DADO** que una unidad organizativa tiene descendientes
- **CUANDO** se guarda usando uno de sus descendientes como padre
- **ENTONCES** el sistema DEBE rechazar el cambio.

### Requisito: Unicidad de Código Activo de Unidad Organizativa

El sistema MUST impedir que dos unidades organizativas activas compartan el mismo `Codigo`.

#### Escenario: Rechazar código activo duplicado

- DADO que existe una unidad organizativa activa con un `Codigo`
- CUANDO se guarda otra unidad activa con el mismo `Codigo`
- ENTONCES el sistema DEBE rechazar el cambio.

#### Escenario: Permitir reutilización tras baja lógica

- DADO que una unidad organizativa con un `Codigo` fue dada de baja lógicamente
- CUANDO se guarda una nueva unidad activa con ese `Codigo`
- ENTONCES el sistema PUEDE permitir el cambio si no existe otra unidad activa con ese `Codigo`.

### Requisito: Baja Lógica de Unidad Organizativa

El sistema MUST conservar las unidades organizativas eliminadas de forma lógica y excluirlas de las consultas activas.

#### Escenario: Ocultar unidad dada de baja

- DADO que una unidad organizativa fue dada de baja lógicamente
- CUANDO se consultan unidades activas
- ENTONCES la unidad dada de baja NO DEBE aparecer en los resultados.

### Requisito: Cargos Reutilizables con Ciclo de Vida

El sistema DEBE mantener cargos como tipos de puesto reutilizables e independientes de los puestos concretos, con ciclo de vida de alta lógica, actualización de campos editables e inmutabilidad de `Codigo`.
(Anteriormente: el requisito solo cubría reutilización sin gestión de ciclo de vida ni inmutabilidad de Codigo.)

#### Escenario: Reutilizar cargo en varios puestos

- **DADO** un cargo llamado Director
- **CUANDO** varios puestos concretos requieren ese cargo
- **ENTONCES** cada puesto DEBE referenciar el mismo cargo.

#### Escenario: Codigo inmutable tras creación

- **DADO** un cargo existente con `Codigo` "DIR01"
- **CUANDO** se intenta modificar el campo `Codigo`
- **ENTONCES** el sistema NO DEBE permitir la modificación
- **Y** el endpoint de actualización NO DEBE incluir `Codigo` como campo editable.

#### Escenario: Baja lógica de Cargo

- **DADO** un cargo activo
- **CUANDO** se solicita desactivar el cargo
- **ENTONCES** el sistema DEBE marcar el cargo como inactivo sin eliminación física
- **Y** el cargo desactivado NO DEBE aparecer en consultas de cargos activos.

#### Escenario: Reactivación de Cargo conservando Codigo

- **DADO** un cargo desactivado con `Codigo` "DIR01"
- **Y** no existe otro cargo activo con `Codigo` "DIR01"
- **CUANDO** se solicita reactivar el cargo
- **ENTONCES** el sistema DEBE restaurar el estado activo
- **Y** el cargo DEBE conservar su `Codigo` original.

### Requisito: Cargos Referencian NivelCargo por FK

El sistema DEBE reemplazar la columna string `Cargos.Nivel` por una FK `Cargos.NivelId` (char(36) NOT NULL) que referencie `NivelesCargo.Id` con `OnDelete(Restrict)`. La columna `NivelId` DEBE estar indexada. La columna string `Nivel` DEBE eliminarse tras la migración.
(Anteriormente: Cargo usaba un campo string `Nivel` sin normalización.)

#### Escenario: FK de NivelCargo en Cargos

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta la estructura de `Cargos`
- **ENTONCES** la columna `NivelId` DEBE existir como `char(36) NOT NULL`
- **Y** DEBE tener una FK hacia `NivelesCargo.Id` con `OnDelete(Restrict)`
- **Y** la columna string `Nivel` NO DEBE existir.

#### Escenario: Eliminación de NivelCargo con Cargo referenciando

- **DADO** que existe un Cargo que referencia un NivelCargo
- **CUANDO** se intenta eliminar el NivelCargo
- **ENTONCES** MySQL DEBE rechazar la operación por violación de FK.

### Requisito: Unicidad de Codigo Activo de Cargo

El sistema DEBE impedir que dos cargos activos compartan el mismo `Codigo`, permitiendo que un mismo `Codigo` se reuse si el cargo previo fue desactivado.

#### Escenario: Rechazar codigo activo duplicado

- **DADO** que existe un cargo activo con un `Codigo`
- **CUANDO** se guarda otro cargo activo con el mismo `Codigo`
- **ENTONCES** el sistema DEBE rechazar el cambio.

#### Escenario: Permitir reutilización tras baja lógica

- **DADO** que un cargo con un `Codigo` fue dado de baja lógicamente
- **CUANDO** se crea un nuevo cargo activo con ese `Codigo`
- **ENTONCES** el sistema PUEDE permitir el cambio si no existe otro cargo activo con ese `Codigo`.

### Requisito: Catálogo `NivelesCargo` con FK `OnDelete(Restrict)`

El sistema DEBE persistir un catálogo inmutable `NivelesCargo` con PK `Id` Guid (`char(36)`), `Codigo varchar(50)` `UNIQUE NOT NULL`, `Nombre varchar(100) NOT NULL`, `ValorNumerico tinyint NOT NULL` con check constraint, y `Orden int NOT NULL`. El catálogo NO DEBE tener columnas `IsActive` ni `IsDeleted`. La columna `Cargos.NivelId` DEBE ser una FK `char(36) NOT NULL` con `OnDelete(Restrict)` y DEBE estar indexada.

#### Escenario: Enforcement de la FK

- **DADO** que existe un `Cargo` que referencia el nivel con id `X`
- **CUANDO** se ejecuta `DELETE FROM NivelesCargo WHERE id = X`
- **ENTONCES** MySQL DEBE rechazar la operación con un error de foreign key constraint
- **Y** la fila `X` DEBE permanecer en la tabla.

#### Escenario: Índice sobre la FK

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `SHOW INDEX FROM Cargos`
- **ENTONCES** DEBE existir un índice sobre la columna `NivelId`
- **Y** ese índice DEBE ser el que usa la FK en `REFERENCES`.

#### Escenario: Catálogo sin flags de estado

- **DADO** que existe la tabla `NivelesCargo`
- **CUANDO** se consultan sus columnas con `DESCRIBE NivelesCargo`
- **ENTONCES** NO DEBE existir una columna `IsActive` ni una columna `IsDeleted`.

#### Escenario: Check constraint de ValorNumerico

- **DADO** que la tabla `NivelesCargo` existe
- **CUANDO** se inserta un registro con `ValorNumerico` fuera del rango permitido
- **ENTONCES** MySQL DEBE rechazar la inserción por la restricción check.

### Requisito: Migración fail-loud con pre-flight de Nivel a NivelId

La migración que introduce la FK `NivelId` DEBE ejecutar un `SELECT` de pre-flight que liste todo valor distinto de `Cargos.Nivel` (string) que no se corresponda con un `Codigo` del seed de NivelesCargo. Si existe al menos un valor ofensivo, la migración DEBE abortar lanzando `InvalidOperationException` con un mensaje que liste los valores ofensivos, sin hacer backfill ni `DROP COLUMN`. Si no hay valores ofensivos, el backfill completa, la columna FK queda `NOT NULL` y la columna string se elimina.

#### Escenario: Backfill limpio de Nivel a NivelId

- **DADO** que todas las filas existentes en `Cargos.Nivel` tienen un valor que coincide con un `Codigo` del seed de NivelesCargo
- **CUANDO** la migración corre
- **ENTONCES** el backfill de `NivelId` desde el `Codigo` DEBE completarse
- **Y** la columna `NivelId` DEBE quedar `NOT NULL`
- **Y** la columna string `Nivel` DEBE eliminarse con `DROP COLUMN`.

#### Escenario: Fail-loud aborta antes del ALTER

- **DADO** que al menos una fila tiene `Nivel = "FooBar"` (un valor que no aparece en el seed de códigos de NivelesCargo)
- **CUANDO** la migración corre
- **ENTONCES** DEBE lanzar `InvalidOperationException`
- **Y** el mensaje de la excepción DEBE listar los valores ofensivos
- **Y** la migración DEBE detenerse antes de cualquier `ALTER TABLE` que cambie `NivelId` a `NOT NULL`
- **Y** la columna `Nivel` (string) DEBE permanecer intacta en la base de datos.

#### Escenario: Seed de NivelesCargo presente después de la migración

- **DADO** que la migración corrió sobre una base de datos limpia
- **CUANDO** se consulta `SELECT COUNT(*) FROM NivelesCargo`
- **ENTONCES** el resultado DEBE corresponder a la cantidad de niveles definidos en el seed.

### Requisito: Habilidades Requeridas

El sistema DEBE soportar habilidades requeridas por cargo con nivel requerido, importancia e indicador de obligatoriedad.

#### Escenario: Configurar habilidades de un cargo

- **DADO** un cargo
- **CUANDO** se agrega una habilidad requerida
- **ENTONCES** se DEBE almacenar la habilidad, el nivel requerido, la ponderación de importancia y si es obligatoria.

### Requisito: Habilidades de Personas

El sistema DEBE soportar habilidades poseídas por personas con su nivel de dominio.

#### Escenario: Registrar habilidad de una persona

- **DADO** una persona y una habilidad
- **CUANDO** se asigna la habilidad a la persona
- **ENTONCES** la asignación DEBE almacenar el nivel de la persona para esa habilidad.

### Requisito: Puestos Concretos

El sistema DEBE persistir puestos concretos pertenecientes a unidades organizativas y basados en cargos. Cada puesto activo DEBE tener `Codigo` y `Nombre` obligatorios, DEBE conservarse mediante baja lógica cuando se elimina y DEBE poder reactivarse si no viola la unicidad activa de `Codigo`. `PuestoSuperiorId` DEBE ser opcional y NO DEBE requerir reglas jerárquicas complejas en esta versión, salvo impedir autorreferencia directa si el modelo ya lo soporta. La gestión de Ocupaciones y Vacantes NO DEBE formar parte de este cambio.
(Previously: el requisito solo exigía persistir puestos asociados a unidades organizativas y cargos, sin ciclo de vida, unicidad activa ni contrato mínimo de campos.)

#### Escenario: Puesto sin ocupante

- **DADO** que existe un puesto
- **CUANDO** no tiene ocupación activa
- **ENTONCES** el puesto DEBE seguir siendo válido y disponible para gestionar vacantes.

#### Escenario: Persistir puesto activo con campos mínimos

- **DADO** que existen una unidad organizativa y un cargo válidos
- **CUANDO** se guarda un puesto con `Codigo` y `Nombre`
- **ENTONCES** el puesto DEBE persistirse activo asociado a esa unidad y cargo.

#### Escenario: Rechazar campos obligatorios faltantes

- **DADO** una solicitud para guardar un puesto
- **CUANDO** falta `Codigo` o `Nombre`
- **ENTONCES** el sistema DEBE rechazar el cambio
- **Y** NO DEBE persistir un puesto incompleto.

#### Escenario: Unicidad de Codigo entre puestos activos

- **DADO** que existe un puesto activo con un `Codigo`
- **CUANDO** se guarda otro puesto activo con el mismo `Codigo`
- **ENTONCES** el sistema DEBE rechazar el cambio.

#### Escenario: Permitir reutilización de Codigo tras baja lógica

- **DADO** que un puesto con un `Codigo` fue dado de baja lógicamente
- **CUANDO** se crea o reactiva un puesto con ese `Codigo`
- **ENTONCES** el sistema PUEDE permitir el cambio si no existe otro puesto activo con ese `Codigo`.

#### Escenario: Baja lógica de puesto

- **DADO** que existe un puesto activo
- **CUANDO** se solicita eliminarlo
- **ENTONCES** el sistema DEBE marcarlo como inactivo sin eliminación física
- **Y** NO DEBE aparecer en consultas de puestos activos.

#### Escenario: Reactivación de puesto

- **DADO** que existe un puesto inactivo sin conflicto de `Codigo` activo
- **CUANDO** se solicita reactivarlo
- **ENTONCES** el sistema DEBE restaurar su estado activo
- **Y** DEBE conservar sus relaciones a unidad organizativa, cargo y puesto superior opcional.

#### Escenario: Puesto superior opcional

- **DADO** una solicitud para guardar un puesto sin `PuestoSuperiorId`
- **CUANDO** los demás campos requeridos son válidos
- **ENTONCES** el sistema DEBE permitir guardar el puesto sin superior.

### Requisito: Historial de Ocupaciones

El sistema DEBE persistir ocupaciones históricas de persona a puesto con fecha de inicio y fecha de finalización.

#### Escenario: Ocupación vigente

- **DADO** que un puesto tiene historial de ocupaciones
- **CUANDO** una ocupación no tiene fecha de finalización
- **ENTONCES** esa ocupación DEBE representar el ocupante vigente.

#### Escenario: Ocupación anterior

- **DADO** que una ocupación tiene fecha de finalización
- **CUANDO** se consulta el historial del puesto para una fecha dentro de su rango
- **ENTONCES** el sistema DEBE identificar la persona que ocupaba el puesto en ese momento.

### Requisito: Gestión de Vacantes

El sistema DEBE persistir vacantes para puestos con fecha de apertura, motivo, estado e historial de estados.

#### Escenario: Abrir vacante

- **DADO** que un puesto requiere cobertura
- **CUANDO** se abre una vacante
- **ENTONCES** la vacante DEBE referenciar el puesto, fecha de apertura, motivo y estado actual.

### Requisito: Postulantes y Postulaciones

El sistema DEBE soportar postulantes internos y externos, y mantener sus postulaciones a vacantes.

#### Escenario: Postulante interno

- **DADO** que existe una persona registrada
- **CUANDO** la persona se postula a una vacante
- **ENTONCES** el postulante DEBE poder vincularse con el registro de persona.

#### Escenario: Postulante externo

- **DADO** que un candidato no es una persona registrada
- **CUANDO** el candidato se postula a una vacante
- **ENTONCES** el postulante DEBE persistirse sin requerir un registro de persona.

### Requisito: Proceso de Selección

El sistema DEBE persistir estados de postulación, historial de estados, evaluaciones, observaciones y recomendaciones.

#### Escenario: Evaluar postulación

- **DADO** que existe una postulación
- **CUANDO** un evaluador registra una evaluación
- **ENTONCES** la evaluación DEBE almacenar puntajes, observaciones, recomendación, evaluador y fecha/hora.

### Requisito: Compatibilidad por Habilidades

El sistema DEBE calcular y almacenar la compatibilidad entre las habilidades requeridas por el cargo de una vacante y las habilidades de un postulante interno.

#### Escenario: Calcular compatibilidad

- **DADO** una vacante cuyo cargo tiene habilidades requeridas
- **Y** un postulante interno con habilidades registradas
- **CUANDO** se calcula la compatibilidad
- **ENTONCES** el resultado DEBE comparar niveles requeridos contra niveles de la persona usando ponderaciones de importancia
- **Y** almacenar un snapshot con porcentaje y categoría de compatibilidad.

### Requisito: Auditoría Reutilizable

El sistema DEBE mantener auditoría reutilizable para cambios en entidades principales.

#### Escenario: Auditar modificación

- **DADO** que una entidad principal se modifica
- **CUANDO** se guardan los cambios
- **ENTONCES** un registro de auditoría DEBE almacenar usuario, fecha/hora, entidad, ID de entidad, operación, valores anteriores y valores nuevos.

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

### Requisito: Catálogo `TiposUnidadOrganizativa` con FK `OnDelete(Restrict)`

El sistema DEBE persistir un catálogo inmutable `TiposUnidadOrganizativa` con PK `Id` Guid (`char(36)`), `Codigo varchar(50)` `UNIQUE NOT NULL` y `Nombre varchar(100) NOT NULL`. El catálogo NO DEBE tener columnas `IsActive` ni `IsDeleted`. La columna `UnidadesOrganizativas.TipoUnidadOrganizativaId` DEBE ser una FK `char(36) NOT NULL` con `OnDelete(Restrict)` y DEBE estar indexada.

#### Escenario: Enforcement de la FK

- **DADO** que existe una `UnidadOrganizativa` que referencia el tipo con id `X`
- **CUANDO** se ejecuta `DELETE FROM TiposUnidadOrganizativa WHERE id = X`
- **ENTONCES** MySQL DEBE rechazar la operación con un error de foreign key constraint
- **Y** la fila `X` DEBE permanecer en la tabla.

#### Escenario: Índice sobre la FK

- **DADO** que la migración se ejecutó
- **CUANDO** se consulta `SHOW INDEX FROM UnidadesOrganizativas`
- **ENTONCES** DEBE existir un índice sobre la columna `TipoUnidadOrganizativaId`
- **Y** ese índice DEBE ser el que usa la FK en `REFERENCES`.

#### Escenario: Catálogo sin flags de estado

- **DADO** que existe la tabla `TiposUnidadOrganizativa`
- **CUANDO** se consultan sus columnas con `DESCRIBE TiposUnidadOrganizativa`
- **ENTONCES** NO DEBE existir una columna `IsActive` ni una columna `IsDeleted`.

### Requisito: Migración fail-loud con pre-flight de strings sucios

La migración que introduce la FK `TipoUnidadOrganizativaId` DEBE ejecutar un `SELECT` de pre-flight que liste todo valor distinto de `UnidadesOrganizativas.TipoUnidad` (string) que no se corresponda con un `Codigo` del seed. Si existe al menos un valor ofensivo, la migración DEBE abortar lanzando `InvalidOperationException` con un mensaje que liste los valores ofensivos, **sin** hacer backfill ni `DROP COLUMN`. Si no hay valores ofensivos, el backfill completa, la columna FK queda `NOT NULL` y la columna string se elimina.

#### Escenario: Backfill limpio

- **DADO** que todas las filas existentes en `UnidadesOrganizativas.TipoUnidad` tienen un valor que coincide con un `Codigo` del seed (por ejemplo, todas son `Direccion`, `Area`, `Departamento`, etc.)
- **CUANDO** la migración corre
- **ENTONCES** el backfill de `TipoUnidadOrganizativaId` desde el `Codigo` DEBE completarse
- **Y** la columna `TipoUnidadOrganizativaId` DEBE quedar `NOT NULL`
- **Y** la columna string `TipoUnidad` DEBE eliminarse con `DROP COLUMN`.

#### Escenario: Fail-loud aborta antes del ALTER

- **DADO** que al menos una fila tiene `TipoUnidad = "FooBar"` (un valor que no aparece en el seed de códigos)
- **CUANDO** la migración corre
- **ENTONCES** DEBE lanzar `InvalidOperationException`
- **Y** el mensaje de la excepción DEBE listar el o los valores ofensivos (por ejemplo, `["FooBar"]`)
- **Y** la migración DEBE detenerse **antes** de cualquier `ALTER TABLE` que cambie `TipoUnidadOrganizativaId` a `NOT NULL`
- **Y** la columna `TipoUnidad` (string) DEBE permanecer intacta en la base de datos.

#### Escenario: Seed presente después de la migración

- **DADO** que la migración corrió sobre una base de datos limpia
- **CUANDO** se consulta `SELECT COUNT(*) FROM TiposUnidadOrganizativa`
- **ENTONCES** el resultado DEBE ser 7
- **Y** los 7 códigos (`Institucion`, `Facultad`, `Secretaria`, `Direccion`, `Departamento`, `Division`, `Area`) DEBEN estar presentes.
