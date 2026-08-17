namespace SGV.Web.Integration.Organizacion;

/// <summary>
/// Presentación de la vigencia de una unidad organizativa para la UI
/// del shell web. Combina un texto legible con una clase CSS opcional
/// para colorear el badge cuando la ventana está fuera del rango de
/// la fecha de referencia (issue #281).
/// </summary>
/// <remarks>
/// "Aún no vigente" se renderiza con <c>badge-soft-info</c>;
/// "Fuera de vigencia" con <c>badge-soft-warning</c>. Las ventanas
/// dentro de rango (incluyendo "Vigencia abierta" cuando ambos extremos
/// son <c>null</c>) no llevan badge.
/// <para>
/// Se aloja en <c>SGV.Web.Integration</c> y no en
/// <c>SGV.Web.Pages</c> para evitar una dependencia circular:
/// <see cref="UnidadOrganizativaListItemViewModel"/> consume este
/// record, y ese viewmodel ya vive en <c>Integration</c> porque es
/// consumido por <see cref="IUnidadOrganizativaApiClient"/>.
/// </para>
/// </remarks>
public sealed record VigenciaViewModel(string Texto, string? BadgeClass)
{
    /// <summary>
    /// Construye la vista de vigencia a partir del rango persistido y
    /// la fecha de referencia (pasada por el caller para mantener el
    /// helper testeable sin acoplar a <c>DateTime.Today</c>).
    /// </summary>
    public static VigenciaViewModel Desde(DateOnly? vigenteDesde, DateOnly? vigenteHasta, DateOnly hoy)
    {
        if (vigenteDesde is null && vigenteHasta is null)
        {
            return new VigenciaViewModel("Vigencia abierta", null);
        }

        if (vigenteDesde is not null && vigenteHasta is null)
        {
            var desde = vigenteDesde.Value;
            return desde > hoy
                ? new VigenciaViewModel($"Aún no vigente (desde {desde:dd/MM/yyyy})", "badge-soft-info")
                : new VigenciaViewModel($"Desde {desde:dd/MM/yyyy}", null);
        }

        if (vigenteHasta is not null && vigenteDesde is null)
        {
            var hasta = vigenteHasta.Value;
            return hasta < hoy
                ? new VigenciaViewModel($"Fuera de vigencia (hasta {hasta:dd/MM/yyyy})", "badge-soft-warning")
                : new VigenciaViewModel($"Hasta {hasta:dd/MM/yyyy}", null);
        }

        var desdeR = vigenteDesde!.Value;
        var hastaR = vigenteHasta!.Value;

        if (hastaR < hoy)
        {
            return new VigenciaViewModel(
                $"Fuera de vigencia ({desdeR:dd/MM/yyyy} — {hastaR:dd/MM/yyyy})",
                "badge-soft-warning");
        }

        if (desdeR > hoy)
        {
            return new VigenciaViewModel(
                $"Aún no vigente ({desdeR:dd/MM/yyyy} — {hastaR:dd/MM/yyyy})",
                "badge-soft-info");
        }

        return new VigenciaViewModel($"{desdeR:dd/MM/yyyy} — {hastaR:dd/MM/yyyy}", null);
    }
}
