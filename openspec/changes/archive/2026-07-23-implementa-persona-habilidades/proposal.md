# Propuesta: Implementa Persona-Habilidades

## Resumen ejecutivo

Completar la gestión web de habilidades por persona, disponible solo en backend. Agrega Razor Page, cliente HTTP tipado y contratos compartidos sin alterar persistencia ni el contrato HTTP. `PersonaSkill*` se migra a `SGV.Contracts`; `VerificadoAt` y `Fuente` se difieren provisionalmente.

## Contexto y motivación

El backend permite consultar y mutar asociaciones, pero Web no expone el flujo. El cambio habilita gestión coherente con `CargoHabilidades`.

## Alcance

- Crear `/personas/{id:guid}/habilidades` en `Pages/Personas/`: listar, agregar, modificar y quitar por PRG; enlazar desde Details.
- Extender `IPersonaApiClient`/`PersonaApiClient` con `GetSkillsAsync`, `UpsertSkillAsync` y `DeleteSkillAsync`.
- Mover, sin duplicar, `PersonaSkill*` a `SGV.Contracts.Personas.*`, preservando JSON.
- Aplicar strict TDD con xUnit significativo.

## Fuera de alcance

- Cambios de dominio, endpoints, BD, migraciones o catálogos/GUID.
- Edición de `VerificadoAt`/`Fuente`, pendiente de confirmación.
- `Ponderacion`, `EsObligatoria` y `NivelRequeridoId`, exclusivos de Cargo.
- Separar Contracts como prerrequisito: integra este mismo cambio.

## Capacidades

### Nuevas
- `persona-skill-web-management`: gestión Razor del vínculo Persona-Habilidad.

### Modificadas
- `persona-management`: navegación al subrecurso, sin contaminar el payload padre.
- `commandresult-error-taxonomy`: sumar PersonaSkill a `ErrorCategoria`, sujeto a confirmación.

## Enfoque y áreas afectadas

Replicar grilla, handlers y feedback de `CargoHabilidades`, usando siempre `NivelHabilidadId`. Resolver primero el gap CRÍTICO de contratos, sin dependencia Web→Aplicación.

| Área | Impacto |
|---|---|
| `src/SGV.Contracts/Personas/` | Wire-types |
| `src/SGV.Web/{Integration/Personas,Pages/Personas}/` | Cliente y UI |
| `tests/SGV.Tests/Web/Persona/` | Fakes y pruebas |

## Criterios de aceptación

- La Razor Page lista asociaciones y ejecuta alta, modificación y baja con feedback PRG.
- El cliente tipado cubre esos casos mediante los tres métodos definidos.
- Web consume `PersonaSkill*` solo desde Contracts, sin cambiar el wire format.
- Formularios, payloads y tests usan `NivelHabilidadId`, nunca `NivelRequeridoId`.
- xUnit protege autorización, validación, mutaciones, errores recuperables y anti-drift; omite trivialidades.

## Riesgos y supuestos

| Riesgo | Impacto | Mitigación | Owner |
|---|---|---|---|
| DTOs fuera de Contracts | CRITICAL | Movimiento atómico + contratos | Implementación |
| Drift del nivel de Cargo | HIGH | Naming + test anti-drift | QA |
| Verificación ambigua | MEDIUM | Confirmar antes de spec | Producto |
| Más de 400 líneas | MEDIUM | Confirmar slices antes de apply | Orquestador |

## Dependencias y rollback

Depende de endpoints y catálogos vigentes. Rollback: revertir página, cliente y usings; restaurar DTOs en Aplicación. Sin datos ni migraciones afectados.

## Decisiones a confirmar con el usuario

1. `VerificadoAt`/`Fuente`: diferir (menor alcance), solo lectura (trazabilidad) o editar (flujo completo).
2. Acceso: todo admin-only (paridad Cargo) o lectura autenticada + escritura admin (mayor visibilidad).
3. Persona inactiva: bloquear (regla simple) o historial solo lectura (auditoría).
4. Errores: `ErrorCategoria` (alineación vigente) o enum legacy temporal (menor cambio).
