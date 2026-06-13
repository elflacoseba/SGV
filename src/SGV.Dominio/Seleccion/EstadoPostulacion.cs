using SGV.Dominio.Comun;

namespace SGV.Dominio.Seleccion;

public sealed class EstadoPostulacion : EntidadBase
{
    private EstadoPostulacion()
    {
    }

    public EstadoPostulacion(string codigo, string nombre, int orden, bool esTerminal, bool esTerminalPositivo)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 100);
        Orden = orden;
        EsTerminal = esTerminal;
        EsTerminalPositivo = esTerminalPositivo;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public int Orden { get; private set; }

    public bool EsTerminal { get; private set; }

    public bool EsTerminalPositivo { get; private set; }
}
