using SGV.Dominio.Comun;
using SGV.Dominio.Vacantes;

namespace SGV.Dominio.Seleccion;

public sealed class Postulacion : EntidadAuditable
{
    private readonly List<HistorialEstadoPostulacion> _historialEstados = [];
    private readonly List<EvaluacionPostulacion> _evaluaciones = [];

    private Postulacion()
    {
    }

    public Postulacion(Guid vacanteId, Guid postulanteId, Guid estadoPostulacionId, DateTime fechaPostulacion)
    {
        VacanteId = vacanteId;
        PostulanteId = postulanteId;
        EstadoPostulacionId = estadoPostulacionId;
        FechaPostulacion = fechaPostulacion;
    }

    public Guid VacanteId { get; private set; }

    public Vacante Vacante { get; private set; } = null!;

    public Guid PostulanteId { get; private set; }

    public Postulante Postulante { get; private set; } = null!;

    public Guid EstadoPostulacionId { get; private set; }

    public EstadoPostulacion EstadoPostulacion { get; private set; } = null!;

    public DateTime FechaPostulacion { get; private set; }

    public decimal? PuntajeCompatibilidad { get; private set; }

    public string? NivelCompatibilidad { get; private set; }

    public string? Observaciones { get; private set; }

    public IReadOnlyCollection<HistorialEstadoPostulacion> HistorialEstados => _historialEstados;

    public IReadOnlyCollection<EvaluacionPostulacion> Evaluaciones => _evaluaciones;

    public void RegistrarCompatibilidad(decimal puntajeCompatibilidad, string nivelCompatibilidad)
    {
        PuntajeCompatibilidad = ValidacionesDominio.Porcentaje(puntajeCompatibilidad, nameof(PuntajeCompatibilidad));
        NivelCompatibilidad = ValidacionesDominio.Requerido(nivelCompatibilidad, nameof(NivelCompatibilidad), 50);
    }

    public HistorialEstadoPostulacion CambiarEstado(Guid estadoNuevoId, string? usuarioId, string? observaciones = null, DateTime? fecha = null)
    {
        var cambio = new HistorialEstadoPostulacion(Id, EstadoPostulacionId, estadoNuevoId, fecha ?? DateTime.UtcNow, usuarioId, observaciones);
        EstadoPostulacionId = estadoNuevoId;
        _historialEstados.Add(cambio);
        return cambio;
    }
}
