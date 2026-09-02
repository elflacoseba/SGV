# Rate limiting por IP vs por subject

## La decisión de partition key

Cada endpoint protegido por rate limiting necesita una llave para
agrupar las requests. Esa llave es la `partition key` de ASP.NET
Core, y la elección entre dos dimensiones — la IP del cliente o el
identificador del sujeto autenticado — define qué atacantes quedan
excluidos por la cuota.

Las políticas vigentes en `SGV.Api/Program.cs` se reparten así:

| Política                  | Partition key | Default           |
|---------------------------|---------------|-------------------|
| `ForgotPassword`          | IP            | 3 / 15 min        |
| `ResetPassword`           | IP            | 5 / 15 min        |
| `Setup` (initial admin)   | IP            | 5 / 15 min        |
| `ChangePassword`          | Subject (fallback IP) | 5 / 15 min |
| `Refresh`                 | IP            | configurable      |

`ForgotPassword`, `ResetPassword` y `Setup` son anónimos: la
solicitud llega antes de que haya un principal resuelto. La única
manera de agrupar es la IP del socket. `Refresh` también es
anónimo en el sentido de que el principal del cookie puede no estar
presente en el momento de la request: la cuota se mide por IP.

`ChangePassword` sí está autenticado. Si la API todavía no resolvió
el principal (escenario anómalo porque `[Authorize]` corre antes), la
función `PartitionKeyBySubjectOrIp` cae a la IP para no abrir una
llave global sin control.

## Por qué `ChangePassword` se agrupa por subject y `Refresh` por IP

`ChangePassword` está pensado para evitar que un atacante con la
cookie de un usuario bombardee el endpoint hasta encontrar una nueva
contraseña. Como la cookie identifica al sujeto, agrupar por
`Identity.Name` significa que la cuota es "5 cambios cada 15 minutos
para este usuario" — sin importar desde qué IP se hagan. Esto
protege contraCredential stuffing distribuido en una botnet: si 50
IPs distintas prueban con la misma cookie, comparten la cuota.

`Refresh` está pensado para evitar fuerza bruta sobre el endpoint
de rotación de tokens. Como el refresh token mismo viaja en una
cookie HttpOnly separada (`sgv.rt`), un atacante que no tiene la
cookie de auth todavía pero tiene la de refresh (por ejemplo, leak
de logs o robo local) igual tiene que pasar por el endpoint. La
cuota por IP es la defensa contra esto: si muchas IPs distintas
presentan refresh tokens a una API concreta, no pueden coordinarse
para agotar la cuota de un usuario legítimo.

La asimetría se sostiene porque cada política protege contra una
amenaza distinta. `ChangePassword` está expuesto a usuarios
autenticados, no a anónimos: tiene que agrupar por lo que el usuario
es. `Refresh` está expuesto a clientes que pueden estar autenticados
o no (la cookie puede haber expirado pero el refresh sigue vivo):
agrupar por IP es lo único defendible.

## El header `Retry-After`

Cuando una cuota se agota, el middleware emite `429 Too Many
Requests` con el header `Retry-After`. El valor se calcula a partir
del `MetadataName.RetryAfter` que el limiter expone: si lo conoce,
usa el delta exacto hasta el reset de la ventana; si no, cae al
fallback `"900"` (los 15 minutos de la ventana por default). Esto
significa que un cliente que observa `Retry-After: 47` sabe que en 47
segundos puede reintentar; uno que observa `Retry-After: 900` sabe
que tiene que esperar una ventana entera.

El header se serializa con `CultureInfo.InvariantCulture` para que
el número viaje sin sorpresas regionales (algunos clientes esperan
sólo dígitos ASCII). El serializador por default de .NET podría usar
la cultura del proceso si la regla no estuviera explícita.

## Fuga de cuota entre endpoints

Una preocupación natural es "si agoto el rate limit de Refresh,
¿también agoto el de Login?". La respuesta corta es no: cada
`AddPolicy` crea su propio namespace de particiones y su propio
limiter. La cuota de Refresh vive en una tabla separada de la cuota
de ForgotPassword, y agotar la una no toca a la otra.

Esta independencia se sostiene porque cada `AddPolicy(name, factory)`
construye un `RateLimitPartition` con su propio factory. Aunque dos
políticas usen la misma partition key (por ejemplo, IP para
ForgotPassword y Refresh), el limiter es distinto — el bucket del
uno no alimenta el del otro. La verificación en tests vive en
`AuthRefreshRateLimitTests`: tras agotar el budget de Refresh, un
`POST /api/v1/auth/login` sigue respondiendo normalmente.

La consecuencia operativa es que un atacante con una botnet que
quema la cuota de Refresh no bloquea el signup legítimo desde la
misma IP. Los buckets son disjuntos por construcción.

## Trade-offs y consecuencias operativas

La elección de IP como llave tiene un problema conocido: las
corporaciones y los ISP suelen presentar una sola IP para muchos
usuarios. Si el rate limit se fijara en 3/15min por IP para
`ForgotPassword`, una oficina con 200 empleados tendría una sola
cuota para los 200. Por eso las cuotas anónimas son relativamente
generosas (3-5 por ventana) y por eso el sistema distingue entre
"cuota de forgot" y "cuota de cambio" — son amenazas distintas.

Una IP compartida (NAT, VPN, proxy transparente) puede agotar la
cuota de un usuario legítimo. La consecuencia es que el usuario ve
un `429` que no sabe interpretar. La defensa operativa es
documentar este caso en mensajes de error Web (el `429` se mapea a
un banner "demasiados intentos, esperá unos minutos") y permitir
contacto a soporte para levantar el bloqueo si se identifica un
falso positivo.

Una mejora futura posible sería usar `X-Forwarded-For` cuando el
host está detrás de un proxy confiable. Esa defensa está
explícitamente fuera de scope hoy (ver nota en
`docs/decisiones-implementacion.md §"Reverse proxy y UseForwardedHeaders"`);
introducirla sin lista blanca de proxies sería peor que el problema
que resuelve.

## Referencias

- `../reference/06-pipeline-middleware-api.md` — orden de los middlewares, incluido el `UseRateLimiter`.
- `../reference/05-configuracion-opciones-secretos.md` — opciones de `RefreshTokenOptions` que pilotan la política `Refresh`.
- `openspec/specs/password-reset-flow/` y `openspec/specs/password-change/` — los specs Given/When/Then que justifican las cuotas vigentes.
- `docs/decisiones-implementacion.md` — sección "Hardening runtime: cookie y CORS por ambiente" para entender cómo CORS y rate limit interactúan en producción.