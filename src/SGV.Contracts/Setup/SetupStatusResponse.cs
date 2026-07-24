namespace SGV.Contracts.Setup;

/// <summary>
/// Respuesta liviana del endpoint <c>GET /api/v1/setup/status</c>
/// (issue #195). Sólo el flag <see cref="RequiresSetup"/>; el catálogo
/// de <c>TipoDocumento</c> se consulta por separado contra
/// <c>GET /api/v1/tipos-documento</c> (que ahora admite
/// <c>[AllowAnonymous]</c>) para mantener este endpoint barato y
/// cacheable con TTL 30s en el shell Web.
/// </summary>
public sealed record SetupStatusResponse(bool RequiresSetup);
