namespace SGV.Aplicacion.Auditoria;

/// <summary>
/// Explicit audit port for mutations that are not observed by the EF audit interceptor.
/// </summary>
public interface IAuditoriaServicio
{
    Task RegistrarAsync(
        string entidad,
        string entityId,
        string accion,
        string? usuarioOperadorId,
        IReadOnlyDictionary<string, object?> valoresAnteriores,
        IReadOnlyDictionary<string, object?> valoresNuevos,
        CancellationToken cancellationToken = default);
}
