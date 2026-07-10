# Delta para autenticación web SGV

## Requisitos AÑADIDOS

### Requisito: Atributos de la cookie de autenticación por ambiente

La cookie que carga el ticket de autenticación de `SGV.Web` DEBE aplicar atributos de seguridad acordes al ambiente de ejecución para evitar la filtración del JWT que `ApiBearerTokenHandler` reenvía a `SGV.Api` como `Authorization: Bearer`. El resto de los requisitos de esta especificación (login, logout, centralización de endpoints) NO se modifican.

| Atributo        | Development        | Distinto de Development |
|-----------------|--------------------|--------------------------|
| `HttpOnly`      | `true`             | `true`                   |
| `SameSite`      | `Lax`              | `Lax`                    |
| `SecurePolicy`  | `SameAsRequest`    | `Always`                 |

#### Escenario: Atributos en ambiente distinto de Development

- **DADO** que `ASPNETCORE_ENVIRONMENT` es distinto de `Development` (por ejemplo `Production` o `Staging`)
- **CUANDO** la aplicación registra la autenticación por cookies en `src/SGV.Web/Program.cs`
- **ENTONCES** la `CookieOptions` resultante DEBE tener `HttpOnly == true`
- **Y** `SameSite == SameSiteMode.Lax`
- **Y** `SecurePolicy == CookieSecurePolicy.Always`.

#### Escenario: Atributos en Development

- **DADO** que `ASPNETCORE_ENVIRONMENT == "Development"`
- **CUANDO** la aplicación registra la autenticación por cookies
- **ENTONCES** la `CookieOptions` resultante DEBE tener `HttpOnly == true`
- **Y** `SameSite == SameSiteMode.Lax`
- **Y** `SecurePolicy == CookieSecurePolicy.SameAsRequest`.

#### Escenario: Atributos verificables desde el contenedor de DI

- **DADO** que la cookie de autenticación está registrada
- **CUANDO** se inspeccionan las opciones del esquema cookie expuestas por el contenedor de DI
- **ENTONCES** los valores de `HttpOnly`, `SameSite` y `SecurePolicy` DEBEN coincidir con la tabla de atributos por ambiente
- **Y** ningún path DEBE sobrescribir esos atributos fuera de la rama de registro de `AddCookie(...)`.
