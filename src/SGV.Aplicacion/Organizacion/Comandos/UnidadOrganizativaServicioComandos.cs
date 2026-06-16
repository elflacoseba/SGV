using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Implements create, update, parent-change, and soft-delete use cases for organizational units.
/// </summary>
public sealed class UnidadOrganizativaServicioComandos(
    IUnidadOrganizativaRepository repository,
    ITipoUnidadOrganizativaRepository tipoUnidadRepository,
    IUnitOfWork unitOfWork) : IUnidadOrganizativaServicioComandos
{
    public async Task<UnidadOrganizativaCommandResult> CrearAsync(
        CrearUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await repository.ExistsActiveCodeAsync(request.Codigo, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado", "Ya existe una unidad organizativa activa con el mismo código."));
        }

        var tipo = await tipoUnidadRepository.GetByIdAsync(request.TipoUnidadOrganizativaId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "TipoUnidadNoExiste",
                    "El tipo de unidad organizativa referenciado no existe."));
        }

        if (request.UnidadPadreId.HasValue)
        {
            var padre = await repository.GetByIdAsync(request.UnidadPadreId.Value, cancellationToken).ConfigureAwait(false);
            if (padre is null)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.NotFound, "UnidadPadreNoEncontrada", "La unidad padre especificada no existe."));
            }
        }

        try
        {
            var unidad = new UnidadOrganizativa(request.Codigo, request.Nombre, request.TipoUnidadOrganizativaId, request.UnidadPadreId)
            {
                Id = Guid.NewGuid()
            };
            unidad.CambiarDatos(request.Codigo, request.Nombre, request.TipoUnidadOrganizativaId, request.Descripcion);
            unidad.DefinirVigencia(request.VigenteDesde, request.VigenteHasta);

            await repository.AddAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return UnidadOrganizativaCommandResult.Success(MapToDto(unidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<UnidadOrganizativaCommandResult> ActualizarAsync(
        Guid id,
        ActualizarUnidadOrganizativaRequest request,
        CancellationToken cancellationToken = default)
    {
        var unidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad organizativa no existe."));
        }

        if (await repository.ExistsActiveCodeAsync(request.Codigo, id, cancellationToken).ConfigureAwait(false))
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Conflict, "CodigoDuplicado", "Ya existe una unidad organizativa activa con el mismo código."));
        }

        var tipo = await tipoUnidadRepository.GetByIdAsync(request.TipoUnidadOrganizativaId, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "TipoUnidadNoExiste",
                    "El tipo de unidad organizativa referenciado no existe."));
        }

        try
        {
            unidad.CambiarDatos(request.Codigo, request.Nombre, request.TipoUnidadOrganizativaId, request.Descripcion);
            unidad.DefinirVigencia(request.VigenteDesde, request.VigenteHasta);

            await repository.UpdateAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return UnidadOrganizativaCommandResult.Success(MapToDto(unidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<UnidadOrganizativaCommandResult> CambiarUnidadPadreAsync(
        Guid id,
        CambiarUnidadPadreRequest request,
        CancellationToken cancellationToken = default)
    {
        var unidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad organizativa no existe."));
        }

        if (request.UnidadPadreId == id)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "CicloJerarquico", "Una unidad organizativa no puede ser padre de sí misma."));
        }

        if (request.UnidadPadreId.HasValue)
        {
            var padre = await repository.GetByIdAsync(request.UnidadPadreId.Value, cancellationToken).ConfigureAwait(false);
            if (padre is null)
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.NotFound, "UnidadPadreNoEncontrada", "La unidad padre especificada no existe."));
            }

            if (await repository.IsDescendantAsync(request.UnidadPadreId.Value, id, cancellationToken).ConfigureAwait(false))
            {
                return UnidadOrganizativaCommandResult.Failure(
                    new(UnidadOrganizativaErrorType.Conflict, "CicloJerarquico", "No se puede asignar como padre una unidad descendiente."));
            }
        }

        try
        {
            unidad.CambiarUnidadPadre(request.UnidadPadreId);

            await repository.UpdateAsync(unidad, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return UnidadOrganizativaCommandResult.Success(MapToDto(unidad));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.Validation, "CicloJerarquico", ex.Message));
        }
    }

    public async Task<UnidadOrganizativaCommandResult> EliminarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var unidad = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (unidad is null)
        {
            return UnidadOrganizativaCommandResult.Failure(
                new(UnidadOrganizativaErrorType.NotFound, "UnidadNoEncontrada", "La unidad organizativa no existe."));
        }

        unidad.Desactivar();
        await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UnidadOrganizativaCommandResult.Success(null!);
    }

    private static UnidadOrganizativaDto MapToDto(UnidadOrganizativa unidad)
    {
        return new UnidadOrganizativaDto(
            unidad.Id,
            unidad.Codigo,
            unidad.Nombre,
            unidad.TipoUnidadOrganizativaId,
            unidad.TipoUnidadOrganizativa?.Nombre ?? string.Empty,
            unidad.Descripcion,
            unidad.VigenteDesde,
            unidad.VigenteHasta,
            unidad.UnidadPadreId);
    }
}
