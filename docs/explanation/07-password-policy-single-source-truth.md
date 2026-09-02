# PasswordPolicy como single source of truth

## La duplicación que ya no queremos

Antes de que existiera `PasswordPolicy`, las reglas de complejidad de
contraseña vivían en cinco lugares distintos. El equipo las había
copiado en cada uno cuando se introdujo cada flujo nuevo, sin una
abstracción central:

1. `src/SGV.Api/Program.cs` — bloque `AddIdentityCore<SgvIdentityUser>`.
2. `src/SGV.Aplicacion/Seguridad/PasswordChange/ChangePasswordRequestValidator.cs` — FluentValidation del request.
3. `src/SGV.Aplicacion/Seguridad/PasswordReset/ResetPasswordRequestValidator.cs` — FluentValidation del reset.
4. `src/SGV.Web/Pages/Auth/CambiarContrasena.cshtml.cs` — pre-flight check vía `MeetsPasswordPolicy`.
5. `src/SGV.Web/Pages/Auth/ResetPassword.cshtml.cs` — idem para el reset web.

Subir la longitud mínima de 6 a 8, por ejemplo, requería editar los
cinco archivos. Si alguien olvidaba uno, el signup podía seguir
aceptando contraseñas de 6 mientras el reset las rechazaba de 8. La
causa raíz era que cada copia tenía su propia constante local y el
compilador no podía ayudar a mantenerlas sincronizadas. No había un
test que cubriera "los cinco lugares cumplen la misma regla".

## Cómo lo resuelve `PasswordPolicy`

El archivo `src/SGV.Contracts/Seguridad/PasswordPolicy.cs` expone las
cinco reglas canónicas como constantes públicas:

```csharp
public const int MinLength = 6;
public const bool RequireLowercase = true;
public const bool RequireUppercase = true;
public const bool RequireDigit = true;
public const bool RequireNonAlphanumeric = true;
public const string LowercasePattern = "[a-z]+";
public const string UppercasePattern = "[A-Z]+";
public const string DigitPattern = "[0-9]+";
public const string NonAlphanumericPattern = "[^a-zA-Z0-9]+";
```

Cada uno de los cinco lugares consume estas constantes en lugar de
declarar las suyas. `AddIdentityCore` lee `PasswordPolicy.RequireDigit`
para `options.Password.RequireDigit`, etc.; los dos validators de
FluentValidation referencian `PasswordPolicy.MinLength` y los cuatro
`PasswordPolicy.*Pattern`; las dos páginas Web invocan
`PasswordPolicy.IsCompliant(password)` como pre-flight.

Vivir en `SGV.Contracts` no es casual: este proyecto es leaf, lo
referencian tanto `SGV.Api` como `SGV.Web` (más `SGV.Aplicacion`), así
que la constante es accesible desde cada uno de los cinco sitios
originales sin introducir nuevas dependencias de capa. Si viviera en
`SGV.Dominio` arrastraría reglas de negocio al wire; si viviera en
`SGV.Api`, la Web no podría usarla. `SGV.Contracts` es exactamente
donde corresponde.

## El método `IsCompliant` y su simetría con los validators

`PasswordPolicy.IsCompliant(string?)` devuelve `false` para entradas
nulas, vacías o más cortas que `MinLength`, y luego aplica cada regex
de clase. La función devuelve `bool`, no `IEnumerable<Error>`. Esto
se debe a que su único consumidor es Razor: la página quiere saber
"¿puedo habilitar el botón de submit?" sin armar un `ValidationProblemDetails`.

La simetría con los validators FluentValidation es importante. El
contrato observable de "qué contraseñas pasan" es idéntico: si la
expresión regular `[a-z]+` matchea en `IsCompliant`, también
`.Matches(PasswordPolicy.LowercasePattern)` matchea en el validator
— porque las dos leen la misma constante. El equipo testea esta
simetría con `AsignarCargoSkillRequestValidatorTests`-style: un test
parametrizado que prueba una grilla de contraseñas contra los dos
caminos y assertea que ambos pasan o ambos rechazan.

## Qué pasa si alguien rompe el single source of truth

Si alguien futuro edita `PasswordPolicy` pensando que sólo la API
consume la constante — por ejemplo, baja `MinLength` a 4 — el cambio
se propaga automáticamente a Identity, a los validators y a los
pre-flights. La consecuencia es un weakening global del modelo de
seguridad. La inversa también es cierta: subir a 8 hace que el botón
"Guardar" de la Web se habilite sólo con contraseñas de 8+.

El peligro opuesto es que alguien reintroduzca una constante local
"porque sólo aplica a este flujo". El detector natural es code
review: una constante numérica o regex en un archivo nuevo que mira
contraseñas debería levantar la mano. La línea roja es que cualquier
validación que diga "regex matchea [a-z]" sin pasar por
`PasswordPolicy.LowercasePattern` está reintroduciendo la duplicación.

Un test de regresión efectivo sería un `PasswordPolicy_ConstantesUnicas`
que enumere los archivos del repo buscando constantes numéricas o
regex de complejidad de contraseña y falle si encuentra alguna fuera
de `PasswordPolicy.cs`. El equipo no lo mantiene hoy — la disciplina
es por code review — pero existe como follow-up documentado.

## Consecuencias operativas

La regla vive en proceso y el wire contract no se entera. El cambio
de política nunca requiere actualizar el front: los validators de la
API rechazan las contraseñas inválidas con `400 ValidationProblemDetails`
y la Web las cuenta como no cumplidas vía el pre-flight. La UX de la
Web se ajusta automáticamente porque consume la misma constante.

Hay una asimetría intencional que vale la pena entender: el
pre-flight de la Web puede equivocarse por "falso negativo" — decir
que una contraseña es inválida cuando en realidad el validator del
backend la aceptaría. Esto puede ocurrir si los regex del
`PasswordPolicy` y los del validator divergen en su interpretación de
caracteres Unicode (por ejemplo, `[a-z]` excluye letras acentuadas,
pero `[a-zA-Z]` ya las cubre parcialmente). La defensa es que ambas
implementaciones usan la misma constante de string. Si el futuro
cambia esa constante, ambos lados cambian juntos.

> ⚠️ A verificar: los archivos `CambiarContrasena.cshtml.MeetsPasswordPolicy`
> y `ResetPassword.cshtml.MeetsPasswordPolicy` referenciados por el
> comentario XML doc de `PasswordPolicy` pueden haber sido refactorizados.
> El recorrido confirmó que los `.cshtml.cs` usan `PasswordPolicy.IsCompliant`,
> pero el nombre interno del helper puede haber cambiado.

## Referencias

- `../how-to/02-operar-flujo-recuperacion-contrasena.md` — el flujo end-to-end desde el formulario de recuperación hasta la persistencia con hash.
- `../how-to/04-bloquear-desbloquear-usuario.md` — qué hace el sistema cuando la política falla reiteradamente.
- `../reference/04-roles-matriz-autorizacion.md` — dónde encaja la política con los roles administrativos.
- `openspec/specs/password-change/` y `openspec/specs/password-reset-flow/` — los specs Given/When/Then vigentes.
- `docs/decisiones-implementacion.md` — búsqueda por "PasswordPolicy" si la decisión queda documentada en algún change futuro.