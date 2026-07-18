# Delta Spec: 2026-07-18-fix-170-crear-usuario-roles-identity

## Purpose

Localiza al español los `IdentityError` traducidos por `ToIdentityFailure`: cubre los códigos de la política de `IdentityOptions.Password` vigente, los códigos de unicidad/formato ya conocidos y garantiza un fallback en español para códigos no reconocidos. Ningún `IdentityError` debe escapar al cliente en inglés.

## ADDED Requirements

### Requirement: Localización de errores de Identity al español en `ToIdentityFailure`

`ToIdentityFailure` MUST traducir cada `IdentityError.Code` alcanzado por la política de `IdentityOptions.Password` vigente o por validaciones de unicidad/formato a un mensaje en español, y MUST envolverlo en un `UsuarioError` con `Categoria = ErrorCategoria.Validation` y `Code = "IdentityError"`. Códigos no reconocidos MUST caer a un fallback genérico en español (nunca en inglés). El sistema MUST NO emitir al cliente mensajes de Identity cuyo texto esté en inglés.

#### Scenario: `PasswordTooShort` informa la longitud requerida

- **DADO** un `IdentityResult.Failed` con `IdentityError { Code = "PasswordTooShort" }` y `Password.RequiredLength = N`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser una cadena en español que incluya la longitud N exigida (p.ej. `La contraseña debe tener al menos N caracteres.`).

#### Scenario: `PasswordRequiresNonAlphanumeric`

- **DADO** un `IdentityResult.Failed` con `Code = "PasswordRequiresNonAlphanumeric"`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser `La contraseña debe incluir al menos un carácter no alfanumérico.`.

#### Scenario: `PasswordRequiresDigit`

- **DADO** un `IdentityResult.Failed` con `Code = "PasswordRequiresDigit"`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser `La contraseña debe incluir al menos un dígito.`.

#### Scenario: `PasswordRequiresLower`

- **DADO** un `IdentityResult.Failed` con `Code = "PasswordRequiresLower"`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser `La contraseña debe incluir al menos una letra minúscula.`.

#### Scenario: `PasswordRequiresUpper`

- **DADO** un `IdentityResult.Failed` con `Code = "PasswordRequiresUpper"`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser `La contraseña debe incluir al menos una letra mayúscula.`.

#### Scenario: `PasswordRequiresUniqueChars` informa los caracteres únicos requeridos

- **DADO** un `IdentityResult.Failed` con `Code = "PasswordRequiresUniqueChars"` y `RequireUniqueChars = N`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser una cadena en español que indique al menos N caracteres únicos (p.ej. `La contraseña debe incluir al menos N caracteres únicos.`).

#### Scenario: `DuplicateUserName` localizado al español

- **DADO** un `IdentityResult.Failed` con `Code = "DuplicateUserName"`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser `El nombre de usuario ya está en uso.`.

#### Scenario: `DuplicateEmail` localizado al español

- **DADO** un `IdentityResult.Failed` con `Code = "DuplicateEmail"`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser `El email ya está en uso.`.

#### Scenario: `InvalidEmail` localizado al español

- **DADO** un `IdentityResult.Failed` con `Code = "InvalidEmail"`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser `El email no tiene un formato válido.`.

#### Scenario: `InvalidUserName` localizado al español

- **DADO** un `IdentityResult.Failed` con `Code = "InvalidUserName"`
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser `El nombre de usuario sólo admite letras, números, punto, guión bajo y guión medio.`.

#### Scenario: Código no reconocido cae a fallback en español

- **DADO** un `IdentityResult.Failed` con un `Code` no mapeado (p.ej. `ConcurrencyFailure`, `RecoveryCodeRedemptionFailed`)
- **CUANDO** `ToIdentityFailure` procesa el resultado
- **ENTONCES** el `UsuarioError.Mensaje` MUST ser un mensaje genérico en español
- **Y** MUST NOT estar redactado en inglés.

#### Scenario: Todos los errores localizados comparten `Categoria = Validation` y `Code = "IdentityError"`

- **DADO** cualquiera de los `IdentityError.Code` cubiertos por este requisito (política de contraseña, duplicados, formato, fallback)
- **CUANDO** `ToIdentityFailure` produce el `UsuarioError`
- **ENTONCES** el `UsuarioError.Categoria` MUST ser `ErrorCategoria.Validation`
- **Y** el `UsuarioError.Code` MUST ser `"IdentityError"`.
