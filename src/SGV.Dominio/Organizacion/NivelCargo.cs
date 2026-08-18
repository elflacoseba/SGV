using SGV.Dominio.Comun;

namespace SGV.Dominio.Organizacion;

/// <summary>
/// Catálogo inmutable de niveles jerárquicos usados por Cargos. El bloque
/// GUID <c>70000000-0000-0000-0000-00000000000X</c> queda reservado para los
/// 4 niveles semilla (Directivo, Conducción Media, Operativo, Académico);
/// ver <c>NivelesCargo</c> para las constantes Guid compartidas con la
/// migración y <c>DatosSemilla.HasData</c>.
/// </summary>
public sealed record class NivelCargo : EntidadBase
{
    private NivelCargo()
    {
    }

    /// <summary>
    /// Crea un nivel de cargo. <paramref name="valorNumerico"/> es un campo
    /// histórico expuesto en el wire (DTO y JSON) para integraciones externas
    /// que lo consumen como referencia. El rango válido es el completo de
    /// <see cref="byte"/> (0..255) intencionalmente: el orden semántico
    /// entre niveles lo define <paramref name="orden"/> (int, comparador
    /// natural), NO <paramref name="valorNumerico"/>. Esto preserva el
    /// contrato histórico del wire sin restringir el dominio a un rango
    /// arbitrario que pueda requerir migración futura.
    /// </summary>
    /// <param name="codigo">Código único del nivel (varchar(50) UNIQUE).</param>
    /// <param name="nombre">Nombre legible del nivel (varchar(100)).</param>
    /// <param name="valorNumerico">Valor numérico histórico (0..255). Solo
    /// se valida el rango del byte; no hay semántica de orden asociada.</param>
    /// <param name="orden">Orden semántico ascendente (menor = nivel más
    /// bajo). Determina cómo se listan y comparan los niveles.</param>
    public NivelCargo(string codigo, string nombre, int valorNumerico, int orden)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 100);

        if (valorNumerico < 0 || valorNumerico > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(ValorNumerico), "El valor numérico debe estar entre 0 y 255.");
        }

        ValorNumerico = (byte)valorNumerico;
        Orden = orden;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>
    /// Valor numérico histórico (0..255) expuesto en el wire contract.
    /// NO determina el orden jerárquico entre niveles; ese rol lo cumple
    /// <see cref="Orden"/>. Se conserva en el dominio únicamente para no
    /// romper consumidores externos que esperan el campo en el JSON.
    /// </summary>
    public byte ValorNumerico { get; private set; }

    /// <summary>
    /// Orden semántico ascendente del nivel. Determina cómo se listan y
    /// comparan los niveles en la UI y en queries con ORDER BY Orden.
    /// </summary>
    public int Orden { get; private set; }
}
