using SGV.Dominio.Comun;

namespace SGV.Dominio.Vacantes;

public sealed record class EstadoVacante : EntidadBase
{
    private EstadoVacante()
    {
    }

    public EstadoVacante(
        string codigo,
        string nombre,
        int orden,
        bool esTerminal,
        bool esCubierta = false,
        bool esCancelada = false)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 100);
        Orden = orden;
        EsTerminal = esTerminal;
        EsCubierta = esCubierta;
        EsCancelada = esCancelada;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public int Orden { get; private set; }

    public bool EsTerminal { get; private set; }

    public bool EsCubierta { get; private set; }

    public bool EsCancelada { get; private set; }
}
