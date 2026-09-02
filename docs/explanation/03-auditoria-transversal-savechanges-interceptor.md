# Auditoría transversal con `SaveChangesInterceptor`

## Por qué auditar al nivel del ORM

SGV necesita responder "quién modificó la fila X y cuándo" para casi
todas las entidades de negocio. La tentación es instrumentar cada
servicio de comandos para que escriba un registro de auditoría. El
problema con ese enfoque es que queda a merced de la disciplina del
equipo: cualquier servicio nuevo que toque una entidad tiene que
recordar llamar al auditor, y un servicio existente que mute por una
ruta no instrumentada deja la tabla `Auditorias` incompleta sin que
nadie se entere.

EF Core ofrece un punto único donde toda mutación pasa, sin importar
qué servicio la originó: los `SaveChangesInterceptor`. La
implementación actual vive en
`src/SGV.Infraestructura/Persistencia/AuditoriaSaveChangesInterceptor.cs`
y hereda de `SaveChangesInterceptor`. Sobre `SavingChanges` y
`SavingChangesAsync` se hace un `ChangeTracker.Entries()` y por cada
entrada cuyo estado es `Added`, `Modified` o `Deleted` se genera una
fila de auditoría antes de que EF persista el cambio.

Esta elección es lo que permite afirmar "toda mutación que pasa por el
`DbContext` queda registrada". La afirmación deja de ser cierta sólo
si alguien escribe raw SQL (`ExecuteSqlRaw`), usa `ExecuteUpdateAsync`
o monta una ruta paralela contra otra conexión — todas situaciones
que el código evita por convención y que están documentadas.

## El ciclo del interceptor

El método privado `AgregarAuditorias` recorre las entradas del change
tracker que no son `AuditoriaEntity` (una fila de auditoría no debe
generar otra fila de auditoría — eso sería recursión infinita), agrupa
las que están en estado de mutación y aplica dos transformaciones.

`AplicarAuditoriaTecnica` se encarga del audit técnico: para entidades
que heredan de `AuditableEntityBase`, escribe `CreatedAt` /
`CreatedByUserId` en altas, `UpdatedAt` / `UpdatedByUserId` en
modificaciones, y en soft-deletes cambia el estado a `Modified` y
setea `DeletedAt` / `DeletedByUserId`. Esta es la pieza que mantiene
los campos `CreatedAt`, `UpdatedAt` e `IsDeleted` vivos sin que cada
servicio lo recuerde.

`CrearAuditoria` luego construye la fila `AuditoriaEntity`. El
`EntityName` se deriva del nombre CLR de la entidad eliminando el
sufijo `Entity` (de modo que `CargoEntity` se loguea como `Cargo`); el
`Operation` se mapea a partir del estado (`Alta`, `Modificacion`,
`BajaLogica` o `Desconocida`); los valores se serializan vía
`System.Text.Json` con la opción `Web` (camelCase) para que el JSON
almacenado sea legible. La fila se agrega al `DbContext` antes del
`SaveChangesAsync` final, así EF la persiste junto con la mutación
original en la misma transacción. Si la transacción rollbackea, la
auditoría también rollbackea — no quedan registros huérfanos.

## El correlationId como hilo conductor

Cada request HTTP tiene un `correlationId` que el middleware de la API
inyecta en el `IUsuarioActual` (que es el port por el cual el
interceptor conoce al usuario actual). El mismo `IUsuarioActual`
expone `CorrelationId` y el interceptor lo graba en cada fila de
auditoría que produce. El resultado: cuando un operador mira la tabla
`Auditorias` y filtra por `correlationId`, ve exactamente el conjunto de
mutaciones que un único request HTTP gatilló, en orden cronológico.

Este es el equivalente funcional de un trace ID de OpenTelemetry pero
persistido en la base, donde sobrevive al fin del request. Si el
sistema sumara tracing distribuido mañana, el `correlationId` se
mantiene como ground truth porque vive en filas, no en memoria de un
agente.

## Lo que queda fuera

El interceptor no audita operaciones que no pasan por el change
tracker. Hay tres categorías que el equipo debe tener presentes:

- **Raw SQL con `ExecuteSqlRaw` o `ExecuteSqlInterpolated`.** El
  comando se ejecuta contra MySQL pero EF no ve las filas mutadas, así
  que no se genera auditoría. La convención del repo es no usar estos
  métodos para mutaciones de negocio. Las migraciones EF los usan para
  crear tablas, índices y triggers — esa categoría está explícitamente
  fuera del scope de auditoría porque no son mutaciones de datos de
  negocio.
- **`ExecuteUpdateAsync` y `ExecuteDeleteAsync`** (issue #238 y
  refresh tokens). Estas operaciones bypasean el change tracker por
  diseño. La defensa en este caso es que cada servicio que las usa
  debe llamar explícitamente a `IAuditoriaServicio.RegistrarAsync`
  después. El `RefreshTokenServicio` lo hace por cada revocación de
  familia (D-RT-2 / R5 del design).
- **`IdentityDbContext` de ASP.NET Core Identity.** Las tablas
  `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, etc. usan un
  contexto distinto al `SgvDbContext`. Los bloqueos de cuenta y
  resets de contraseña que pasan por `UserManager<T>` no producen filas
  en `Auditorias` por construcción. La trazabilidad de esos eventos
  vive en `SecurityStamp` y en el audit de Identity (que el repo no
  activa — ver issue #191 / auditoría limitada).

## Cómo se consume desde el controller

La capa de lectura es independiente del interceptor. `IAuditoriaServicioConsulta`
(en Aplicación) es implementada por `AuditoriaServicioConsulta` en
Infraestructura, que consume la tabla `Auditorias` con `AsNoTracking()`.
La proyección al wire type `AuditoriaDto` es **explícita y campo-a-campo**
en el `IQueryable` (decisión D-2 del change `implementa-modulo-auditorias`):

```csharp
.Select(a => new AuditoriaDto(
    a.Id, a.EntityName, a.EntityId, a.Operation, a.OccurredAt,
    a.UserId, a.ChangedPropertiesJson, a.CorrelationId))
```

`OldValuesJson` y `NewValuesJson` nunca se proyectan al wire. Un test
estructural (`AuditoriaDto_NoExponeOldValuesJsonNiNewValuesJson`) verifica
por reflexión que esos campos no existen en el record; otros dos tests
verifican que ni siquiera un JSON HTTP completo contiene
`oldValuesJson` / `newValuesJson`. La defensa contra un futuro
`AddJsonOptions(...).UseCamelCase()` que filtrara esos nombres ya está
implementada.

El controller `AuditoriasController` se monta admin-only
(`[Authorize(Roles = RolesSgv.Administrador)]`) y expone paginación
con `ORDER BY OccurredAt DESC, Id DESC`. El `Id` como tiebreaker
determinista importa porque dos eventos en el mismo milisegundo
podrían aparecer en orden distinto entre dos corridas de un test.

## Consecuencias operativas

La decisión transversal tiene tres consecuencias que el equipo debe
aceptar como el costo del modelo.

**Los campos sensibles nunca llegan a la auditoría.** El helper
`EsCampoSensible` filtra cualquier propiedad cuyo nombre contenga
`Password`, `Token`, `SecurityStamp` o `ConcurrencyStamp`. Esto
explica por qué `RefreshTokenEntity.TokenHash` no aparece en
`NewValuesJson` (decisión D-RT-2) — el nombre dispara el filtro. Si
en el futuro alguien renombra `TokenHash` a `HashInterno`, el filtro
deja de proteger y la fuga pasa inadvertida. La disciplina de nombres
es, paradójicamente, parte del control de seguridad.

**El interceptor introduce una latencia fija por mutación.** Cada
`SaveChangesAsync` paga el costo de serializar todas las propiedades
de cada entidad modificada. Para una mutación batch con cientos de
filas, la auditoría es O(n) sobre el total. En la práctica el cuello
de botella suele estar en MySQL, no en la serialización JSON, pero un
cambio de tráfico debe mirar también este componente.

**Una mutación parcial no queda registrada como un solo evento.**
Si un `SaveChangesAsync` falla a la mitad, las filas que ya estaban
agregadas al change tracker para auditoría no se persisten (la
transacción rollbackea). El operador verá la transacción fallida en
los logs pero no en `Auditorias`. Para reconstruir qué intentó hacer
el request hay que mirar logs de aplicación o el cuerpo del request.
Esta es una característica esperada del modelo transaccional, no un
bug, pero un equipo acostumbrado a audit externo por log puede
encontrarlo contraintuitivo.

## Referencias

- `../how-to/08-auditar-quien-modifico-entidad.md` — cómo un operador navega la tabla `Auditorias` cuando necesita reconstruir quién cambió una fila.
- `../reference/02-esquema-base-de-datos.md` — esquema completo de la tabla `Auditorias` y sus índices.
- `../reference/10-taxonomia-errores.md` — cómo la auditoría encaja con la taxonomía de errores vigente.
- `docs/decisiones-implementacion.md` — sección "Módulo transversal de Auditoría — capa de lectura" (decisiones D-1 a D-8).
- `openspec/specs/auditoria-query/` y `openspec/specs/auditoria-detalle/` — los specs Given/When/Then que definen el contrato observable del módulo.