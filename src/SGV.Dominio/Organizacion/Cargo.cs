using SGV.Dominio.Comun;
using SGV.Dominio.Habilidades;

namespace SGV.Dominio.Organizacion;

public sealed class Cargo : EntidadAuditable
{
    private readonly List<CargoHabilidad> _habilidades = [];
    private readonly List<Puesto> _puestos = [];

    private Cargo()
    {
    }

    public Cargo(string codigo, string nombre, string? nivel = null, string? descripcion = null)
    {
        CambiarDatos(codigo, nombre, nivel, descripcion);
        IsActive = true;
    }

    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    public string? Descripcion { get; private set; }

    public string? Nivel { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<CargoHabilidad> Habilidades => _habilidades;

    public IReadOnlyCollection<Puesto> Puestos => _puestos;

    public void CambiarDatos(string codigo, string nombre, string? nivel = null, string? descripcion = null)
    {
        Codigo = ValidacionesDominio.Requerido(codigo, nameof(Codigo), 50);
        Nombre = ValidacionesDominio.Requerido(nombre, nameof(Nombre), 200);
        Nivel = ValidacionesDominio.Opcional(nivel, nameof(Nivel), 50);
        Descripcion = ValidacionesDominio.Opcional(descripcion, nameof(Descripcion), 1000);
    }

    public CargoHabilidad AgregarHabilidad(Guid habilidadId, Guid nivelRequeridoId, decimal ponderacion, bool esObligatoria)
    {
        if (_habilidades.Any(h => h.HabilidadId == habilidadId))
        {
            throw new InvalidOperationException("La habilidad ya está configurada para el cargo.");
        }

        var cargoHabilidad = new CargoHabilidad(Id, habilidadId, nivelRequeridoId, ponderacion, esObligatoria);
        _habilidades.Add(cargoHabilidad);
        return cargoHabilidad;
    }
}
