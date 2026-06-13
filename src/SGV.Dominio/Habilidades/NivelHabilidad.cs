using SGV.Dominio.Comun;

namespace SGV.Dominio.Habilidades;

public sealed class NivelHabilidad : EntidadBase
{
    private NivelHabilidad()
    {
    }

    public NivelHabilidad(string codigo, string nombre, byte valorNumerico, int orden)
    {
        if (valorNumerico is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(valorNumerico), "El nivel debe estar entre 1 y 4.");
        }

        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 100);
        ValorNumerico = valorNumerico;
        Orden = orden;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public byte ValorNumerico { get; private set; }

    public int Orden { get; private set; }
}
