namespace SGV.Aplicacion.Compatibilidad;

public sealed class ServicioCompatibilidadHabilidades
{
    public ResultadoCompatibilidad Calcular(
        IEnumerable<RequisitoHabilidad> requisitos,
        IEnumerable<HabilidadPersona> habilidadesPersona)
    {
        var requisitosLista = requisitos.ToList();
        if (requisitosLista.Count == 0)
        {
            return new ResultadoCompatibilidad(100, "Total", true, []);
        }

        if (requisitosLista.Any(r => r.NivelRequerido is < 1 or > 4))
        {
            throw new ArgumentOutOfRangeException(nameof(requisitos), "Los niveles requeridos deben estar entre 1 y 4.");
        }

        if (requisitosLista.Any(r => r.Ponderacion <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(requisitos), "Las ponderaciones deben ser mayores a cero.");
        }

        var habilidadesPorId = habilidadesPersona
            .GroupBy(h => h.HabilidadId)
            .ToDictionary(g => g.Key, g => g.Max(h => h.Nivel));

        var detalles = requisitosLista.Select(requisito =>
        {
            habilidadesPorId.TryGetValue(requisito.HabilidadId, out var nivelPersona);
            byte? nivelEncontrado = nivelPersona == 0 ? null : nivelPersona;
            var puntajeNormalizado = CalcularPuntajeNormalizado(requisito.NivelRequerido, nivelEncontrado);

            return new ResultadoCompatibilidadHabilidad(
                requisito.HabilidadId,
                requisito.NivelRequerido,
                nivelEncontrado,
                requisito.Ponderacion,
                puntajeNormalizado,
                requisito.EsObligatoria,
                nivelEncontrado.HasValue && nivelEncontrado.Value >= requisito.NivelRequerido);
        }).ToList();

        var ponderacionTotal = detalles.Sum(d => d.Ponderacion);
        var puntaje = detalles.Sum(d => d.PuntajeNormalizado * d.Ponderacion) / ponderacionTotal * 100;
        var puntajeRedondeado = Math.Round(puntaje, 2, MidpointRounding.AwayFromZero);
        var cumpleObligatorias = detalles.Where(d => d.EsObligatoria).All(d => d.CumpleNivelRequerido);
        var categoria = puntajeRedondeado >= 90 && cumpleObligatorias
            ? "Total"
            : puntajeRedondeado >= 60
                ? "Parcial"
                : "Insuficiente";

        return new ResultadoCompatibilidad(puntajeRedondeado, categoria, categoria == "Total", detalles);
    }

    private static decimal CalcularPuntajeNormalizado(byte nivelRequerido, byte? nivelPersona)
    {
        if (!nivelPersona.HasValue)
        {
            return 0;
        }

        if (nivelPersona.Value >= nivelRequerido)
        {
            return 1;
        }

        return decimal.Divide(nivelPersona.Value, nivelRequerido);
    }
}
