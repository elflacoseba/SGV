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

### Requisito: Cargos Reutilizables

El sistema DEBE mantener cargos como tipos de puesto reutilizables e independientes de los puestos concretos.

#### Escenario: Reutilizar cargo en varios puestos

- **DADO** un cargo llamado Director
- **CUANDO** varios puestos concretos requieren ese cargo
- **ENTONCES** cada puesto DEBE referenciar el mismo cargo.

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

El sistema DEBE persistir puestos concretos pertenecientes a unidades organizativas y basados en cargos.

#### Escenario: Puesto sin ocupante

- **DADO** que existe un puesto
- **CUANDO** no tiene ocupación activa
- **ENTONCES** el puesto DEBE seguir siendo válido y disponible para gestionar vacantes.

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
