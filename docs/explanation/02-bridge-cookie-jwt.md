# Bridge cookie → JWT: por qué SGV.Web tiene un `ApiBearerTokenHandler`

## El problema que la cookie no puede resolver sola

SGV.Web es una shell Razor Pages. Su navegador necesita persistir la
sesión del usuario entre requests, así que autentica vía cookies — el
mecanismo idiomático de ASP.NET Core para HTML. Pero la lógica de
negocio vive en `SGV.Api`, que sólo entiende JWT bearer. Si el shell
reenviara sus requests sin más, la API devolvería `401 Unauthorized` para
todas las llamadas autenticadas: la cookie no es un mecanismo que
`SGV.Api` valide.

La solución obvia — copiar el JWT en `localStorage` y leerlo desde
JavaScript — abre una caja de Pandora. Cualquier script de terceros
cargado por la página (analytics, fuentes CDN, polyfills) tiene acceso
al JWT. Un XSS se convierte en un compromiso de cuenta inmediato, sin
necesidad de robar la cookie porque el token se expone explícitamente
en el DOM. La industria lleva años documentando este patrón como
anti-patrón crítico de seguridad web.

`SGV.Web` toma un camino distinto: el JWT se guarda en la cookie de
sesión (no visible para JavaScript) y un `DelegatingHandler` lo extrae
en cada request saliente hacia la API, lo pega como `Authorization:
Bearer` y lo descarta antes de salir del proceso. El navegador nunca
ve el JWT; el JavaScript nunca ve el JWT; sólo lo ve el HttpClient que
lo reenvía por dentro del host web.

## Cómo se construye el bridge

El JWT llega al shell después de un login exitoso. La respuesta de
`POST /api/v1/auth/login` incluye `accessToken` y `expiresAt` además
del `refreshToken` que vive en una segunda cookie (`sgv.rt`). La
factoría `AuthSessionFactory.CreateProperties` toma ese par y los
empaqueta en el ticket de autenticación cookie usando
`AuthenticationTokenExtensions.StoreTokens`:

```csharp
properties.StoreTokens(new[]
{
    new AuthenticationToken { Name = AuthTokenNames.AccessToken, Value = response.AccessToken },
    new AuthenticationToken { Name = AuthTokenNames.ExpiresAt, Value = response.ExpiresAt.ToString("O") }
});
```

Los tokens quedan serializados dentro de la cookie `sgv.auth` —
`HttpOnly=true`, `SameSite=Lax`, `SecurePolicy=Always` fuera de
Development. ASP.NET Core los encripta con Data Protection antes de
escribir la cookie.

El `ApiBearerTokenHandler` se enchufa en cada `HttpClient` que la Web
construye para hablar con la API. Su `SendAsync` resuelve el
`HttpContext` actual, lee el `access_token` del ticket cookie con
`HttpContext.GetTokenAsync(AuthTokenNames.AccessToken)`, y arma el
header `Authorization: Bearer ...` sobre el `HttpRequestMessage`
saliente. Si el handler no encuentra contexto (background jobs), si ya
hay un `Authorization` configurado por el caller, o si el ticket no
tiene token, deja pasar el request sin tocarlo.

## Atributos de la cookie y por qué cada uno importa

La matriz de seguridad de la cookie vive en `SGV.Web/Program.cs`. Las
tres dimensiones que valen la pena entender son:

**HttpOnly = true**. El flag hace que `document.cookie` no pueda leerla.
Aunque un XSS inyecte un `<script>` malicioso, ese script no puede
extraer la cookie ni, por extensión, el JWT. Es la única razón por la
que tiene sentido alojar el JWT en la cookie y no en `localStorage`.

**SameSite = Lax**. La cookie no se envía en cross-site sub-requests
(embeds, iframes, fetch cross-origin). Sí se envía en navegación
top-level (click en un link). Para una shell monolítica que sólo llama
a su propia API por mismo origen, esto neutraliza CSRF sin necesidad
de tokens anti-forgery adicionales. La excepción son los POST
cross-origin — y la API no los admite sin CORS explícito.

**SecurePolicy = Always (fuera de Development)**. La cookie sólo viaja
sobre HTTPS en ambientes no-Development. En Development usa
`SameAsRequest` para permitir que `http://localhost:5266` funcione sin
TLS. La nota operativa está registrada en `docs/decisiones-implementacion.md
sección "Hardening runtime: cookie y CORS por ambiente"`: la diferencia
entre Development y otros ambientes es la diferencia entre un riesgo
local acotado y un riesgo de exfiltración cross-origin.

## Qué pasa cuando el JWT expira

El access JWT dura 60 minutos. Cuando expira, el siguiente request
del shell hacia la API devuelve `401 TokenExpirado`. Aquí es donde la
composición se vuelve interesante, porque el bridge no sabe rotar
tokens por sí mismo. La responsabilidad se reparte entre tres piezas.

`AuthSessionFactory.CreateProperties` deja el `AllowRefresh = false`
del ticket cookie. ASP.NET Core interpreta ese flag como "no
intentes reemitir el ticket cookie automáticamente". La rotación del
JWT no ocurre dentro del ticket de `sgv.auth`: ocurre a través de la
cookie separada `sgv.rt`, que carga el refresh token.

`CookiePrincipalRevalidator` se enchufa en el evento `ValidateAsync` del
middleware de cookies. Antes de aceptar cada request entrante, hace un
ping autenticado a `GET /api/v1/usuarios/{id}` con el JWT actual. Si
la API responde `401/403/404`, el principal se rechaza y la sesión
cookie se invalida — sign-out local. Si responde `200`, el cookie se
mantiene.

Para los `401` que se filtran (token expirado pero cookie todavía
válida por revalidación exitosa anterior), el cliente HTTP de la Web
típicamente recibe el código, lo mapea via `ToCommandResultAsync` y
propaga el error al usuario con un redirect a `/Auth/SignIn`. La
rotación transparente de refresh tokens es responsabilidad del
controller de autenticación de la Web, no del handler de bridge.

## Consecuencias operativas

El bridge introduce una asimetría importante: el navegador nunca toca
el JWT pero el host web lo lee constantemente. Esto tiene tres
implicaciones prácticas.

**Logs y traces del lado Web no deben incluir el token.** El
`ApiBearerTokenHandler` loguea `Authenticated request to {Path} has no
{TokenName}` cuando el ticket viene sin access token — útil para
diagnóstico — pero nunca loguea el valor del token. Esa disciplina se
extiende a todo código de Web: cualquier `ILogger.LogInformation(...,
accessToken)` rompe el modelo de amenaza.

**Si el JWT y el cookie divergen (por ejemplo, el reloj del host de la
API drift respecto al de la Web), el bridge puede enviar un token que
la API rechaza.** El clock skew está fijado en 30 segundos en
`JwtTokenValidationParameters.Create` para tolerar drift menor de NTP.
Más allá de eso, la sesión se rompe y el usuario re-loguea. La
recomendación operativa (registrada en la decisión sobre
`ClockSkew`) es revisar NTP antes que revisar claves.

**El bridge sólo sirve para HttpClient que el shell configura
explícitamente.** Si un futuro servicio crea un `new HttpClient()` sin
inyectar el handler, ese cliente viajará sin bearer y la API lo
rechazará. La defensa actual es por convención: el patrón canónico
vive en `IHttpClientFactory` registrado en `Program.cs`, y cualquier
cliente que se desvíe debe justificarse en code review.

## Referencias

- `../how-to/02-operar-flujo-recuperacion-contrasena.md` — cómo un humano opera el ciclo cookie → JWT → cookie desde el otro extremo.
- `../how-to/03-rotar-jwt-signing-key.md` — por qué rotar la clave invalida todas las sesiones activas de ambos hosts.
- `../reference/05-configuracion-opciones-secretos.md` — la matriz de secretos JWT y la regla fail-loud.
- `../reference/07-pipeline-arranque-web.md` — orden concreto de middlewares en `SGV.Web`, incluido el bridge.
- `docs/decisiones-implementacion.md` — secciones "Hardening defense-in-depth" y "Hardening runtime: cookie y CORS por ambiente".