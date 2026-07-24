using SGV.Aplicacion.Setup;
using SGV.Contracts.Setup;

namespace SGV.Tests.Setup;

/// <summary>
/// Fake <see cref="ISetupServicio"/> para tests del controller que no
/// requieren MySQL real. Los handlers <see cref="ObtenerEstadoAsyncHandler"/>
/// y <see cref="CrearAdminAsyncHandler"/> son inyectados vía DI por los
/// tests; si un handler no está configurado, devuelve un resultado
/// por defecto razonable.
/// </summary>
internal sealed class FakeSetupServicio : ISetupServicio
{
    private readonly SetupStatusResponse? _obtenerEstadoDefault;
    private readonly SetupCommandResult? _crearAdminDefault;
    private readonly Func<SetupRequest, CancellationToken, Task<SetupCommandResult>>? _crearAdminHandler;

    public FakeSetupServicio(
        Func<SetupStatusResponse>? obtenerEstadoAsync = null,
        Func<SetupRequest, SetupCommandResult>? crearAdminAsync = null,
        Func<SetupRequest, CancellationToken, Task<SetupCommandResult>>? crearAdminAsyncWithCt = null)
    {
        if (obtenerEstadoAsync is not null)
        {
            _obtenerEstadoDefault = obtenerEstadoAsync();
        }
        else
        {
            _obtenerEstadoDefault = new SetupStatusResponse(RequiresSetup: false);
        }

        if (crearAdminAsyncWithCt is not null)
        {
            _crearAdminHandler = crearAdminAsyncWithCt;
            _crearAdminDefault = null;
        }
        else
        {
            _crearAdminHandler = null;
            _crearAdminDefault = crearAdminAsync is null
                ? SetupCommandResult.Success(new SetupResult(Guid.NewGuid(), "user-id", "admin"))
                : null;
            _crearAdminSync = crearAdminAsync;
        }
    }

    private readonly Func<SetupRequest, SetupCommandResult>? _crearAdminSync;

    public Task<SetupStatusResponse> ObtenerEstadoAsync(CancellationToken ct = default)
        => Task.FromResult(_obtenerEstadoDefault!);

    public Task<SetupCommandResult> CrearAdminAsync(SetupRequest request, CancellationToken ct = default)
    {
        if (_crearAdminHandler is not null)
        {
            return _crearAdminHandler(request, ct);
        }

        if (_crearAdminSync is not null)
        {
            return Task.FromResult(_crearAdminSync(request));
        }

        return Task.FromResult(_crearAdminDefault!);
    }
}
