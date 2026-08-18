using SGV.Aplicacion.Seguridad;

namespace SGV.Tests.Aplicacion.Comun;

/// <summary>
/// Stub de <see cref="IUsuarioActual"/> para tests. Permite controlar
/// <see cref="UserId"/> (y demás propiedades) desde el helper
/// <c>CrearServicio</c> sin depender de <c>HttpContext</c>.
/// </summary>
/// <remarks>
/// <para>
/// Por defecto devuelve <c>UserId = "test-user-id"</c> para que los tests
/// pre-existentes que asumen <c>ChangedByUserId</c> poblado propaguen el
/// valor sin tocar cada uno. Tests que necesitan un principal anónimo
/// usan <see cref="Anonymous"/>.
/// </para>
/// <para>
/// Cambio <c>vacantes-hardening</c> D-1 (cluster A): inyecta identidad
/// en <c>VacanteServicioComandos.CambiarEstadoAsync</c> y
/// <c>OcupacionServicioComandos.CrearOcupacionCubriendoVacanteAsync</c>.
/// </para>
/// </remarks>
internal sealed class FakeUsuarioActual : IUsuarioActual
{
    public string? UserId { get; set; } = "test-user-id";
    public Guid? PersonaId { get; set; } = Guid.Parse("80000000-0000-0000-0000-000000000001");
    public IReadOnlyCollection<string> Roles { get; set; } = new[] { "Administrador" };
    public Guid? CorrelationId { get; set; } = Guid.Parse("80000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Singleton que representa un principal anónimo. <c>UserId</c> es
    /// <c>null</c> y <c>Roles</c> está vacío — usado para ejercitar el
    /// guard defensivo del servicio (D-1, comportamiento Unauthorized).
    /// </summary>
    public static IUsuarioActual Anonymous { get; } = new FakeUsuarioActual
    {
        UserId = null,
        PersonaId = null,
        Roles = [],
        CorrelationId = null
    };
}
