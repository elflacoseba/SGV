using System.Net;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Web.Integration.Organizacion;

namespace SGV.Tests.Web.Puesto;

/// <summary>
/// Fake en memoria de <see cref="IPuestosApiClient"/> compartido por las
/// pruebas web de Puestos. Decisión de diseño D2: respuestas programadas vía
/// propiedades (<c>GetAllResult</c>, <c>GetByIdResult</c>, <c>CreateResult</c>,
/// …) más captura de invocaciones (<c>GetAllCalls</c>, <c>DeleteCalls</c>, …) y
/// excepciones inyectables por método. Modela la baja lógica marcando ids
/// eliminados para que <c>GetAllAsync</c>/<c>GetByIdAsync</c> reflejen el
/// comportamiento real del backend.
/// </summary>
public sealed class FakePuestosApiClient : IPuestosApiClient
{
    private readonly HashSet<Guid> _deletedIds = new();

    // ── Respuestas programadas ──────────────────────────────────

    /// <summary>Resultado de <see cref="GetAllAsync"/> (se filtran los ids eliminados).</summary>
    public IReadOnlyList<PuestoDto> GetAllResult { get; set; } = [];

    /// <summary>Resultado de <see cref="GetByIdAsync"/> cuando no se resuelve desde <see cref="GetAllResult"/>.</summary>
    public PuestoDto? GetByIdResult { get; set; }

    /// <summary>Resultado de <see cref="DeleteAsync"/>. Por defecto, éxito 204.</summary>
    public PuestoDeleteResult DeleteResult { get; set; } = new(true, HttpStatusCode.NoContent, null, null);

    /// <summary>Resultado de <see cref="CreateAsync"/>. Por defecto, fallo NotImplemented para forzar cableado explícito.</summary>
    public PuestoCommandResult CreateResult { get; set; } = PuestoCommandResult.Failure(
        new PuestoError(PuestoErrorType.NotFound, "NotImplemented", "CreateResult no fue cableado en el fake."));

    /// <summary>Resultado de <see cref="UpdateAsync"/>. Por defecto, fallo NotImplemented para forzar cableado explícito.</summary>
    public PuestoCommandResult UpdateResult { get; set; } = PuestoCommandResult.Failure(
        new PuestoError(PuestoErrorType.NotFound, "NotImplemented", "UpdateResult no fue cableado en el fake."));

    /// <summary>Resultado de <see cref="ReactivateAsync"/>. Por defecto, fallo NotImplemented para forzar cableado explícito.</summary>
    public PuestoCommandResult ReactivateResult { get; set; } = PuestoCommandResult.Failure(
        new PuestoError(PuestoErrorType.NotFound, "NotImplemented", "ReactivateResult no fue cableado en el fake."));

    // ── Excepciones inyectables ─────────────────────────────────

    public Exception? GetAllException { get; set; }
    public Exception? GetByIdException { get; set; }
    public Exception? CreateException { get; set; }
    public Exception? UpdateException { get; set; }
    public Exception? DeleteException { get; set; }
    public Exception? ReactivateException { get; set; }

    // ── Captura de invocaciones ─────────────────────────────────

    public List<int> GetAllCalls { get; } = new();
    public List<Guid> GetByIdCalls { get; } = new();
    public List<CrearPuestoRequest> CreateCalls { get; } = new();
    public List<(Guid Id, ActualizarPuestoRequest Request)> UpdateCalls { get; } = new();
    public List<Guid> DeleteCalls { get; } = new();
    public List<Guid> ReactivateCalls { get; } = new();

    /// <summary>Construye un fake que devuelve la lista indicada en <see cref="GetAllAsync"/>.</summary>
    public static FakePuestosApiClient WithPuestoList(params PuestoDto[] puestos)
        => new() { GetAllResult = puestos };

    /// <summary>Indica si el id fue marcado como eliminado vía <see cref="DeleteAsync"/>.</summary>
    public bool IsDeleted(Guid id) => _deletedIds.Contains(id);

    public Task<IReadOnlyList<PuestoDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls.Add(1);

        if (GetAllException is not null)
        {
            throw GetAllException;
        }

        IReadOnlyList<PuestoDto> snapshot = GetAllResult;
        if (_deletedIds.Count > 0)
        {
            snapshot = snapshot.Where(p => !_deletedIds.Contains(p.Id)).ToArray();
        }

        return Task.FromResult(snapshot);
    }

    public Task<PuestoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GetByIdCalls.Add(id);

        if (GetByIdException is not null)
        {
            throw GetByIdException;
        }

        if (_deletedIds.Contains(id))
        {
            return Task.FromResult<PuestoDto?>(null);
        }

        var fromList = GetAllResult.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(fromList ?? GetByIdResult);
    }

    public Task<PuestoCommandResult> CreateAsync(CrearPuestoRequest request, CancellationToken cancellationToken = default)
    {
        CreateCalls.Add(request);

        if (CreateException is not null)
        {
            throw CreateException;
        }

        return Task.FromResult(CreateResult);
    }

    public Task<PuestoCommandResult> UpdateAsync(Guid id, ActualizarPuestoRequest request, CancellationToken cancellationToken = default)
    {
        UpdateCalls.Add((id, request));

        if (UpdateException is not null)
        {
            throw UpdateException;
        }

        return Task.FromResult(UpdateResult);
    }

    public Task<PuestoDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        DeleteCalls.Add(id);

        if (DeleteException is not null)
        {
            throw DeleteException;
        }

        if (DeleteResult.Succeeded)
        {
            _deletedIds.Add(id);
        }

        return Task.FromResult(DeleteResult);
    }

    public Task<PuestoCommandResult> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReactivateCalls.Add(id);

        if (ReactivateException is not null)
        {
            throw ReactivateException;
        }

        if (ReactivateResult.IsSuccess)
        {
            _deletedIds.Remove(id);
        }

        return Task.FromResult(ReactivateResult);
    }
}
