using FluentValidation;
using SGV.Contracts.Setup;

namespace SGV.Aplicacion.Setup.Validaciones;

/// <summary>
/// Reglas FluentValidation para <see cref="SetupRequest"/> (issue #195).
/// La política de password real la ejecuta Identity dentro del
/// gateway (<see cref="SetupErrorCode.PasswordDebil"/>); este validator
/// pre-filtra sólo la forma de los campos y longitudes razonables para
/// evitar round-trips caros a Identity cuando el input ya es
/// claramente inválido.
/// </summary>
public sealed class SetupRequestValidator : AbstractValidator<SetupRequest>
{
    public SetupRequestValidator()
    {
        RuleFor(request => request.Nombres)
            .NotEmpty()
                .WithMessage("El nombre es obligatorio.")
            .MaximumLength(100)
                .WithMessage("El nombre no puede tener más de 100 caracteres.");

        RuleFor(request => request.Apellidos)
            .NotEmpty()
                .WithMessage("El apellido es obligatorio.")
            .MaximumLength(100)
                .WithMessage("El apellido no puede tener más de 100 caracteres.");

        RuleFor(request => request.Legajo)
            .MaximumLength(50)
                .When(request => request.Legajo is not null)
                .WithMessage("El legajo no puede tener más de 50 caracteres.");

        RuleFor(request => request.Email)
            .NotEmpty()
                .WithMessage("El email es obligatorio.")
            .EmailAddress()
                .WithMessage("El email no tiene un formato válido.")
            .MaximumLength(256)
                .WithMessage("El email no puede tener más de 256 caracteres.");

        RuleFor(request => request.UserName)
            .NotEmpty()
                .WithMessage("El nombre de usuario es obligatorio.")
            .MinimumLength(3)
                .WithMessage("El nombre de usuario debe tener al menos 3 caracteres.")
            .MaximumLength(50)
                .WithMessage("El nombre de usuario no puede tener más de 50 caracteres.")
            .Matches("^[A-Za-z0-9._-]+$")
                .WithMessage("El nombre de usuario sólo admite letras, números, punto, guión bajo y guión medio.");

        RuleFor(request => request.Password)
            .NotEmpty()
                .WithMessage("La contraseña es obligatoria.")
            .MinimumLength(6)
                .WithMessage("La contraseña debe tener al menos 6 caracteres.")
            .MaximumLength(128)
                .WithMessage("La contraseña no puede tener más de 128 caracteres.");

        // Documento: si TipoDocumentoId está presente, NumeroDocumento también.
        RuleFor(request => request.NumeroDocumento)
            .NotEmpty()
                .When(request => request.TipoDocumentoId.HasValue)
                .WithMessage("Debe ingresar el número de documento cuando selecciona un tipo de documento.")
            .MaximumLength(50)
                .When(request => request.NumeroDocumento is not null)
                .WithMessage("El número de documento no puede tener más de 50 caracteres.");

        RuleFor(request => request.Telefono)
            .MaximumLength(50)
                .When(request => request.Telefono is not null)
                .WithMessage("El teléfono no puede tener más de 50 caracteres.");
    }
}
