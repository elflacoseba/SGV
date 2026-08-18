using FluentValidation;
using SGV.Aplicacion.Comun.Persistencia;
using SGV.Aplicacion.Common;
using SGV.Aplicacion.Ocupaciones.Consultas;
using SGV.Aplicacion.Organizacion.Consultas;
using SGV.Aplicacion.Organizacion.Comandos.Validaciones;
using SGV.Contracts.Comun;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Dominio.Organizacion;

namespace SGV.Aplicacion.Organizacion.Comandos;

/// <summary>
/// Implements create, update, soft-delete, and reactivate use cases for Puesto.
/// </summary>
public sealed class PuestoServicioComandos(
    IPuestoRepository repository,
    IUnidadOrganizativaRepository unidadOrganizativaRepository,
    ICargoRepository cargoRepository,
    IUnitOfWork unitOfWork,
    IValidator<CrearPuestoRequest> crearValidator,
    IValidator<ActualizarPuestoRequest> actualizarValidator,
    IOcupacionRepository ocupacionRepository) : IPuestoServicioComandos
{
    private static IReadOnlyDictionary<string, string[]> BuildFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
        => ValidationHelper.BuildFieldErrors(failures);

    /// <summary>
    /// Convenience constructor for backward compatibility (e.g., tests
    /// that NO necesitan la guarda contra ocupaciones vigentes, como
    /// <c>PuestoWebTestFixture</c>). Inyecta un null-object que reporta
    /// cero ocupaciones activas y nunca bloquea la baja (DEC-2).
    /// </summary>
    public PuestoServicioComandos(
        IPuestoRepository repository,
        IUnidadOrganizativaRepository unidadOrganizativaRepository,
        ICargoRepository cargoRepository,
        IUnitOfWork unitOfWork)
        : this(repository, unidadOrganizativaRepository, cargoRepository, unitOfWork,
               new CrearPuestoRequestValidator(),
               new ActualizarPuestoRequestValidator(),
               new NullOcupacionRepository())
    {
    }

    /// <summary>
    /// Null-object de <see cref="IOcupacionRepository"/> usado por el ctor
    /// legacy. Reporta cero ocupaciones activas (siempre <c>false</c>) y
    /// mantiene compat con fixtures/tests que no necesitan la guarda.
    /// </summary>
    private sealed class NullOcupacionRepository : IOcupacionRepository
    {
        public Task AddAsync(global::SGV.Dominio.Ocupaciones.Ocupacion ocupacion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("NullOcupacionRepository: AddAsync no soportado.");

        public Task<global::SGV.Dominio.Ocupaciones.Ocupacion?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("NullOcupacionRepository: GetByIdForUpdateAsync no soportado.");

        public Task<global::SGV.Dominio.Ocupaciones.Ocupacion?> GetByIdIncludingHistoryAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("NullOcupacionRepository: GetByIdIncludingHistoryAsync no soportado.");

        public Task UpdateAsync(global::SGV.Dominio.Ocupaciones.Ocupacion ocupacion, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("NullOcupacionRepository: UpdateAsync no soportado.");

        public Task<IReadOnlyList<global::SGV.Dominio.Ocupaciones.Ocupacion>> ListAllIncludingHistoryAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("NullOcupacionRepository: ListAllIncludingHistoryAsync no soportado.");

        public Task<(IReadOnlyList<global::SGV.Dominio.Ocupaciones.Ocupacion> Items, int TotalCount)> QueryAsync(
            global::SGV.Contracts.Ocupaciones.Consultas.OcupacionListQuery query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("NullOcupacionRepository: QueryAsync no soportado.");

        public Task<bool> ExistsActiveByPuestoAsync(Guid puestoId, Guid? excludingId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> ExistsActiveByPersonaYPuestoAsync(Guid personaId, Guid puestoId, Guid? excludingId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        // T1.9 / REQ-OCC-FORM-010 (invertir-flujo-cubrir): el null-object
        // reporta cero cobertura derivada por Vacante; PuestoServicioComandos
        // nunca invoca estos métodos, pero la firma debe existir.
        public Task<bool> ExistsActiveByVacanteAsync(Guid vacanteId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<(Guid Id, string PersonaNombre)?> ObtenerVigentePorVacanteAsync(
            Guid vacanteId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(Guid Id, string PersonaNombre)?>(null);

        public Task<global::SGV.Dominio.Ocupaciones.Ocupacion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("NullOcupacionRepository: GetByIdAsync no soportado.");

        public Task<IReadOnlyList<global::SGV.Dominio.Ocupaciones.Ocupacion>> ListAllAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException("NullOcupacionRepository: ListAllAsync no soportado.");
    }

    public async Task<PuestoCommandResult> CrearAsync(
        CrearPuestoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await crearValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        if (await repository.ExistsActiveCodeAsync(request.Codigo, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Conflict, "CodigoDuplicado", "Ya existe un puesto activo con el mismo código."));
        }

        var unidad = await unidadOrganizativaRepository
            .GetByIdAsync(request.UnidadOrganizativaId, cancellationToken)
            .ConfigureAwait(false);
        if (unidad is null)
        {
            // El contrato de IReadOnlyRepository.GetByIdAsync filtra por
            // IsActive && !IsDeleted en el `Query` base, por lo que este
            // branch cubre tanto "no existe" como "existe pero está
            // inactivo/eliminado". Si el contrato del repo cambia para
            // incluir soft-deleted, este guard seguirá siendo correcto y
            // la FK con OnDelete=Restrict evitaría guardar un Puesto con
            // Cargo/Unidad inactiva. Test de regresión:
            // PuestoServicioComandosTests.CrearAsync_CargoInactivo_RetornaValidation.
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "UnidadOrganizativaNoExiste",
                    "La unidad organizativa referenciada no existe o no está activa."));
        }

        var cargo = await cargoRepository
            .GetByIdAsync(request.CargoId, cancellationToken)
            .ConfigureAwait(false);
        if (cargo is null)
        {
            // Mismo contrato que la unidad: cubre "no existe" + "inactivo".
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "CargoNoExiste",
                    "El cargo referenciado no existe o no está activo."));
        }

        if (request.PuestoSuperiorId.HasValue)
        {
            var superiorError = await ValidarPuestoSuperiorAsync(
                request.PuestoSuperiorId.Value, cancellationToken).ConfigureAwait(false);
            if (superiorError is not null)
            {
                return superiorError;
            }
        }

        try
        {
            var puesto = new Puesto(
                request.UnidadOrganizativaId,
                request.CargoId,
                request.Codigo,
                request.Nombre,
                request.PuestoSuperiorId,
                request.Descripcion)
            {
                Id = Guid.NewGuid()
            };

            await repository.AddAsync(puesto, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PuestoCommandResult.Success(MapToDto(puesto, unidad.Nombre, cargo.Nombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<PuestoCommandResult> ActualizarAsync(
        Guid id,
        ActualizarPuestoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await actualizarValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DatosInvalidos", "Uno o más campos contienen errores de validación."),
                BuildFieldErrors(validationResult.Errors));
        }

        var puesto = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe."));
        }

        if (request.PuestoSuperiorId.HasValue)
        {
            if (request.PuestoSuperiorId.Value == id)
            {
                return PuestoCommandResult.Failure(
                    new(PuestoErrorType.Validation, "PuestoSuperiorInvalido",
                        "Un puesto no puede ser superior de sí mismo."));
            }

            var superiorError = await ValidarPuestoSuperiorAsync(
                request.PuestoSuperiorId.Value, cancellationToken).ConfigureAwait(false);
            if (superiorError is not null)
            {
                return superiorError;
            }
        }

        try
        {
            puesto.Actualizar(request.Nombre, request.Descripcion, request.PuestoSuperiorId);

            await repository.UpdateAsync(puesto, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PuestoCommandResult.Success(MapToDto(puesto, puesto.UnidadOrganizativa?.Nombre, puesto.Cargo?.Nombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DatosInvalidos", ex.Message));
        }
    }

    public async Task<PuestoCommandResult> DesactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var puesto = await repository.GetByIdForUpdateAsync(id, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe."));
        }

        // ===========================================================================
        // Guarda contra ocupaciones vigentes (REQ-PTO-010 / DEC-3).
        // Si el puesto tiene ocupaciones activas, NO se muta y se devuelve
        // un Conflict con código estable `PuestoConOcupacionesActivas`. El
        // parámetro `Categoria = ErrorCategoria.Conflict` es CRÍTICO: el
        // default sería `Unexpected` y `ApiResults.MapCategoria` mapearía
        // eso a HTTP 500. Sin esta línea, la guarda funcionaría pero la
        // API respondería 500 en lugar de 409.
        // ===========================================================================
        if (await ocupacionRepository.ExistsActiveByPuestoAsync(id, null, cancellationToken).ConfigureAwait(false))
        {
            return PuestoCommandResult.Failure(
                new PuestoError(
                    PuestoErrorType.Conflict,
                    "PuestoConOcupacionesActivas",
                    "El puesto tiene ocupaciones vigentes y no puede darse de baja.",
                    null,
                    ErrorCategoria.Conflict));
        }

        try
        {
            puesto.Desactivar();
            await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PuestoCommandResult.Success(null!);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "DesactivacionInvalida", ex.Message));
        }
    }

    public async Task<PuestoCommandResult> ReactivarAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var puesto = await repository.GetByIdIncludingDeletedAsync(id, cancellationToken).ConfigureAwait(false);
        if (puesto is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.NotFound, "PuestoNoEncontrado", "El puesto no existe."));
        }

        if (await repository.ExistsActiveCodeAsync(puesto.Codigo, id, cancellationToken).ConfigureAwait(false))
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Conflict, "CodigoDuplicado",
                    "Ya existe un puesto activo con el mismo código."));
        }

        try
        {
            puesto.Activar();
            await repository.ReactivateAsync(id, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return PuestoCommandResult.Success(MapToDto(puesto, puesto.UnidadOrganizativa?.Nombre, puesto.Cargo?.Nombre));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "ReactivacionInvalida", ex.Message));
        }
    }

    private async Task<PuestoCommandResult?> ValidarPuestoSuperiorAsync(
        Guid puestoSuperiorId,
        CancellationToken cancellationToken)
    {
        var superior = await repository
            .GetByIdAsync(puestoSuperiorId, cancellationToken)
            .ConfigureAwait(false);

        if (superior is null)
        {
            return PuestoCommandResult.Failure(
                new(PuestoErrorType.Validation, "PuestoSuperiorNoExiste",
                    "El puesto superior referenciado no existe o no está activo."));
        }

        return null;
    }

    private static PuestoDto MapToDto(Puesto puesto, string? unidadNombre, string? cargoNombre)
    {
        return new PuestoDto(
            puesto.Id,
            puesto.Codigo,
            puesto.Nombre,
            puesto.Descripcion,
            puesto.UnidadOrganizativaId,
            unidadNombre ?? string.Empty,
            puesto.CargoId,
            cargoNombre ?? string.Empty,
            puesto.PuestoSuperiorId);
    }
}
