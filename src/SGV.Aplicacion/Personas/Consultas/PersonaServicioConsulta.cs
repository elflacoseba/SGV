using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Dominio.Personas;

namespace SGV.Aplicacion.Personas.Consultas;

/// <summary>
/// Read-only query service for Personas.
/// Issue #147 PR2: además de mapear la persona, inyecta
/// <see cref="ITipoDocumentoCatalogoConsulta"/> y proyecta
/// <c>TipoDocumentoCodigo</c> / <c>TipoDocumentoNombre</c> desde el catálogo
/// para que el consumidor (web/API) no tenga que hacer una segunda request.
/// </summary>
public sealed class PersonaServicioConsulta : IPersonaServicioConsulta
{
    private readonly IPersonaRepository _repository;
    private readonly ITipoDocumentoCatalogoConsulta _tipoDocumentoCatalogo;

    /// <summary>
    /// Constructor primario: inyección del repositorio y del catálogo de
    /// TiposDocumento para la proyección denormalizada del JOIN.
    /// </summary>
    public PersonaServicioConsulta(
        IPersonaRepository repository,
        ITipoDocumentoCatalogoConsulta tipoDocumentoCatalogo)
    {
        _repository = repository;
        _tipoDocumentoCatalogo = tipoDocumentoCatalogo;
    }

    /// <summary>
    /// Constructor de back-compat (1 argumento): los tests unitarios que
    /// sólo necesitan verificar paginación/sort/segmento pueden usar este
    /// ctor sin mockear el catálogo. La denormalización queda inerte
    /// (TipoDocumentoCodigo/Nombre siempre null).
    /// </summary>
    public PersonaServicioConsulta(IPersonaRepository repository)
        : this(repository, new EmptyTipoDocumentoCatalogoConsulta())
    {
    }

    public async Task<IReadOnlyList<PersonaDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Cargar el catálogo una sola vez por request para evitar N+1.
        var tipoLookup = await TipoDocumentoLookupBuilder
            .BuildAsync(_tipoDocumentoCatalogo, cancellationToken)
            .ConfigureAwait(false);

        var entities = await _repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        return entities.Select(p => MapToDto(p, tipoLookup)).ToList();
    }

    public async Task<PersonaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }
        var tipoLookup = await TipoDocumentoLookupBuilder
            .BuildAsync(_tipoDocumentoCatalogo, cancellationToken)
            .ConfigureAwait(false);
        return MapToDto(entity, tipoLookup);
    }

    public async Task<PersonaListadoDto> ListarAsync(
        PersonaListQuery query,
        CancellationToken cancellationToken = default)
    {
        // Cargar el catálogo una sola vez por request.
        var tipoLookup = await TipoDocumentoLookupBuilder
            .BuildAsync(_tipoDocumentoCatalogo, cancellationToken)
            .ConfigureAwait(false);

        var (items, totalCount) = await _repository.QueryAsync(
            query.Search,
            query.Page,
            query.PageSize,
            query.Sort,
            query.Segmento,
            cancellationToken,
            query.SoloSinUsuario).ConfigureAwait(false);

        return new PersonaListadoDto(
            items.Select(p => MapToDto(p, tipoLookup)).ToList(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    private static PersonaDto MapToDto(
        Persona entity,
        IReadOnlyDictionary<Guid, TipoDocumentoDto> tipoLookup)
    {
        string? tipoCodigo = null;
        string? tipoNombre = null;
        if (entity.TipoDocumentoId.HasValue
            && tipoLookup.TryGetValue(entity.TipoDocumentoId.Value, out var tipo))
        {
            tipoCodigo = tipo.Codigo;
            tipoNombre = tipo.Nombre;
        }

        return new PersonaDto(
            entity.Id,
            entity.Legajo,
            entity.Nombres,
            entity.Apellidos,
            entity.Email,
            entity.TipoDocumentoId,
            tipoCodigo,
            tipoNombre,
            entity.NumeroDocumento,
            entity.Telefono,
            entity.IsActive
        );
    }

    /// <summary>
    /// Stub vacío para el constructor de back-compat. NO se usa en producción
    /// porque el DI siempre inyecta el impl real
    /// (<see cref="SGV.Aplicacion.Personas.Consultas.TipoDocumentoCatalogoConsulta"/>).
    /// Vive en este archivo para no contaminar SGV.Infraestructura con
    /// tipos de test.
    /// </summary>
    private sealed class EmptyTipoDocumentoCatalogoConsulta : ITipoDocumentoCatalogoConsulta
    {
        public Task<IReadOnlyList<TipoDocumentoDto>> ListarAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TipoDocumentoDto>>([]);

        public Task<TipoDocumentoDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<TipoDocumentoDto?>(null);
    }
}
