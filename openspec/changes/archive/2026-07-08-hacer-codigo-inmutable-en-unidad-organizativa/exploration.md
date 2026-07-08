# Exploración: hacer código inmutable en Unidad Organizativa

## Estado actual

### La entidad de dominio

`src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` define:

```csharp
public sealed class UnidadOrganizativa : EntidadAuditable
{
    private readonly List<UnidadOrganizativa> _unidadesHijas = [];
    private readonly List<Puesto> _puestos = [];

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public Guid TipoUnidadOrganizativaId { get; private set; }
    public string? Descripcion { get; private set; }
    public DateOnly? VigenteDesde { get; private set; }
    public DateOnly? VigenteHasta { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? UnidadPadreId { get; private set; }
    public UnidadOrganizativa? UnidadPadre { get; private set; }
    public TipoUnidadOrganizativa? TipoUnidadOrganizativa { get; private set; }

    public IReadOnlyCollection<UnidadOrganizativa> UnidadesHijas => _unidadesHijas;
    public IReadOnlyCollection<Puesto> Puestos => _puestos;

    public void CambiarDatos(string codigo, string nombre, Guid tipoUnidadOrganizativaId, string? descripcion = null) { ... }
    public void CambiarUnidadPadre(Guid? unidadPadreId) { ... }
    public void DefinirVigencia(DateOnly? desde, DateOnly? hasta) { ... }
    public void Desactivar() => IsActive = false;
    public void Activar() => IsActive = true;
}
```

La entidad es una **clase sellada con setters `private` y métodos mutadores explícitos**. NO es inmutable:

- Las propiedades tienen `private set`; desde fuera del agregado nadie puede asignarlas.
- La mutación se concentra en métodos con nombre (`CambiarDatos`, `CambiarUnidadPadre`, `DefinirVigencia`, `Activar`, `Desactivar`).
- Las colecciones hijas (`_unidadesHijas`, `_puestos`) son `List<T>` mutable expuesto solo como `IReadOnlyCollection<T>`.
- La base `EntidadAuditable` (`CreatedAt`, `IsDeleted`, etc.) tiene `public set`, y desde `PersistenceToDomainMapper` se inyecta por reflexión con `BindingFlags.NonPublic`.

Patrón equivalente en `Cargo.cs` y `Puesto.cs`: misma encapsulación rica. El repo ya trabaja con un **modelo de dominio rico + mutación encapsulada**, NO con C#-records inmutables.

### Persistencia

- Entidad EF Core: `src/SGV.Infraestructura/Persistencia/Entidades/UnidadOrganizativaEntity.cs` (setters `public`, sin encapsulación).
- Mapeo EF: `UnidadOrganizativaConfiguracion.cs` configura tabla `UnidadesOrganizativas`, columna computada `ActiveCodigoUnique` para unicidad activa y los índices `IX_UnidadesOrganizativas_ActivoPadre`, `IX_UnidadesOrganizativas_ActivoTipo`, `IX_UnidadesOrganizativas_ActivoCodigo`.
- Mappers explícitos: `DomainToPersistenceMapper.ToEntity` / `UpdateEntity` y `PersistenceToDomainMapper.ToDomain` (este último usa reflexión `SetProperty` para asignar `IsActive`, `UnidadPadre`, etc. por `BindingFlags.NonPublic`).
- Migraciones: `InicialSgvo` (20260614183103), `CambiarTipoUnidadATablaTipoUnidadOrganizativa` (20260616190624). No hay columna `version`/`rowversion`.

### Servicios de aplicación y comandos

`UnidadOrganizativaServicioComandos` expone `Crear`, `Actualizar`, `CambiarUnidadPadre`, `Eliminar`, `Reactivar`. `Actualizar` reusa la misma instancia de dominio y aplica mutaciones in-place:

```csharp
unidad.CambiarDatos(request.Codigo, request.Nombre, request.TipoUnidadOrganizativaId, request.Descripcion);
unidad.DefinirVigencia(request.VigenteDesde, request.VigenteHasta);
await repository.UpdateAsync(unidad, cancellationToken);
```

`Reactivar` y `Eliminar` (soft-delete) tocan flags (`IsActive`, `IsDeleted`) en la entidad de persistencia directamente desde el repositorio (`UnidadOrganizativaRepository.ReactivateAsync` / `DeleteAsync`).

### Superficie HTTP

`UnidadesOrganizativasController` expone `POST`, `PUT`, `PATCH /unidad-padre`, `PATCH /reactivar`, `DELETE`, `GET`, `GET /consulta`, `GET /arbol`. El contrato externo es DTO (`UnidadOrganizativaDto`); la entidad nunca sale del backend.

### Web (Razor Pages)

`src/SGV.Web/Pages/Organizacion/UnidadesOrganizativas/Edit.cshtml.cs` consume `IUnidadOrganizativaApiClient` (typed HttpClient con `ApiBearerTokenHandler`). El PageModel recibe DTOs, los bindea a `UnidadOrganizativaInputModel` y dispara `UpdateAsync`/`ChangeParentAsync`/`ReactivateAsync` con PRG.

### Estado del repositorio (tests)

Cobertura actual del módulo:

- `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` (5 facts sobre mutación por constructor y `CambiarDatos`).
- `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` (909 líneas, ~30 tests cubriendo CRUD + reactivación + conflictos + reglas de jerarquía).
- `tests/SGV.Tests/Aplicacion/Organizacion/CrearUnidadOrganizativaRequestValidatorTests.cs`, `ActualizarUnidadOrganizativaRequestValidatorTests.cs`.
- `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioConsultaTests.cs`.
- `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs` y `UnidadOrganizativaEntityModificationTests.cs`.
- `tests/SGV.Tests/Web/UnidadOrganizativaWebTests.cs`.

Bug conocido abierto (issue #59): `ActivePuestoIdUnique INT` en migración inicial incompatible con `PuestoId CHAR(36)`. **No se debe tocar en este cambio**.

## Qué significa "inmutable" en este proyecto

Hoy el repo trabaja con **mutación encapsulada rica**: setters `private` + métodos de dominio explícitos. Esto NO es inmutabilidad en el sentido de C# (`record class` + `init`/`required`) ni en el sentido de DB (append-only / event sourcing).

Posibles interpretaciones del pedido, ordenadas por probabilidad:

1. **Inmutabilidad a nivel C# (records + init-only)**: convertir `UnidadOrganizativa` en `record class` con `init` setters. Las mutaciones pasan a ser expresiones `with` que producen nuevas instancias; `CambiarDatos` se reemplaza por `unidad.With(codigo, nombre, ...)`. El repositorio deja de hacer `SetProperty` y compara valores antes de generar UPDATE.
2. **Superficie de dominio read-only + comandos para cambios**: mantener la entidad como clase, pero introducir una proyección `IReadOnlyUnidadOrganizativa` (solo getters) que es lo único que se expone fuera del agregado. Los casos de uso siguen siendo los únicos caminos de mutación. Documentar contractualmente que ningún caller puede asignar propiedades.
3. **Read-only a nivel DB + versionado**: agregar columna `version` (auto-increment) a `UnidadesOrganizativas`. En lugar de UPDATE, cada cambio genera una nueva fila con `version+1` y marca la anterior como `superseded`. Restringir `UPDATE` por rol MySQL.
4. **Híbrido B+C**: combinar proyección read-only en código y versionado append-only en DB.

Cualquiera de las cuatro tiene que coexistir con:

- Soft delete + reactivación ya implementados (contratos `DELETE`, `PATCH /reactivar`, listado `status=activas|eliminadas`).
- `CambiarUnidadPadre` (PATCH `/unidad-padre`) que el usuario espera como mutación directa de la unidad, no como una versión nueva.
- Auditoría centralizada vía interceptor EF Core sobre tabla `Auditorias`.
- ReadOnly API con Cargos autenticados y unidades con CRUD; cualquier cambio debe seguir exponiendo los mismos endpoints.

## Áreas afectadas

Para los cuatro enfoques hay un piso común (lectura de DTOs + navegación Razor Pages) que NO cambia; lo que cambia es la capa de Dominio/Aplicación/Infraestructura:

### Enfoque A (records + init-only)

- `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` — reescritura a `record class` con `init` setters; validación en constructor primario o factory `Create`.
- `src/SGV.Dominio/Comun/EntidadAuditable.cs`, `EntidadBase.cs` — mantener `Id` con `init` o cambiar a `required`. `CreatedAt`/`IsDeleted` también deberían migrar a `init` o quedarse fuera de la inmutabilidad (la auditoría necesita escribirlos).
- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` — `Actualizar` ya no muta, devuelve `unidad.With(...)` y persiste la nueva instancia. `Reactivar`/`Eliminar` pasan por constructores estáticos (e.g. `UnidadOrganizativa.Reactivate(this)`).
- `src/SGV.Infraestructura/Persistencia/Mapeos/PersistenceToDomainMapper.cs` — eliminar reflexión `SetProperty`; mapeo por constructor o `with`.
- `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` — `UpdateAsync` debe detectar cambios por comparación y emitir UPDATE; o siempre UPDATE completo.
- `src/SGV.Infraestructura/Persistencia/Configuraciones/UnidadOrganizativaConfiguracion.cs` — verificar compatibilidad con `init` setters en EF Core 9 + Pomelo (soportado pero hay que validar shadow props de auditoría).
- **Migración EF nueva** si cambian shadow props; reutilizar la inicial si no.
- `tests/SGV.Tests/Dominio/Organizacion/UnidadOrganizativaTests.cs` — reescritura total (asserts con `record equality`).
- `tests/SGV.Tests/Aplicacion/Organizacion/UnidadOrganizativaServicioComandosTests.cs` — reescritura.
- `tests/SGV.Tests/Persistencia/UnidadOrganizativaRepositoryTests.cs`, `UnidadOrganizativaEntityModificationTests.cs` — reescritura.

### Enfoque B (read-only surface + comandos)

- `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` — agregar `internal interface IReadOnlyUnidadOrganizativa` (o `UnidadOrganizativaSnapshot`) que expone solo getters; los callers externos (consultas, mappers) tipan contra la interfaz.
- `src/SGV.Aplicacion/Organizacion/Consultas/UnidadOrganizativaServicioConsulta.cs` — cambiar retornos a `IReadOnlyList<IReadOnlyUnidadOrganizativa>` (o `UnidadOrganizativa` sigue, pero documentado).
- `src/SGV.Infraestructura/Persistencia/Mapeos/*` — sin cambios.
- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` — sin cambios (los métodos de mutación siguen vigentes; son el ÚNICO punto de cambio).
- `src/SGV.Aplicacion/Organizacion/Comandos/IUnidadOrganizativaServicioComandos.cs` — agregar comentario XML que documente que esta es la única superficie mutable.
- Tests: no cambian. Se pueden agregar tests verificando que `IReadOnlyUnidadOrganizativa` no expone setters (regresión cheap).

### Enfoque C (DB-level read-only con versionado)

- Nueva migración EF: añadir `Version INT NOT NULL DEFAULT 0` y posiblemente columna `SupersededAt DATETIME NULL` (o tabla histórica `UnidadesOrganizativasHistorico`).
- `src/SGV.Infraestructura/Persistencia/Configuraciones/UnidadOrganizativaConfiguracion.cs` — quitar `IX_UnidadesOrganizativas_Activo*` o reemplazarlos por equivalentes con `Version DESC`.
- `src/SGV.Dominio/Organizacion/UnidadOrganizativa.cs` — agregar `int Version { get; }` (versión lógica), mantener API actual.
- `src/SGV.Aplicacion/Organizacion/Comandos/UnidadOrganizativaServicioComandos.cs` — `Actualizar` clona + inserta nueva fila + marca anterior como superseded.
- `src/SGV.Infraestructura/Persistencia/Repositorios/UnidadOrganizativaRepository.cs` — `UpdateAsync` ahora es `InsertVersionAsync`; lecturas filtran `SupersededAt IS NULL`.
- `src/SGV.Api/Controllers/UnidadesOrganizativasController.cs` — agregar endpoint `GET /api/v1/unidades-organizativas/{id}/versiones` (o devolver historial como recurso aparte).
- Spec: `openspec/specs/unidad-organizativa-crud/spec.md` necesita agregar escenarios para versionado.
- Infra/permisos: script SQL que revoque `UPDATE` a un rol restringido y `BEFORE UPDATE` trigger que registre cambio (opcional). No es exigible por la app; basta con discipline en el código.
- Tests existentes: deben seguir pasando; agregan tests de "insertar versión nueva" y "superseded no aparece en consultas activas".

### Enfoque D (híbrido B+C)

- Suma de archivos de B y C.
- No aporta valor sobre B+C separados salvo que el equipo quiera una garantía defensiva (read-only en código + auditoría histórica en DB).

## Enfoques comparados

| Enfoque | Descripción | Pros | Contras | Esfuerzo |
|---------|-------------|------|---------|----------|
| **A. records + init-only** | `record class` con `init` setters; mutaciones vía `with` | Garantía de inmutabilidad a nivel C#; idiomático en C# 12+/14; previene asignaciones accidentales desde el código de aplicación | Reescritura completa de tests (≈60+ tests); cambia contrato del dominio para callers; `PersistenceToDomainMapper` debe rehacerse; posible fricción con `EntidadAuditable` que tiene setters públicos | **Alto** |
| **B. read-only surface + comandos** | Mantener la clase rica; introducir `IReadOnlyUnidadOrganizativa` y documentar comandos como única vía de mutación | Mínimo cambio en tests; consistente con patrón actual del repo (Cargo, Puesto ya encapsulan); no toca DB | No es "inmutabilidad" en sentido estricto C#; sigue permitiendo mutación interna a través de los métodos de dominio; es más disciplina contractual que restricción del compilador | **Bajo** |
| **C. DB-level read-only + versionado** | Añadir columna `Version`, cada cambio crea una fila nueva | Historial completo para auditoría; defensa en profundidad; respeta soft delete existente | Doble INSERT por cambio (más write amplification); query plans cambian; rompe la expectativa actual de "actualizo la fila"; migración compleja con datos existentes | **Alto** |
| **D. híbrido B+C** | Combina superficie read-only en código con versionado en DB | Defensa en dos capas; buen ajuste a un dominio organizacional casi-catálogo | Costo duplicado; sin valor proporcional salvo auditoría fuerte; introduce nuevos endpoints (versiones) que pueden no ser pedidos | **Muy alto** |

## Recomendación

**Enfoque B (read-only surface + comandos)** es el camino más consistente con el proyecto:

1. El repo ya trabaja con encapsulación rica + soft delete; introducir `IReadOnlyUnidadOrganizativa` como contrato explícito **suma una restricción contractual sin tocar lo que ya funciona**.
2. Los tests existentes siguen pasando, no rompe `CambiarDatos`, `CambiarUnidadPadre`, `DefinirVigencia` ni la reactivación.
3. Compatible con `strict_tdd: true`: el delta de tests es pequeño y orientado a regresión (verificar que el dominio expone solo getters a través de la proyección).
4. No toca migraciones, no toca el cliente HTTP de SGV.Web, no rompe el contrato HTTP existente.

Sin embargo, el pedido "hacer codigo inmutable" tiene alta probabilidad de referirse a **inmutabilidad C# real** (Enfoque A). Antes de proponer, vale la pena confirmar con el usuario qué interpretación busca.

## Riesgos

- **Costo de reescritura de tests** si se elige A: ≈60 tests distribuídos entre Dominio, Aplicación y Persistencia. Con `strict_tdd: true`, hay que mantenerlos pasando durante toda la transición.
- **Compatibilidad EF Core 9 + Pomelo MySQL con `init` setters** en Enfoque A: hay soporte oficial, pero las shadow props de auditoría (`CreatedAt`, `UpdatedAt`, `IsDeleted`) requieren decidir si migran a `init` (rompe la inyección post-construcción) o quedan con `public set` (inmutabilidad parcial). El interceptor de auditoría necesita escribir en `UpdatedAt` después del SaveChanges.
- **`PersistenceToDomainMapper` usa reflexión con `BindingFlags.NonPublic`** para asignar `IsActive`, `UnidadPadre`, etc. En Enfoque A hay que eliminar la reflexión o cambiar a factory/constructor con esos parámetros, lo que vuelve a la entidad menos "rica".
- **El interceptor de auditoría en `SGV.Infraestructura.Interceptores`** escribe sobre propiedades de `EntidadAuditable` con setters públicos (`CreatedAt = DateTime.UtcNow`). Si esas propiedades migran a `init`, el interceptor debe usar `BindingFlags.NonPublic` o el tipo debe exponer un `internal SetCreatedAt()` para auditoría. Cualquier cambio tiene que coordinarse.
- **Reactivación y soft delete ya son user-visible** y contrato documentado en `openspec/specs/unidad-organizativa-crud/spec.md`. Ningún enfoque debe romper `PATCH /reactivar` ni `DELETE`, ni el contrato de listado segmentado `status=activas|eliminadas`.
- **Issue #59 (bug `ActivePuestoIdUnique`)** sigue abierto. No debe mezclarse con este cambio.
- **Tests de integración MySQL** (12 tests de `OcupacionRepositoryTests`) ya fallan en CI por ese bug. Si este cambio toca migraciones o tablas vecinas, hay riesgo de agravar la situación; cualquier nueva migración debe evitar la tabla `Puestos` o aplicar el fix atómico que se decida por separado.
- **Encadenamiento de PRs**: la web shell y la API exponen la entidad vía DTOs; si el cambio es solo de dominio, el PR puede vivir sin tocar frontend. Si toca la API (enfoque C), el contrato externo cambia y exige PR web sincronizado.

## Listo para propuesta

**No** — el pedido es ambiguo y antes de escribir la propuesta necesito que el usuario aclare el alcance y la interpretación. Preguntas concretas:

1. **Nivel de inmutabilidad**: ¿buscás inmutabilidad **a nivel C#** (records + `init`, Enfoque A), **read-only por contrato** (interfaz/proyección, Enfoque B), **DB-level con versionado** (Enfoque C) o **híbrido** (Enfoque D)?
2. **Soft delete y reactivación**: ¿siguen siendo first-class? Cualquier Enfoque debe preservar `DELETE` y `PATCH /reactivar`. Confirmá que querés preservarlos tal como están hoy.
3. **Métodos de mutación del dominio** (`CambiarDatos`, `CambiarUnidadPadre`, `DefinirVigencia`, `Activar`, `Desactivar`): ¿querés que se **eliminen** y toda mutación pase por constructores / `with` (Enfoque A estricto), o que se **mantengan** como la única vía de mutación documentada (Enfoque B)?
4. **Alcance**: ¿solo `UnidadOrganizativa`, o también `Cargo` y `Puesto`? Ambos comparten el mismo patrón y se podrían unificar en el mismo PR.
5. **Catálogo cerrado vs. versionado histórico**: ¿querés que el sistema recuerde **versiones anteriores** de una unidad organizativa (Enfoque C) o basta con que el código sea inmutable?
