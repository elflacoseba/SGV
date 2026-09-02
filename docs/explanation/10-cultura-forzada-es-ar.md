# Cultura forzada es-AR y contrato HTTP invariante

## La asimetría entre capa de presentación y wire contract

SGV opera con una única cultura regional, `es-AR`, fijada en proceso
tanto en `SGV.Api` como en `SGV.Web` mediante
`AddLocalization()` + `Configure<RequestLocalizationOptions>` con
`DefaultRequestCulture = new RequestCulture("es-AR")`. La elección
se sostiene en tres invariantes simultáneas:

- `SupportedCultures` y `SupportedUICultures` se limitan a `[es-AR]`.
- `FallBackToParentCultures = false`, así que un cliente que pide
  `en-US` no degrada silenciosamente a `es` (el padre).
- `app.UseRequestLocalization()` se inserta entre `UseRouting()` y
  `UseAuthentication()` en la Web, y temprano en el pipeline de la
  API (después de CORS, antes de RateLimiter).

El motivo es operativo. Tres bugs se concentraban en la gestión de
habilidades de Cargos antes de la decisión documentada en issue #191:
los banners de feedback no eran dismissibles (la cultura del host
heredada de la máquina del developer hacía fallar el binding JS);
el input `type="number"` para Ponderación sólo aceptaba `"."` como
separador decimal (HTML5 ignora la cultura); la ponderación podía
llegar vacía y el servicio aplicaba un default invisible. Forzar
`es-AR` cierra los tres vectores en una sola decisión, porque
quien diseña los inputs sabe exactamente cómo se va a formatear.

## Por qué el wire contract no se entera

La capa de presentación y la capa de transporte son ortogonales. La
cultura `es-AR` afecta cómo se renderizan números y cómo se ordenan
strings — pero System.Text.Json, el serializer por default de ASP.NET
Core, no consulta `CultureInfo.CurrentCulture` para serializar
decimales, fechas o booleanos. Los decimales viajan como `"1.50"`,
no como `"1,50"`. Las fechas como `DateTime` se serializan como ISO
8601 (`"2024-09-15T18:30:00Z"`), no como `"15/09/2024 18:30"`.

El contrato HTTP entre `SGV.Web` y `SGV.Api` transporta decimales
con punto y fechas con sufijo `Z` UTC, sin importar el `Accept-Language`
del navegador ni la configuración regional del servidor. Esto es lo
que la decisión llama "JSON wire invariante" y está protegido por la
suite: cualquier test que valide un payload HTTP no toca
`CultureInfo` porque no hace falta.

## Dónde sí afecta la cultura

La cultura del proceso impacta tres lugares concretos:

- **Render HTML y binding de inputs.** Razor Pages usa la cultura
  para decidir qué separador decimal mostrar en un `<input
  type="text" inputmode="decimal">` y para parsear lo que el
  usuario tipea al hacer POST. Con `es-AR`, `","` es el separador
  esperado y `"."` se acepta sólo porque el parser es tolerante.
- **Orden de strings.** `StringComparer.Create(CultureInfo.CurrentCulture,
  ...)` en el orden de unidades organizativas (y en cualquier
  listado que use ordenamiento "natural" por cultura) devuelve
  resultados distintos según la cultura. Forzar `es-AR` elimina la
  variabilidad entre deploys con distinta configuración regional
  del sistema operativo.
- **Mensajes localized.** El middleware de localización provee
  fallback a `es-AR` para `IStringLocalizer<T>` si ningún resource
  coincide con la cultura solicitada. Como `SupportedCultures` está
  fijo en `[es-AR]`, no hay ambigüedad: si un día se suma un
  `Resources.es.resx`, siempre se elige esa variante.

## Consecuencias operativas

La invariancia del wire simplifica enormemente la integración. Un
cliente externo puede consumir la API sin tener que negociar la
cultura: el JSON que recibe es estable independientemente de la
configuración regional del host que lo sirve. La consecuencia
inversa también es cierta: si en el futuro SGV se consume desde un
proceso que sí espera formato regional (por ejemplo, una planilla de
cálculo importada en formato europeo), hay que convertir en el borde
del cliente, no esperar que la API lo haga.

El `Retry-After` header del rate limiter se serializa explícitamente
con `InvariantCulture` para evitar que un deploy con cultura
no-española escriba `"900,5"` en lugar de `"900.5"`. Esa excepción
está documentada en `docs/decisiones-implementacion.md §"Cultura
regional es-AR"` y vale como precedente para futuros headers que
deban viajar invariantes.

Hay un punto donde la cultura y el wire pueden chocar: los inputs de
formularios Web. `Ponderación` (en Habilidades de Cargo) dejó de ser
`type="number"` precisamente porque HTML5 ignora la cultura y exigía
`"."`. Ahora es `type="text" inputmode="decimal" pattern="..."` y la
validación server-side en `CargoSkillPonderacionRule.TryParse`
tolera coma es-AR. Esa cadena (`text` + `inputmode` + `pattern` +
validación tolerante) es el patrón para futuros inputs numéricos en
la Web.

## Referencias

- `../how-to/12-configurar-smtp-real.md` — los emails que el sistema envía también formatean importes con la cultura del proceso.
- `../reference/05-configuracion-opciones-secretos.md` — donde podría documentarse la cultura si en el futuro se la externaliza a configuración.
- `docs/decisiones-implementacion.md` — sección "Cultura regional es-AR como default único (issue #191)", con la decisión adoptada, las invariantes preservadas y la cobertura nueva.