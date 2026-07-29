using SGV.Contracts.Personas.Consultas.Dtos;

namespace SGV.Web.Helpers;

/// <summary>
/// Helper estático que centraliza la composición de los textos derivados de
/// <see cref="PersonaDto"/> que aparecen en la card de persona del shell
/// web. Antes de este helper existían tres copias inline equivalentes en
/// <c>Usuarios/Details.cshtml</c>, <c>Usuarios/_Form.cshtml</c> y
/// <c>Ocupaciones/_Form.cshtml</c> (esta última como
/// <c>FormatearDocumento</c>); ahora todos los consumers invocan
/// <see cref="FormatDocumento"/> vía <c>@using SGV.Web.Helpers</c> en
/// <c>_ViewImports.cshtml</c>.
/// </summary>
/// <remarks>
/// Slice 1 / PR 1 del change <c>reusable-persona-card</c> (issue #219).
/// El separador entre <c>TipoDocumentoCodigo</c> y <c>NumeroDocumento</c>
/// es un único **espacio**, preservando el markup server-side vigente y
/// evitando regresión visual (PER-CARD-09). El colon que usa el JS
/// <c>personaDisplay</c> vive en otra display distinta y no se toca.
/// </remarks>
public static class PersonaFormatHelper
{
    /// <summary>
    /// Compone la etiqueta de Documento a partir de un
    /// <see cref="PersonaDto"/> en formato
    /// <c>"{TipoDocumentoCodigo} {NumeroDocumento}"</c> cuando ambos están
    /// presentes. Reglas (PERFMT-01/02):
    /// <list type="bullet">
    ///   <item>DTO nulo → <see cref="string.Empty"/>.</item>
    ///   <item>Sólo tipo presente → retorna el tipo.</item>
    ///   <item>Sólo número presente → retorna el número.</item>
    ///   <item>Sin tipo ni número pero con <c>Legajo</c> → retorna <c>Legajo</c>.</item>
    ///   <item>Sin tipo, sin número, sin <c>Legajo</c> → <see cref="string.Empty"/>.</item>
    /// </list>
    /// </summary>
    /// <param name="persona">DTO de persona; puede ser <c>null</c>.</param>
    /// <returns>Etiqueta lista para renderizar.</returns>
    public static string FormatDocumento(PersonaDto? persona)
    {
        if (persona is null)
        {
            return string.Empty;
        }

        var tipo = persona.TipoDocumentoCodigo;
        var numero = persona.NumeroDocumento;
        var sinTipo = string.IsNullOrWhiteSpace(tipo);
        var sinNumero = string.IsNullOrWhiteSpace(numero);

        if (sinTipo && sinNumero)
        {
            // PERFMT-02: sin documento, cae a Legajo si existe.
            return string.IsNullOrWhiteSpace(persona.Legajo) ? string.Empty : persona.Legajo;
        }

        if (sinTipo)
        {
            return numero!;
        }

        if (sinNumero)
        {
            return tipo!;
        }

        return $"{tipo} {numero}";
    }
}