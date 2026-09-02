# Refresh tokens single-use y detección de replay

## Por qué un refresh token no se persiste tal cual

El access JWT dura 60 minutos. Cuando expira, el cliente tiene que
conseguir uno nuevo. La forma más simple sería pedir a la API "dame
otro JWT" usando el mismo JWT anterior — pero un JWT firmado es
válido hasta su `exp`, y un atacante que robe el token podría seguir
pidiendo refreshes hasta el cierre de la ventana. La ventana de
exposición sería la ventana de uso.

La forma histórica de resolver esto es persistir un "refresh token"
opaco, largo, separado del JWT, y rotarlo cada vez que se usa. El
servidor mantiene el control sobre qué tokens están vivos. Pero si
ese refresh token también es estático, un atacante que lo roba puede
seguir pidiendo nuevos access JWTs hasta que el dueño del token
también use el legítimo — y entonces la API no puede distinguir
"cliente legítimo" de "atacante". Ambos presentan el mismo string.

La solución que adoptó SGV es **single-use con detección de replay**.
Cada refresh token sirve exactamente una vez. La segunda vez que el
servidor ve el mismo token — sea porque el atacante lo está
reutilizando, sea porque el cliente legítimo lo presentó dos veces por
una doble-pestaña o un retry de red — el servidor interpreta el
segundo uso como señal de compromiso y revoca la familia completa.

## Qué se persiste y qué no

El token plain nunca toca almacenamiento. Sólo viaja en memoria: se
genera con `RandomNumberGenerator.GetBytes(32)` (256 bits de
entropía), se codifica como Base64 URL-safe sin padding y se devuelve
al caller exactamente una vez en `RefreshTokenEmitido.Token`. El
servidor calcula `SHA-256(token)` y persiste el digest hex de 64
caracteres en la columna `TokenHash VARCHAR(64)` de la tabla
`RefreshTokens`.

El resto de la fila es bookkeeping puro:

- `Id` (Guid) — PK. También actúa como `ReplacedById` para encadenar
  generaciones.
- `UserId` (varchar 450) — FK a `AspNetUsers.Id` con `ON DELETE CASCADE`.
- `FamilyId` (Guid) — la "familia" a la que pertenece el token.
- `CreatedAt`, `ExpiresAt`, `LastUsedAt` — timestamps `DATETIME(6)`.
- `RevokedAt` — null mientras el token está vivo; UTC cuando se
  consume o se revoca por familia.
- `ReplacedById` — Guid del siguiente token de la familia. Se llama
  `ReplacedById` y no `ReplacedByTokenId` porque el helper
  `EsCampoSensible` filtra cualquier propiedad cuyo nombre contenga
  `Token`. Si la columna se hubiera llamado `ReplacedByTokenId`, la
  auditoría la hubiera excluido sin querer.

`AuditoriaSaveChangesInterceptor` filtra automáticamente `TokenHash`
por la misma razón, así que el digest nunca aparece en `NewValuesJson`
ni en `OldValuesJson`. La privacidad por construcción es una propiedad
del sistema, no una promesa de desarrollo.

## La familia como unidad de revocación

Cada login emite un `FamilyId` Guid nuevo. El primer refresh token
nace con ese `FamilyId`. La rotación single-use siempre emite el
siguiente token con el mismo `FamilyId`. El resultado es una cadena
de tokens donde cada nodo conoce a su padre vía `ReplacedById` y todos
comparten un origen común.

Cuando el servicio detecta un replay, la acción no es revocar el
token individual: es revocar la familia completa. La fila que disparó
el replay tenía un `RevokedAt` previo (porque ya se consumió); todas
las filas con ese `FamilyId` se marcan con el mismo `RevokedAt` y
ningún dispositivo del usuario puede seguir usando ese camino de
refresh. La familia es, en la práctica, una sesión lógica. Romperla
por replay cierra la sesión para todos los clientes.

## El contrato `RefreshAsync`

El método `RefreshTokenServicio.RefreshAsync` toma el plain token del
caller y devuelve un `RefreshResult` discriminado:

```csharp
public enum RefreshOutcome
{
    Success,
    Invalid,        // token no existe
    Expired,        // existe pero pasó ExpiresAt (no se revoca familia)
    ReplayDetected  // ya estaba consumido/revocado (se revoca familia)
}
```

El corazón de la operación es `IRefreshTokenRepository.TryConsumeAsync`,
un único `ExecuteUpdateAsync` atómico con predicado
`WHERE TokenHash = @h AND RevokedAt IS NULL AND ExpiresAt > @now`. Si
el `UPDATE` afecta una fila, ganamos la carrera y rotamos. Si afecta
cero filas, perdimos o el token nunca existió. La atomicidad vive en
ese `UPDATE` — no hay `SELECT FOR UPDATE`, no hay `BEGIN ... END`.

Cuando `TryConsumeAsync` devuelve `false`, el servicio llama a
`ResolverFalloAsync` para distinguir los tres motivos. La distinción
importa porque la respuesta es distinta:

- **Invalid** — el token nunca existió. La causa más común es una
  cookie borrada por el browser o un token escrito a mano. No hay
  familia que revocar (la fila nunca estuvo ahí).
- **Expired** — la fila existe, no está revocada, pero `ExpiresAt <= now`.
  Por requisito `REQ-AUTH-REFRESH-2`, la familia NO se revoca: la
  expiración natural no es señal de compromiso. El cliente sólo
  necesita volver a loguearse.
- **ReplayDetected** — la fila existe y está revocada (o fue consumida
  por otro caller en paralelo). Toda la familia se marca
  `RevokedAt = nowUtc` y se audita vía `IAuditoriaServicio.RegistrarAsync`
  con `Operacion = "RevocarFamilia"`, `Motivo = "Replay"`.

## Por qué los tres modos colapsan a 401

El controller que sirve el endpoint `/api/v1/auth/refresh` colapsa los
tres `RefreshOutcome` en un único `401 Unauthorized`. La decisión
operativa es deliberada: no filtrar al cliente el motivo del fallo.

Un cliente malicioso que intenta adivinar tokens válidos no debería
pver distinguir "el token que probaste es inválido" de "tu token
expiró pero la familia sigue activa". Distinguirlos le regalaría
información: la cantidad de refresh tokens válidos activos en el
sistema, por ejemplo, o la latencia entre emisión y revocación. El
costo de colapsar es menor: cualquier fallo de refresh hace que el
cliente re-logee, que es exactamente lo que se quiere ante señal de
compromiso y también lo correcto ante un token genuinamente expirado.

La consecuencia operativa es que los tests del controller verifican
"los tres outcomes devuelven 401" sin distinguir más. La
discriminación vive en los logs estructurados del servicio, no en el
wire.

## Privacidad de los logs

Cada outcome emite un log estructurado con nombre de evento, nivel y
campos seguros:

| Evento                   | Nivel        | Campos                                       |
|--------------------------|--------------|----------------------------------------------|
| `RefreshSuccess`         | Information  | `UserId`, `FamilyId`, `NewTokenExpiresAt`    |
| `RefreshFailure`         | Warning      | `Error` (Invalid/Expired), `UserId`, `FamilyId` |
| `RefreshReplayDetected`  | Error        | `UserId`, `FamilyId`, `AffectedFamilySize`   |
| `FamilyRevocation`       | Information  | `UserId`, `RevokedTokensCount`, `FamilyId`   |

Ningún campo incluye el token plain ni su hash. El test
`RefreshTokenServicioLoggingTests.Logs_NeverContainPlainTokenOrHash`
corre la traza completa (success + replay + invalid + expired +
logout) y assertea que ninguno aparece en los mensajes. Esa
propiedad se sostiene sólo mientras ningún logger nuevo agregue
campos con el contenido del token — es una invariante de revisión.

## Trade-offs y consecuencias operativas

El modelo single-use introduce una fricción intencional para el
usuario legítimo: si abre dos pestañas y en ambas intenta refrescar,
una gana y la otra dispara replay. La familia se revoca y ambas
pestañas quedan deslogueadas. El equipo acepta esa fricción porque el
contrapeso (alguien puede presentar el mismo refresh token sin
consecuencias) es mucho peor.

El lifetime absoluto es 14 días (`RefreshTokenOptions.RefreshTokenLifetimeDays`)
sin sliding window. La decisión se documentó en `docs/decisiones-implementacion.md §D-RT-6`:
un sliding window complica debugging y auditoría porque hace que la
"última actividad" mute constantemente sin un evento observable. Si
en el futuro se quiere extender la sesión activa, la solución es un
refresh token nuevo emitido por una acción del usuario (re-login), no
un alargamiento silencioso.

La revocación familiar por logout se ejecuta en
`RefreshTokenServicio.RevokeAsync(userId, plainToken?)`. El token
presentado sólo se usa para enriquecer la auditoría con la familia
que gatilló el logout; la revocación misma afecta a TODAS las
familias activas del usuario. Esto convierte el logout en un sign-out
global: ningún dispositivo sobrevive. Si el usuario no tiene refresh
tokens activos (sesión legacy pre-PR1a), la operación es un no-op
gracioso sin entrada de auditoría.

> ⚠️ A verificar: la descripción anterior asume que el logout desde
> la API (`POST /api/v1/auth/logout`) llama a `RevokeAsync` con el
> token plain del usuario autenticado. El contrato exacto entre el
> controller y el servicio (qué headers o claims viajan) no lo
> inspeccioné en este recorrido.

## Referencias

- `../how-to/02-operar-flujo-recuperacion-contrasena.md` — el ciclo de login + refresh desde el punto de vista del usuario.
- `../reference/05-configuracion-opciones-secretos.md` — opciones de configuración vigentes para `RefreshTokenOptions`.
- `../reference/06-pipeline-middleware-api.md` — el endpoint `/refresh` y su política de rate limiting independiente.
- `openspec/changes/archive/` (búsqueda por "refresh-tokens") — artefactos SDD del change que introdujo el ciclo (PR1a a PR4).
- `docs/decisiones-implementacion.md` — sección "Refresh tokens con rotación single-use y revocación familiar" (decisiones D-RT-1 a D-RT-10).