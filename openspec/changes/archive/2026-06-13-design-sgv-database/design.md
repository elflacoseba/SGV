# Diseño de Base de Datos SGV

## 1. Análisis del Dominio

### Entidades Principales

- `UnidadOrganizativa`: unidad jerárquica como facultad, secretaría, dirección, departamento, división o área.
- `Cargo`: definición reutilizable de tipo de puesto, como Director, Profesor o Administrativo.
- `Habilidad`: conocimiento, competencia o skill.
- `Puesto`: posición concreta dentro de una unidad organizativa, basada en un cargo.
- `Persona`: empleado, agente o miembro interno.
- `Ocupacion`: asignación histórica de una persona a un puesto.
- `Vacante`: necesidad de cobertura para un puesto.
- `Postulante`: candidato interno o externo.
- `Postulacion`: postulación de un postulante a una vacante.
- `EvaluacionPostulacion`: evaluación registrada durante el proceso de selección.
- `Auditoria`: trazabilidad reutilizable de cambios.

### Responsabilidades

`UnidadOrganizativa` mantiene el árbol institucional. Para la versión 1.0 alcanza con una lista de adyacencia mediante `UnidadPadreId`; si las consultas jerárquicas crecen, puede incorporarse una tabla de cierre o `hierarchyid`.

`Cargo` define perfiles reutilizables e independientes de los puestos. Un mismo cargo puede usarse en distintas áreas y declarar habilidades requeridas.

`Puesto` representa una posición real. Pertenece a una unidad, usa un cargo y puede tener un puesto superior para la cadena jerárquica.

`Persona` existe independientemente de los puestos. Sus movimientos se registran mediante `Ocupacion`.

`Ocupacion` es la fuente de verdad para saber quién ocupa u ocupó un puesto. La ocupación vigente es la que no tiene fecha de finalización.

`Vacante` modela el proceso abierto para cubrir un puesto. Aunque normalmente nace cuando el puesto está sin ocupante, no conviene derivarla solo de esa condición porque pueden existir procesos anticipados.

`Postulante` separa la candidatura del registro interno de persona. Permite candidatos externos y personas ya registradas.

`Postulacion` y `EvaluacionPostulacion` mantienen estados, observaciones, puntajes y snapshots de compatibilidad.

`Auditoria` registra usuario, fecha/hora, operación, entidad afectada y valores anteriores/nuevos.

### Cardinalidades

- Una unidad organizativa puede tener muchas unidades hijas; cada hija tiene cero o una unidad padre.
- Una unidad organizativa tiene muchos puestos.
- Un cargo puede usarse en muchos puestos.
- Un cargo requiere muchas habilidades mediante `CargoHabilidad`.
- Una persona posee muchas habilidades mediante `PersonaHabilidad`.
- Una habilidad puede estar asociada a muchos cargos y muchas personas.
- Un puesto tiene muchas ocupaciones en el tiempo.
- Una persona tiene muchas ocupaciones en el tiempo.
- Un puesto puede tener muchas vacantes en el tiempo.
- Una vacante tiene muchas postulaciones.
- Un postulante tiene muchas postulaciones.
- Una postulación tiene muchas evaluaciones e historial de estados.

### Reglas de Negocio

- Los IDs de entidades de aplicación deben ser `uniqueidentifier`.
- Las entidades principales usan baja lógica con `IsDeleted`, `DeletedAt` y `DeletedByUserId`.
- Un puesto puede tener como máximo una ocupación activa.
- Una persona debería tener como máximo una ocupación principal activa en versión 1.0. Si luego se permiten asignaciones concurrentes, agregar tipo de ocupación o porcentaje de dedicación.
- Una unidad no puede ser padre de sí misma.
- Un puesto no puede ser superior de sí mismo.
- Los niveles de habilidad usan una escala controlada.
- La importancia de una habilidad requerida usa una ponderación numérica.
- Una vacante siempre referencia un puesto.
- Una postulación debe ser única por vacante y postulante.
- Contratar o seleccionar un postulante debe cerrar la vacante y generar o preparar una ocupación desde la capa de aplicación.

## 2. Modelo Conceptual

El modelo se organiza en tres ejes:

- Estructura organizacional: `UnidadOrganizativa` y `Puesto`.
- Perfil de talento: `Cargo`, `Habilidad`, `CargoHabilidad`, `Persona`, `PersonaHabilidad` y `Postulante`.
- Cobertura de vacantes: `Ocupacion`, `Vacante`, `Postulacion`, `EvaluacionPostulacion` e historiales de estado.

La jerarquía se modela en dos lugares:

- Jerarquía de unidades mediante `UnidadOrganizativa.UnidadPadreId`.
- Cadena de mando mediante `Puesto.PuestoSuperiorId`.

Esta separación es intencional: la unidad indica dónde pertenece un puesto, mientras que el puesto superior indica a quién reporta.

## 3. Modelo Lógico

Todas las tablas principales deben incluir columnas de auditoría técnica: `CreatedAt`, `CreatedByUserId`, `UpdatedAt`, `UpdatedByUserId`, `IsDeleted`, `DeletedAt` y `DeletedByUserId`.

Convenciones recomendadas para SQL Server:

- `uniqueidentifier` para IDs, generados por aplicación o con `newsequentialid()`.
- `datetime2(7)` para fecha/hora.
- `nvarchar` para texto.
- `decimal(5,2)` para porcentajes.
- Restricciones `CHECK` para rangos.
- Índices únicos filtrados para filas activas.

### Identity

Usar ASP.NET Core Identity para autenticación y autorización:

- `AspNetUsers`
- `AspNetRoles`
- `AspNetUserRoles`
- Tablas estándar de claims, logins y tokens.

Las tablas de negocio pueden referenciar usuarios mediante columnas `nvarchar(450)` si Identity conserva IDs string. Si se personaliza Identity con GUID, usar `uniqueidentifier`.

### UnidadesOrganizativas

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UnidadPadreId | uniqueidentifier null | FK a UnidadesOrganizativas.Id |
| Codigo | nvarchar(50) | Único entre unidades activas |
| Nombre | nvarchar(200) | Requerido |
| TipoUnidad | nvarchar(50) | Facultad, Departamento, Área, etc. |
| Descripcion | nvarchar(1000) null | Opcional |
| VigenteDesde | date null | Útil para reorganizaciones |
| VigenteHasta | date null | Útil para reorganizaciones |
| IsActive | bit | Default 1 |

Índices y restricciones:

- `CHECK (UnidadPadreId <> Id)`.
- Índice único filtrado por `Codigo WHERE IsDeleted = 0`.
- Índice por `UnidadPadreId`.
- Índice por `Nombre`.

### Cargos

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| Codigo | nvarchar(50) | Único entre cargos activos |
| Nombre | nvarchar(200) | Requerido |
| Descripcion | nvarchar(1000) null | Opcional |
| Nivel | nvarchar(50) null | Operativo, conducción media, directivo |
| IsActive | bit | Default 1 |

Índices:

- Índice único filtrado por `Codigo WHERE IsDeleted = 0`.
- Índice por `Nombre`.

### Habilidades

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| Codigo | nvarchar(50) | Único entre habilidades activas |
| Nombre | nvarchar(200) | Requerido |
| Descripcion | nvarchar(1000) null | Opcional |
| Categoria | nvarchar(100) null | Técnica, liderazgo, dominio, etc. |
| IsActive | bit | Default 1 |

Índices:

- Índice único filtrado por `Codigo WHERE IsDeleted = 0`.
- Índice por `Categoria`.

### NivelesHabilidad

Catálogo semilla para dominio de habilidades.

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| Codigo | nvarchar(50) | Basico, Intermedio, Avanzado, Experto |
| Nombre | nvarchar(100) | Nombre visible |
| ValorNumerico | tinyint | Valores sugeridos 1 a 4 |
| Orden | int | Orden de UI |

Restricciones:

- `Codigo` único.
- `ValorNumerico` único.
- `CHECK (ValorNumerico BETWEEN 1 AND 4)`.

### CargoHabilidades

Relación entre cargo y habilidades requeridas.

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| CargoId | uniqueidentifier | FK |
| HabilidadId | uniqueidentifier | FK |
| NivelRequeridoId | uniqueidentifier | FK a NivelesHabilidad |
| Ponderacion | decimal(5,2) | Rango sugerido 0.10 a 5.00 |
| EsObligatoria | bit | Default 1 |

Restricciones e índices:

- Único `(CargoId, HabilidadId)`.
- `CHECK (Ponderacion > 0)`.
- Índice por `HabilidadId`.

### Personas

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| Legajo | nvarchar(50) null | Único si existe |
| Nombres | nvarchar(100) | Requerido |
| Apellidos | nvarchar(100) | Requerido |
| Email | nvarchar(320) null | Único si existe |
| TipoDocumento | nvarchar(50) null | Opcional |
| NumeroDocumento | nvarchar(50) null | Opcional |
| Telefono | nvarchar(50) null | Opcional |
| IsActive | bit | Default 1 |

Índices:

- Único filtrado por `Legajo WHERE Legajo IS NOT NULL AND IsDeleted = 0`.
- Único filtrado por `Email WHERE Email IS NOT NULL AND IsDeleted = 0`.
- Opcional único filtrado por `(TipoDocumento, NumeroDocumento)`.
- Índice por `(Apellidos, Nombres)`.

### PersonaHabilidades

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| PersonaId | uniqueidentifier | FK |
| HabilidadId | uniqueidentifier | FK |
| NivelHabilidadId | uniqueidentifier | FK |
| VerificadoAt | datetime2 null | Opcional |
| Fuente | nvarchar(100) null | Declarada, certificada, evaluación |

Restricciones e índices:

- Único `(PersonaId, HabilidadId)`.
- Índice por `HabilidadId`.

### Puestos

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UnidadOrganizativaId | uniqueidentifier | FK |
| CargoId | uniqueidentifier | FK |
| PuestoSuperiorId | uniqueidentifier null | FK a Puestos.Id |
| Codigo | nvarchar(50) | Único entre puestos activos |
| Nombre | nvarchar(200) | Requerido |
| Descripcion | nvarchar(1000) null | Opcional |
| IsActive | bit | Default 1 |

Restricciones e índices:

- `CHECK (PuestoSuperiorId <> Id)`.
- Índice único filtrado por `Codigo WHERE IsDeleted = 0`.
- Índices por `UnidadOrganizativaId`, `CargoId` y `PuestoSuperiorId`.

### Ocupaciones

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| PersonaId | uniqueidentifier | FK |
| PuestoId | uniqueidentifier | FK |
| FechaInicio | date | Requerida |
| FechaFin | date null | Null indica vigente |
| TipoAsignacion | nvarchar(50) | Permanente, interina, temporal |
| Observaciones | nvarchar(1000) null | Opcional |

Restricciones e índices:

- `CHECK (FechaFin IS NULL OR FechaFin >= FechaInicio)`.
- Índice único filtrado por `PuestoId WHERE FechaFin IS NULL AND IsDeleted = 0`.
- Índice único filtrado opcional por `PersonaId WHERE FechaFin IS NULL AND IsDeleted = 0`.
- Índices por `(PuestoId, FechaInicio, FechaFin)` y `(PersonaId, FechaInicio, FechaFin)`.

### EstadosVacante

Catálogo semilla:

- Abierta.
- EnSeleccion.
- Cubierta.
- Cancelada.

Campos: `Id`, `Codigo`, `Nombre`, `Orden`, `EsTerminal`.

### Vacantes

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| PuestoId | uniqueidentifier | FK |
| EstadoVacanteId | uniqueidentifier | FK |
| FechaApertura | datetime2 | Requerida |
| FechaCierre | datetime2 null | Requerida al cubrir o cancelar |
| Motivo | nvarchar(500) | Requerido |
| Observaciones | nvarchar(1000) null | Opcional |

Índices:

- Por `PuestoId`.
- Por `EstadoVacanteId`.
- Por `FechaApertura`.
- Índice filtrado para vacantes activas por `(EstadoVacanteId, FechaApertura)`.

### HistorialEstadosVacante

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| VacanteId | uniqueidentifier | FK |
| EstadoAnteriorId | uniqueidentifier null | FK |
| EstadoNuevoId | uniqueidentifier | FK |
| ChangedAt | datetime2 | Requerido |
| ChangedByUserId | nvarchar(450) null | Usuario Identity |
| Motivo | nvarchar(500) null | Opcional |

Índice por `(VacanteId, ChangedAt)`.

### Postulantes

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| PersonaId | uniqueidentifier null | FK para candidatos internos |
| Nombres | nvarchar(100) null | Requerido si es externo |
| Apellidos | nvarchar(100) null | Requerido si es externo |
| Email | nvarchar(320) null | Opcional |
| Telefono | nvarchar(50) null | Opcional |
| Fuente | nvarchar(100) null | Interno, externo, referido |
| Observaciones | nvarchar(1000) null | Opcional |

Restricciones e índices:

- Único filtrado por `PersonaId WHERE PersonaId IS NOT NULL AND IsDeleted = 0`.
- Índice por `(Apellidos, Nombres)`.
- Índice por `Email`.
- La aplicación valida que exista `PersonaId` o datos mínimos de candidato externo.

### EstadosPostulacion

Catálogo semilla:

- Postulado.
- Preseleccionado.
- Entrevistado.
- Aprobado.
- Rechazado.
- Contratado.

Campos: `Id`, `Codigo`, `Nombre`, `Orden`, `EsTerminal`, `EsTerminalPositivo`.

### Postulaciones

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| VacanteId | uniqueidentifier | FK |
| PostulanteId | uniqueidentifier | FK |
| EstadoPostulacionId | uniqueidentifier | FK |
| FechaPostulacion | datetime2 | Requerida |
| PuntajeCompatibilidad | decimal(5,2) null | Snapshot porcentual |
| NivelCompatibilidad | nvarchar(50) null | Total, parcial, insuficiente |
| Observaciones | nvarchar(1000) null | Opcional |

Restricciones e índices:

- Único `(VacanteId, PostulanteId)`.
- `CHECK (PuntajeCompatibilidad IS NULL OR PuntajeCompatibilidad BETWEEN 0 AND 100)`.
- Índices por `EstadoPostulacionId` y `(VacanteId, EstadoPostulacionId)`.

### HistorialEstadosPostulacion

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| PostulacionId | uniqueidentifier | FK |
| EstadoAnteriorId | uniqueidentifier null | FK |
| EstadoNuevoId | uniqueidentifier | FK |
| ChangedAt | datetime2 | Requerido |
| ChangedByUserId | nvarchar(450) null | Usuario Identity |
| Observaciones | nvarchar(1000) null | Opcional |

Índice por `(PostulacionId, ChangedAt)`.

### EvaluacionesPostulacion

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| PostulacionId | uniqueidentifier | FK |
| EvaluadoAt | datetime2 | Requerido |
| EvaluadoByUserId | nvarchar(450) null | Usuario Identity |
| PuntajeTecnico | decimal(5,2) null | 0 a 100 |
| PuntajeEntrevista | decimal(5,2) null | 0 a 100 |
| PuntajeCompatibilidad | decimal(5,2) null | 0 a 100 |
| Recomendacion | nvarchar(50) null | Recomendada, con reservas, no recomendada |
| Observaciones | nvarchar(2000) null | Notas de negocio |

Índices:

- Por `PostulacionId`.
- Por `EvaluadoAt`.

### Auditorias

| Columna | Tipo | Notas |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UserId | nvarchar(450) null | Usuario Identity |
| OccurredAt | datetime2 | Requerido |
| EntityName | nvarchar(200) | Nombre de entidad o tabla |
| EntityId | nvarchar(100) | GUID como string |
| Operation | nvarchar(50) | Alta, Modificacion, BajaLogica |
| OldValuesJson | nvarchar(max) null | Snapshot JSON |
| NewValuesJson | nvarchar(max) null | Snapshot JSON |
| ChangedPropertiesJson | nvarchar(max) null | Lista de propiedades |
| CorrelationId | uniqueidentifier null | Correlación de request o transacción |

Índices:

- `(EntityName, EntityId, OccurredAt)`.
- `(UserId, OccurredAt)`.
- `CorrelationId`.

## 4. Modelo de Habilidades

### Relación Cargo-Habilidad

`CargoHabilidad` almacena las habilidades requeridas por cada cargo junto con nivel requerido, obligatoriedad y ponderación. Esto evita una relación muchos a muchos débil y permite calcular compatibilidad.

Ponderaciones sugeridas:

- `1.00`: deseable.
- `2.00`: importante.
- `3.00`: crítica.

### Relación Persona-Habilidad

`PersonaHabilidad` registra habilidades y niveles de dominio de personas internas. Para postulantes externos, la versión 1.0 puede usar evaluaciones manuales. Si se necesita compatibilidad estructurada para externos, agregar `PostulanteHabilidad` con la misma forma que `PersonaHabilidad`.

### Cálculo de Compatibilidad

Para un postulante interno:

1. Obtener las habilidades requeridas por el cargo del puesto de la vacante.
2. Obtener las habilidades de la persona vinculada al postulante.
3. Comparar `ValorNumerico` del nivel de la persona contra el nivel requerido.
4. Puntuar cada habilidad:
   - `1.0` si el nivel de la persona es igual o superior.
   - `nivelPersona / nivelRequerido` si es inferior.
   - `0` si no posee la habilidad.
5. Multiplicar cada resultado por `Ponderacion`.
6. Calcular porcentaje: `sumatoria(puntaje ponderado) / sumatoria(ponderaciones) * 100`.

Categorías:

- Total: `>= 90` y todas las habilidades obligatorias cubiertas.
- Parcial: `>= 60` o habilidades obligatorias cubiertas parcialmente.
- Insuficiente: `< 60` o falta una habilidad obligatoria crítica.

Guardar el resultado como snapshot en `Postulaciones` y, si aplica, en `EvaluacionesPostulacion`.

## 5. Modelo de Vacantes y Selección

El estado actual de una vacante vive en `Vacantes.EstadoVacanteId`; las transiciones se registran en `HistorialEstadosVacante`.

El estado actual de una postulación vive en `Postulaciones.EstadoPostulacionId`; las transiciones se registran en `HistorialEstadosPostulacion`.

Las evaluaciones deben ser append-only por defecto. Si se corrige una evaluación, conviene agregar una nueva o auditar la modificación, evitando borrar decisiones históricas.

Transiciones recomendadas:

- Vacante: Abierta -> EnSeleccion -> Cubierta.
- Vacante: Abierta -> Cancelada.
- Vacante: EnSeleccion -> Cancelada.
- Postulación: Postulado -> Preseleccionado -> Entrevistado -> Aprobado -> Contratado.
- Postulación: Postulado/Preseleccionado/Entrevistado/Aprobado -> Rechazado.

La capa de aplicación debe validar transiciones permitidas para mantener simple la base de datos.

## 6. Estrategia de Auditoría

### Recomendación: Tabla Única de Auditoría

Usar una tabla `Auditorias` poblada por un interceptor de EF Core o pipeline de guardado.

Ventajas:

- Una implementación para todas las entidades.
- Trazabilidad transversal.
- Adecuada para versión 1.0.
- Compatible con arquitectura limpia si la captura vive en infraestructura.

Desventajas:

- Los valores JSON son menos cómodos para reportes tipados.
- Alto volumen puede requerir particionamiento o archivado.

### Alternativa: Tablas de Auditoría por Entidad

Ventajas:

- Tipado fuerte.
- Reportes específicos más simples.
- Mejor rendimiento potencial en entidades muy auditadas.

Desventajas:

- Más mantenimiento.
- Mayor riesgo de inconsistencias entre entidades.
- Complejidad innecesaria para versión 1.0.

Recomendación final: comenzar con `Auditorias`, incluir `CorrelationId` y planificar retención, archivado o particionamiento si el volumen lo exige.

## 7. Estrategia de Migraciones

Orden sugerido:

1. Tablas de Identity.
2. Convenciones de auditoría técnica en EF Core.
3. Catálogos: `NivelesHabilidad`, `EstadosVacante`, `EstadosPostulacion`.
4. Estructura: `UnidadesOrganizativas`.
5. Catálogos de negocio: `Cargos`, `Habilidades`.
6. Relaciones: `CargoHabilidades`.
7. Personas: `Personas`, `PersonaHabilidades`.
8. Puestos: `Puestos`.
9. Historial: `Ocupaciones`.
10. Vacantes: `Vacantes`, `HistorialEstadosVacante`.
11. Postulantes: `Postulantes`.
12. Selección: `Postulaciones`, `HistorialEstadosPostulacion`, `EvaluacionesPostulacion`.
13. Auditoría: `Auditorias`.
14. Índices filtrados y restricciones check.
15. Datos semilla.

Preferir migraciones por área funcional en lugar de una única migración gigante.

## 8. Datos Semilla

Unidades organizativas:

- Institución raíz.
- Facultad.
- Secretaría.
- Dirección.
- Departamento.
- División.
- Área.

Cargos:

- Decano.
- Secretario.
- Director.
- Jefe de Departamento.
- Administrativo.
- Profesor.

Habilidades:

- Liderazgo.
- Gestión de Personal.
- SQL Server.
- Entity Framework.
- Programación .NET.
- Administración Pública.
- Docencia Universitaria.

Niveles de habilidad:

- Básico = 1.
- Intermedio = 2.
- Avanzado = 3.
- Experto = 4.

Estados de vacante:

- Abierta.
- En Selección.
- Cubierta.
- Cancelada.

Estados de postulación:

- Postulado.
- Preseleccionado.
- Entrevistado.
- Aprobado.
- Rechazado.
- Contratado.

Roles:

- Administrador.
- RecursosHumanos.
- GestorOrganizacional.
- EvaluadorSeleccion.
- Lector.

## 9. Riesgos y Escalabilidad

### Consultas Jerárquicas

La lista de adyacencia es simple, pero las consultas recursivas pueden volverse costosas. Si el árbol se consulta con mucha frecuencia, agregar tabla de cierre o `hierarchyid`.

### Reorganizaciones

Mover unidades puede afectar reportes históricos. La versión 1.0 debe conservar fechas de vigencia y ocupaciones históricas; una versión posterior puede agregar historial estructural.

### Ocupaciones Concurrentes

Algunas instituciones permiten asignaciones interinas o secundarias. Si se requiere, reemplazar la restricción de una ocupación activa por persona con tipo de ocupación y porcentaje de dedicación.

### Habilidades de Postulantes Externos

La versión 1.0 puede evaluar externos manualmente. Si se requiere cálculo automático, agregar `PostulanteHabilidad`.

### Volumen de Auditoría

La auditoría centralizada crecerá rápido. Planificar retención, archivado y particionamiento cuando existan métricas reales de uso.

### Complejidad de Workflow

Validar transiciones en la aplicación es suficiente para la versión 1.0. Si el flujo debe configurarse dinámicamente, incorporar definiciones de workflow en una versión posterior.

## Recomendación Final

Implementar la versión 1.0 con tablas normalizadas, IDs GUID, catálogos de estado, índices únicos filtrados, auditoría reusable y snapshots de compatibilidad. Mantener jerarquía y workflow simples, pero dejar el modelo preparado para agregar tabla de cierre, workflow configurable, habilidades de externos e historial avanzado sin reemplazar el núcleo de la base de datos.
