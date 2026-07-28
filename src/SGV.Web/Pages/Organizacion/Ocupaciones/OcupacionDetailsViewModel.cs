using SGV.Contracts.Ocupaciones.Dtos;

namespace SGV.Web.Pages.Organizacion.Ocupaciones;

/// <summary>
/// ViewModel de la página Details de Ocupaciones. Proyecta el
/// <see cref="OcupacionDto"/> wire a la vista readonly del shell web y
/// agrega flags derivados (<see cref="EsVigente"/>, <see cref="EsAdministrador"/>)
/// que la Razor Page ramifica para mostrar u ocultar las acciones de ciclo
/// de vida (Finalizar, Eliminar, Reactivar) según REQ-OCC-FORM-003.
/// </summary>
/// <remarks>
/// Modelado espejado de <c>PuestoDetailsViewModel</c>: el DTO wire queda
/// accesible como propiedad y los flags de UI se computan en el record
/// sin estado mutable. El PageModel asigna <see cref="EsAdministrador"/>
/// tras la instanciación (no se puede computar en el constructor primario
/// porque depende de <c>User.IsInRole(...)</c> del request vigente).
/// </remarks>
public sealed class OcupacionDetailsViewModel
{
    /// <summary>DTO wire de la ocupación mostrada en el detalle.</summary>
    public required OcupacionDto Ocupacion { get; init; }

    /// <summary>
    /// <c>true</c> cuando el usuario autenticado tiene rol Administrador
    /// (decisión locked § DEC-3 del change). Lo asigna el PageModel.
    /// </summary>
    public bool EsAdministrador { get; set; }

    /// <summary>
    /// <c>true</c> cuando el estado vigente de la ocupación permite edición,
    /// finalización o eliminación (REQ-OCC-FORM-002 / REQ-OCC-FORM-003).
    /// </summary>
    public bool EsVigente => Ocupacion.Estado == SGV.Contracts.Ocupaciones.Enums.OcupacionEstado.Vigente;

    /// <summary><c>true</c> cuando la ocupación fue finalizada con <c>FechaFin</c> pero no eliminada.</summary>
    public bool EsFinalizada => Ocupacion.Estado == SGV.Contracts.Ocupaciones.Enums.OcupacionEstado.Finalizada;

    /// <summary><c>true</c> cuando la ocupación fue dada de baja lógica.</summary>
    public bool EsEliminada => Ocupacion.Estado == SGV.Contracts.Ocupaciones.Enums.OcupacionEstado.Eliminada;

    /// <summary>
    /// Construye el viewmodel a partir del DTO wire. <see cref="EsAdministrador"/>
    /// queda en <c>false</c> por default; el PageModel lo sobreescribe.
    /// </summary>
    public static OcupacionDetailsViewModel FromDto(OcupacionDto dto)
        => new() { Ocupacion = dto, EsAdministrador = false };
}