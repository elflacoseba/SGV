# Diseño de Base de Datos SGV

## Resumen

Definir el diseño de base de datos para el Sistema de Gestión de Vacantes (SGV), cubriendo estructura organizacional, puestos, personas, ocupaciones, vacantes, postulantes, procesos de selección, compatibilidad por habilidades y auditoría reutilizable.

## Motivación

SGV necesita un modelo normalizado para SQL Server que sea lo bastante simple para la versión 1.0 y lo bastante estable para evolucionar hacia un sistema corporativo. El diseño debe soportar Entity Framework Core sobre .NET 9, arquitectura limpia, usuarios y roles con Identity, identificadores GUID, trazabilidad histórica y futuras reorganizaciones sin rediseños importantes.

## Alcance

Este cambio produce solo el plan técnico de base de datos. No crea migraciones, entidades, mapeos de DbContext, controladores, UI, seeders ni servicios de ejecución.

Incluye:

- Análisis del dominio y responsabilidades de entidades.
- Modelo conceptual y relaciones.
- Modelo lógico SQL Server con campos sugeridos, claves, restricciones e índices.
- Modelo de habilidades y estrategia de compatibilidad.
- Modelo de vacantes, postulaciones, evaluaciones e historial.
- Modelo de auditoría reutilizable.
- Orden de migraciones y plan de datos semilla.
- Riesgos, alternativas y recomendaciones de escalabilidad.

Excluido de la versión 1.0:

- Firma digital.
- Motor de workflow complejo.
- Notificaciones.
- Integraciones externas.
- Gestión documental.
- Presupuesto, salarios, capacitación y evaluación de desempeño.

## Principios de Diseño

- Usar GUID como claves primarias de entidades de aplicación.
- Mantener autenticación y autorización con Identity separadas del modelo de negocio, permitiendo referencias de auditoría a usuarios.
- Preferir tablas normalizadas y entidades de unión explícitas cuando una relación muchos a muchos tenga atributos.
- Modelar estado actual para consultas eficientes y preservar historial con filas temporales o historiales de estado.
- Usar baja lógica en entidades principales de negocio.
- Mantener estados y catálogos como datos semilla extensibles.
