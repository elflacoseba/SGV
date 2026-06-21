namespace SGV.Aplicacion.Seguridad.Usuarios;

public sealed record CrearUsuarioRequest(
    Guid PersonaId,
    string UserName,
    string Email,
    string Password,
    IReadOnlyCollection<string> Roles);

public sealed record AsignarRolesRequest(IReadOnlyCollection<string> Roles);

public sealed record LoginRequest(string UserNameOrEmail, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record UsuarioDto(
    string Id,
    Guid PersonaId,
    string UserName,
    string Email,
    IReadOnlyCollection<string> Roles);

public enum UsuarioErrorType
{
    NotFound,
    Conflict,
    Validation,
    Unauthorized
}

public sealed record UsuarioError(UsuarioErrorType Type, string Code, string Message);

public sealed record UsuarioCommandResult(bool IsSuccess, UsuarioDto? Value, UsuarioError? Error)
{
    public static UsuarioCommandResult Success(UsuarioDto value) => new(true, value, null);

    public static UsuarioCommandResult Failure(UsuarioError error) => new(false, null, error);
}

public interface IUsuarioIdentityGateway
{
    Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> AsignarRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}

public interface IUsuarioServicioComandos
{
    Task<UsuarioCommandResult> CrearAsync(CrearUsuarioRequest request, CancellationToken cancellationToken = default);

    Task<UsuarioCommandResult> AsignarRolesAsync(string userId, AsignarRolesRequest request, CancellationToken cancellationToken = default);
}

public interface IUsuarioServicioConsulta
{
    Task<IReadOnlyList<UsuarioDto>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IRolServicioConsulta
{
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IAuthServicio
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
