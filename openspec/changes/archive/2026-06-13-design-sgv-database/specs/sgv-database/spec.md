# Especificación de Base de Datos SGV

## Requisitos AGREGADOS

### Requisito: Jerarquía de Unidades Organizativas

El sistema DEBE persistir unidades organizativas en una jerarquía padre-hijo que permita representar un árbol organizacional completo.

#### Escenario: Crear unidad hija

- **DADO** que existe una unidad organizativa
- **CUANDO** se crea una nueva unidad con esa unidad como padre
- **ENTONCES** la nueva unidad DEBE referenciar a la unidad padre
- **Y** debe ser posible recorrer el árbol desde el padre hacia sus hijos.

#### Escenario: Evitar padre propio

- **CUANDO** una unidad se guarda con ella misma como padre
- **ENTONCES** la base de datos o la aplicación DEBE rechazar el cambio.

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

### Requisito: Compatibilidad con SQL Server y EF Core

El diseño de base de datos DEBE apuntar a SQL Server y Entity Framework Core sobre .NET 9.

#### Escenario: Identificadores GUID

- **CUANDO** se crean entidades de aplicación
- **ENTONCES** sus claves primarias DEBEN usar identificadores GUID en lugar de identificadores enteros.
