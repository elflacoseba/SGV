namespace SGV.Aplicacion.Seguridad;

public interface IUsuarioActual
{
    string? UserId { get; }

    Guid? PersonaId { get; }

    IReadOnlyCollection<string> Roles { get; }

    Guid? CorrelationId { get; }
}
