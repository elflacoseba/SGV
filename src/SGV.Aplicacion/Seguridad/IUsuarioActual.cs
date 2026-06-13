namespace SGV.Aplicacion.Seguridad;

public interface IUsuarioActual
{
    string? UserId { get; }

    Guid? CorrelationId { get; }
}
