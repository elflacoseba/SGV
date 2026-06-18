using SGV.Dominio.Comun;

namespace SGV.Dominio.Organizacion;

public sealed class NivelCargo : EntidadBase
{
    private NivelCargo()
    {
    }

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

    public byte ValorNumerico { get; private set; }

    public int Orden { get; private set; }
}
