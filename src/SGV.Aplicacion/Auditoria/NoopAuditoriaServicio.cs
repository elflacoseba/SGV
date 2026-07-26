namespace SGV.Aplicacion.Auditoria;

/// <summary>
/// Implementación nula de <see cref="IAuditoriaServicio"/> que no
/// persiste nada. Se usa como default en constructores de back-compat
/// (issue #202) para que los tests pre-existentes no necesiten cablear
/// una auditoría falsa; el código de producción real resuelve
/// <see cref="AuditoriaServicio"/> desde DI.
/// </summary>
internal sealed class NoopAuditoriaServicio : IAuditoriaServicio
{
    public Task RegistrarAsync(
        string entidad,
        string entityId,
        string accion,
        string? usuarioOperadorId,
        IReadOnlyDictionary<string, object?> valoresAnteriores,
        IReadOnlyDictionary<string, object?> valoresNuevos,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}