using SGV.Aplicacion.Seguridad;

namespace SGV.Aplicacion.Seguridad.Usuarios;

public sealed class RolServicioConsulta : IRolServicioConsulta
{
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(RolesSgv.Todos);
}
