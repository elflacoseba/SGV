# Decisiones de Implementación

## SDK y Target Framework

Los proyectos apuntan a `net9.0` para respetar el diseño aprobado. El archivo `global.json` prefiere SDK `9.0.100` y permite roll-forward mayor para que el SDK 10 instalado localmente pueda compilar.

## Identity

Se mantiene `IdentityUser` con clave string, por lo que las columnas de auditoría que referencian usuarios usan `nvarchar(450)`. Esta decisión conserva el comportamiento estándar de ASP.NET Core Identity y evita personalización prematura.

## Ocupaciones Activas

La versión inicial aplica una única ocupación vigente por puesto y una única ocupación vigente por persona mediante índices únicos filtrados. Si el negocio requiere cargos concurrentes, se deberá agregar tipo de ocupación o porcentaje de dedicación.

## Postulantes Externos

Los postulantes externos se registran sin habilidades estructuradas en esta versión. La compatibilidad automática queda enfocada en postulantes internos vinculados a una persona.

## Auditoría

La auditoría se implementa con una tabla única `Auditorias` y un interceptor de EF Core. Se excluyen campos sensibles por nombre para evitar persistir contraseñas, tokens o stamps de seguridad en JSON.
