using SGV.Dominio.Comun;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Seleccion;

namespace SGV.Dominio.Vacantes;

public sealed class Vacante : EntidadAuditable
{
    private readonly List<HistorialEstadoVacante> _historialEstados = [];
    private readonly List<Postulacion> _postulaciones = [];

    private Vacante()
    {
    }

    public Vacante(Guid puestoId, Guid estadoVacanteId, DateTime fechaApertura, string motivo)
    {
        PuestoId = puestoId;
        EstadoVacanteId = estadoVacanteId;
        FechaApertura = fechaApertura;
        Motivo = ValidacionesDominio.Requerido(motivo, nameof(Motivo), 500);
    }

    public Guid PuestoId { get; private set; }

    public Puesto Puesto { get; private set; } = null!;

    public Guid EstadoVacanteId { get; private set; }

    public EstadoVacante EstadoVacante { get; private set; } = null!;

    public DateTime FechaApertura { get; private set; }

    public DateTime? FechaCierre { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public string? Observaciones { get; private set; }

    public IReadOnlyCollection<HistorialEstadoVacante> HistorialEstados => _historialEstados;

    public IReadOnlyCollection<Postulacion> Postulaciones => _postulaciones;

    public HistorialEstadoVacante CambiarEstado(Guid estadoNuevoId, string? usuarioId, string? motivo = null, DateTime? fecha = null, bool cerrar = false)
    {
        var cambio = new HistorialEstadoVacante(Id, EstadoVacanteId, estadoNuevoId, fecha ?? DateTime.UtcNow, usuarioId, motivo);
        EstadoVacanteId = estadoNuevoId;
        if (cerrar)
        {
            FechaCierre = cambio.ChangedAt;
        }

        _historialEstados.Add(cambio);
        return cambio;
    }
}
