namespace SGV.Web.Integration.Ocupaciones;

/// <summary>
/// Stable binding keys used by the Ocupacion Create/Edit form contract.
/// Centralized so the partial (<c>_Form.cshtml</c>), the page models
/// (<c>Create.cshtml.cs</c>, <c>Edit.cshtml.cs</c>) and tests agree on the
/// exact strings the model binder expects. Espejo de
/// <see cref="Organizacion.PuestoFormKeys"/> extendido con los cinco campos
/// de Ocupación.
/// </summary>
public static class OcupacionFormKeys
{
    /// <summary>
    /// Common prefix used by Razor's <c>asp-for="Input.Xyz"</c> tag helpers.
    /// </summary>
    public const string InputPrefix = "Input.";

    /// <summary>Binding key for the <c>PersonaId</c> field.</summary>
    public const string PersonaIdKey = InputPrefix + nameof(OcupacionInputModel.PersonaId);

    /// <summary>Binding key for the <c>PuestoId</c> field.</summary>
    public const string PuestoIdKey = InputPrefix + nameof(OcupacionInputModel.PuestoId);

    /// <summary>Binding key for the <c>FechaInicio</c> field.</summary>
    public const string FechaInicioKey = InputPrefix + nameof(OcupacionInputModel.FechaInicio);

    /// <summary>Binding key for the <c>TipoAsignacion</c> field.</summary>
    public const string TipoAsignacionKey = InputPrefix + nameof(OcupacionInputModel.TipoAsignacion);

    /// <summary>Binding key for the <c>Observaciones</c> field.</summary>
    public const string ObservacionesKey = InputPrefix + nameof(OcupacionInputModel.Observaciones);
}