using System.Net;
using SGV.Contracts.Habilidades.Comandos;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Web.Integration.Habilidades;
using HabilidadListQuery = SGV.Web.Integration.Habilidades.HabilidadListQuery;

namespace SGV.Tests.Web.Habilidad;

/// <summary>
/// Fake en memoria de <see cref="IHabilidadApiClient"/> compartido por las
/// pruebas web de Habilidades. Permite configurar resultados de cada método,
/// forzar excepciones y registrar invocaciones.
/// </summary>
public sealed class FakeHabilidadApiClient : IHabilidadApiClient
{
    private readonly IReadOnlyList<HabilidadDto>? _getAllResult;
    private readonly HashSet<Guid> _deletedIds = new();

    public FakeHabilidadApiClient()
        : this(Array.Empty<HabilidadDto>())
    {
    }

    private FakeHabilidadApiClient(IReadOnlyList<HabilidadDto>? getAllResult)
    {
        _getAllResult = getAllResult;
    }

    public List<int> GetAllCalls { get; } = new();

    public List<Guid> DeleteCalls { get; } = new();

    public HabilidadDeleteResult DeleteResult { get; set; } = new(true, HttpStatusCode.NoContent, null, null);

    public HabilidadCommandResult CreateResult { get; set; } = HabilidadCommandResult.Failure(
        new HabilidadError(HabilidadErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    public List<CrearHabilidadRequest> CreateCalls { get; } = new();

    public Exception? CreateException { get; set; }

    public HabilidadCommandResult UpdateResult { get; set; } = HabilidadCommandResult.Failure(
        new HabilidadError(HabilidadErrorType.NotFound, "NotImplemented", "Not yet implemented"));

    public List<(Guid Id, ActualizarHabilidadRequest Request)> UpdateCalls { get; } = new();

    public Exception? UpdateException { get; set; }

    public IReadOnlyList<NivelHabilidadDto> NivelesResult { get; set; } = [];

    public Exception? NivelesException { get; set; }

    public int NivelesCalls { get; private set; }

    public Func<HabilidadListQuery, PagedResult<HabilidadDto>>? QueryHandler { get; set; }

    public List<HabilidadListQuery> QueryCalls { get; } = new();

    public Exception? QueryException { get; set; }

    /// <summary>
    /// Handler opcional para <see cref="GetByIdAsync"/>. Si está seteado,
    /// tiene prioridad sobre el comportamiento seed-default.
    /// </summary>
    public Func<Guid, HabilidadDto?>? GetByIdHandler { get; set; }

    /// <summary>
    /// Excepción opcional que <see cref="GetByIdAsync"/> debe lanzar
    /// (simula una falla de transporte contra el subrecurso).
    /// </summary>
    public Exception? GetByIdException { get; set; }

    public HabilidadCommandResult ReactivateResult { get; set; } = HabilidadCommandResult.Success(
        new HabilidadDto(Guid.NewGuid(), "PROG", "Programación", null, null, "Técnica"));

    public List<Guid> ReactivateCalls { get; } = new();

    public Exception? ReactivateException { get; set; }

    /// <summary>
    /// Resultado configurable del subrecurso <c>GET /api/v1/skills/{skillId}/cargos</c>.
    /// Si no se setea, devuelve una página vacía (paridad con
    /// <see cref="QueryAsync"/>).
    /// </summary>
    public Func<Guid, HabilidadCargosListQuery, PagedResult<SkillCargoDetailDto>>? GetCargosHandler { get; set; }

    public List<(Guid SkillId, HabilidadCargosListQuery Query)> GetCargosCalls { get; } = new();

    public Exception? GetCargosException { get; set; }

    public PagedResult<SkillCargoDetailDto> GetCargosResult { get; set; } =
        new(Array.Empty<SkillCargoDetailDto>(), 0, 1, 20);

    // PR agrega-navegacion-personas-habilidades / PR C — frontend subreverso.
    // Seed determinista del subrecurso /api/v1/skills/{skillId}/personas.
    // Paridad con el patrón de GetCargosHandler/GetCargosResult.
    private readonly Dictionary<Guid, IReadOnlyList<SkillPersonaDetailDto>> _personasActivasSeed = new();
    private readonly Dictionary<Guid, IReadOnlyList<SkillPersonaDetailDto>> _personasEliminadasSeed = new();

    public Func<Guid, HabilidadPersonasListQuery, PersonaHabilidadesPageResult>? GetPersonasHandler { get; set; }

    public List<(Guid SkillId, HabilidadPersonasListQuery Query)> GetPersonasCalls { get; } = new();

    public Exception? GetPersonasException { get; set; }

    public PersonaHabilidadesPageResult GetPersonasResult { get; set; } =
        new(Array.Empty<SkillPersonaDetailDto>(), Page: 1, PageSize: 20, Total: 0, Sort: null, Segmento: PersonaSegmentoListado.Activas);

    /// <summary>
    /// Registra el seed determinista de personas activas asociadas a
    /// <paramref name="skillId"/>. Cada <see cref="PersonaDto"/> se mapea
    /// a un <see cref="SkillPersonaDetailDto"/> compartiendo
    /// <paramref name="nivel"/> como nivel de la asociación. Para sembrar
    /// el segmento "eliminadas" usar <see cref="SeedPersonasEliminadas"/>.
    /// </summary>
    public void GetPersonasSeed(Guid skillId, IEnumerable<PersonaDto> personas, NivelHabilidadDto nivel)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(nivel);

        _personasActivasSeed[skillId] = personas
            .Select(p => new SkillPersonaDetailDto(p, nivel)
            {
                PersonaId = p.Id,
                HabilidadId = skillId,
                NivelHabilidadId = nivel.Id,
            })
            .ToArray();
    }

    /// <summary>
    /// Registra el seed determinista de personas soft-deleted asociadas a
    /// <paramref name="skillId"/> para el segmento Eliminadas. Complementa
    /// <see cref="GetPersonasSeed"/> y respeta la separación del segmento.
    /// </summary>
    public void SeedPersonasEliminadas(Guid skillId, IEnumerable<PersonaDto> personas, NivelHabilidadDto nivel)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentNullException.ThrowIfNull(nivel);

        _personasEliminadasSeed[skillId] = personas
            .Select(p => new SkillPersonaDetailDto(p, nivel)
            {
                PersonaId = p.Id,
                HabilidadId = skillId,
                NivelHabilidadId = nivel.Id,
            })
            .ToArray();
    }

    public static FakeHabilidadApiClient WithHabilidadList(params HabilidadDto[] habilidades)
        => new(habilidades.Length == 0 ? null : habilidades);

    public Task<IReadOnlyList<HabilidadDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls.Add(1);

        IReadOnlyList<HabilidadDto> snapshot = _getAllResult ?? [];
        if (_deletedIds.Count > 0)
        {
            snapshot = snapshot.Where(h => !_deletedIds.Contains(h.Id)).ToArray();
        }

        return Task.FromResult(snapshot);
    }

    public Task<HabilidadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (GetByIdException is not null)
        {
            throw GetByIdException;
        }

        if (GetByIdHandler is not null)
        {
            return Task.FromResult(GetByIdHandler(id));
        }

        if (_getAllResult is null)
            return Task.FromResult<HabilidadDto?>(null);

        if (_deletedIds.Contains(id))
            return Task.FromResult<HabilidadDto?>(null);

        return Task.FromResult(_getAllResult.FirstOrDefault(c => c.Id == id));
    }

    public Task<HabilidadDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);

        if (DeleteResult.Succeeded)
        {
            _deletedIds.Add(id);
        }

        return Task.FromResult(DeleteResult);
    }

    /// <summary>
    /// Indica si el identificador fue marcado como eliminado en este fake
    /// (vía <see cref="DeleteAsync"/>). Útil para tests que necesitan
    /// sembrar bajas lógicas sin tener que invocar el handler HTTP.
    /// </summary>
    public bool IsDeleted(Guid id) => _deletedIds.Contains(id);

    public Task<HabilidadCommandResult> CreateAsync(CrearHabilidadRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return Task.FromResult(CreateResult);
    }

    public Task<HabilidadCommandResult> UpdateAsync(Guid id, ActualizarHabilidadRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((id, request));

        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<IReadOnlyList<NivelHabilidadDto>> GetNivelesHabilidadAsync(CancellationToken cancellationToken = default)
    {
        NivelesCalls++;

        if (NivelesException is not null)
        {
            throw NivelesException;
        }

        return Task.FromResult(NivelesResult);
    }

    public Task<PagedResult<HabilidadDto>> QueryAsync(HabilidadListQuery query, CancellationToken cancellationToken = default)
    {
        QueryCalls.Add(query);

        if (QueryException is not null)
        {
            throw QueryException;
        }

        if (QueryHandler is not null)
        {
            return Task.FromResult(QueryHandler(query));
        }

        // Comportamiento server-side simulado (paridad con FakeCargoApiClient):
        // filtro por segmento (Status) + búsqueda (case-insensitive) + sort + paginación.
        var source = (_getAllResult ?? Array.Empty<HabilidadDto>()).ToList();
        var snapshot = ApplyStatusFilter(source, query.Status);

        var lowered = query.Search?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(lowered))
        {
            snapshot = snapshot
                .Where(h => h.Codigo.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || h.Nombre.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                         || (h.CategoriaNombre?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false)
                         || (h.Descripcion?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        snapshot = ApplySort(snapshot, query.Sort);

        var total = snapshot.Count;
        var pageItems = snapshot
            .Skip(Math.Max(0, (query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToList();

        return Task.FromResult(new PagedResult<HabilidadDto>(pageItems, total, query.Page, query.PageSize));
    }

    private List<HabilidadDto> ApplyStatusFilter(List<HabilidadDto> source, string? status)
    {
        // Status = "eliminadas" (case-insensitive) → sólo registros en _deletedIds.
        // Status = "activas" (case-insensitive) o null → snapshot activo, idéntico
        // a GetAllAsync (excluye _deletedIds).
        if (string.Equals(status, "eliminadas", StringComparison.OrdinalIgnoreCase))
        {
            return source.Where(h => _deletedIds.Contains(h.Id)).ToList();
        }

        return source.Where(h => !_deletedIds.Contains(h.Id)).ToList();
    }

    private static List<HabilidadDto> ApplySort(List<HabilidadDto> source, string? sort) =>
        sort?.ToLowerInvariant() switch
        {
            "codigo_desc" => source.OrderByDescending(static h => h.Codigo, StringComparer.OrdinalIgnoreCase).ToList(),
            "codigo_asc" => source.OrderBy(static h => h.Codigo, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombre_desc" => source.OrderByDescending(static h => h.Nombre, StringComparer.OrdinalIgnoreCase).ToList(),
            "nombre_asc" => source.OrderBy(static h => h.Nombre, StringComparer.OrdinalIgnoreCase).ToList(),
            "categoria_desc" => source.OrderByDescending(static h => h.CategoriaNombre ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            "categoria_asc" => source.OrderBy(static h => h.CategoriaNombre ?? string.Empty, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => source.OrderBy(static h => h.Codigo, StringComparer.OrdinalIgnoreCase).ToList()
        };

    public Task<HabilidadCommandResult> ReactivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCalls.Add(id);

        if (ReactivateException is not null)
        {
            throw ReactivateException;
        }

        return Task.FromResult(ReactivateResult);
    }

    public Task<PagedResult<SkillCargoDetailDto>> GetCargosAsync(
        Guid skillId,
        HabilidadCargosListQuery query,
        CancellationToken cancellationToken = default)
    {
        GetCargosCalls.Add((skillId, query));

        if (GetCargosException is not null)
        {
            throw GetCargosException;
        }

        if (GetCargosHandler is not null)
        {
            return Task.FromResult(GetCargosHandler(skillId, query));
        }

        return Task.FromResult(GetCargosResult);
    }

    // PR agrega-navegacion-personas-habilidades / PR C — stub mínimo para
    // satisfacer el contrato IHabilidadApiClient.GetPersonasAsync. C.4 lo
    // reemplaza por la implementación con seed determinista + handlers.
    public Task<PersonaHabilidadesPageResult> GetPersonasAsync(
        Guid skillId,
        HabilidadPersonasListQuery query,
        CancellationToken cancellationToken = default)
    {
        GetPersonasCalls.Add((skillId, query));

        if (GetPersonasException is not null)
        {
            throw GetPersonasException;
        }

        if (GetPersonasHandler is not null)
        {
            return Task.FromResult(GetPersonasHandler(skillId, query));
        }

        // Comportamiento server-side simulado: lookup por segmento
        // (activas|eliminadas), búsqueda case-insensitive sobre
        // legajo/apellidos/nombres, paginación Skip/Take.
        var source = (query.Segmento == PersonaSegmentoListado.Eliminadas
            ? _personasEliminadasSeed
            : _personasActivasSeed).TryGetValue(skillId, out var seed)
                ? seed.ToArray()
                : Array.Empty<SkillPersonaDetailDto>();

        var lowered = query.Search?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(lowered))
        {
            source = source
                .Where(item =>
                    (item.Persona.Legajo?.Contains(lowered, StringComparison.OrdinalIgnoreCase) ?? false)
                    || item.Persona.Nombres.Contains(lowered, StringComparison.OrdinalIgnoreCase)
                    || item.Persona.Apellidos.Contains(lowered, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var total = source.Length;
        var pageItems = source
            .Skip(Math.Max(0, (query.Page - 1) * query.PageSize))
            .Take(query.PageSize)
            .ToArray();

        return Task.FromResult(new PersonaHabilidadesPageResult(
            pageItems,
            Page: query.Page,
            PageSize: query.PageSize,
            Total: total,
            Sort: query.Sort,
            Segmento: query.Segmento));
    }
}