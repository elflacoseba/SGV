using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Aplicacion.Habilidades.Consultas.Dtos;
using SGV.Aplicacion.Organizacion.Comandos;
using SGV.Aplicacion.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Organizacion;
using Xunit;
using CargoListQuery = SGV.Web.Integration.Organizacion.CargoListQuery;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Cargo;

/// <summary>
/// Unit tests for the typed <see cref="CargoApiClient"/>.
/// Covers HTTP translation, request paths, and the mapping of status codes
/// (including ProblemDetails parsing) to <see cref="CargoDeleteResult"/>.
/// </summary>
public class CargoApiClientTests
{
    [Fact]
    public async Task GetAllAsync_Http200WithPayload_ReturnsParsedDtosAndHitsListRoute()
    {
        var id = Guid.NewGuid();
        var payload = new[] { new CargoDto(id, "C-001", "Analista", null, Guid.NewGuid()) };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetAllAsync();

        Assert.Single(result);
        Assert.Equal(id, result[0].Id);
        Assert.Equal("Analista", result[0].Nombre);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/cargos", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http200_ReturnsDtoAndHitsDetailRoute()
    {
        var id = Guid.NewGuid();
        var payload = new CargoDto(id, "C-002", "Líder", "Desc", Guid.NewGuid());
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("Líder", result!.Nombre);
        Assert.Equal($"/api/v1/cargos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdAsync_Http404_ReturnsNullWithoutThrowing()
    {
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_Http204_ReturnsSuccessAndHitsDeleteRoute()
    {
        var id = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DeleteAsync_Http404WithProblemDetails_ReturnsFailedResultWithTitleAndDetail()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails { Title = "NotFound", Detail = "Cargo no disponible", Status = 404 };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("NotFound", result.Code);
        Assert.Equal("Cargo no disponible", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_Http409WithProblemDetails_ReturnsFailedResultWithConflictDetail()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Title = "CargoConPuestosActivos",
            Detail = "El cargo tiene puestos activos",
            Status = 409
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("CargoConPuestosActivos", result.Code);
        Assert.Equal("El cargo tiene puestos activos", result.Message);
    }

    [Fact]
    public async Task DeleteAsync_Http500WithNonJsonBody_ReturnsFailedResultWithoutCrashing()
    {
        var id = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteAsync(id);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
    }

    // ──────────────────────────────────────────────
    // PR2A Task 12: CreateAsync / GetNivelesAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Http201WithPayload_ReturnsDtoAndHitsPostRoute()
    {
        var nivelId = Guid.NewGuid();
        var dto = new CargoDto(Guid.NewGuid(), "C-001", "Analista", "Desc", nivelId, "Junior");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Created, dto));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new CrearCargoRequest("C-001", "Analista", nivelId, "Desc");
        var result = await client.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("C-001", result.Value!.Codigo);
        Assert.Equal("Analista", result.Value.Nombre);
        Assert.Equal(HttpMethod.Post, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/cargos", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task CreateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors()
    {
        var nivelId = Guid.NewGuid();
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "El código es obligatorio." }
        })
        {
            Status = 400,
            Title = "ValidationError",
            Detail = "Datos inválidos."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new CrearCargoRequest("", "Analista", nivelId);
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("codigo", result.FieldErrors!.Keys);
        Assert.Equal("El código es obligatorio.", result.FieldErrors!["codigo"][0]);
    }

    [Fact]
    public async Task CreateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict()
    {
        var nivelId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un cargo activo con ese código."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new CrearCargoRequest("C-DUP", "Analista", nivelId);
        var result = await client.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("CodigoDuplicado", result.Error.Code);
        Assert.Equal("Ya existe un cargo activo con ese código.", result.Error.Message);
    }

    [Fact]
    public async Task GetNivelesAsync_Http200WithArray_ReturnsDtosAndHitsCatalogRoute()
    {
        var nivelId = Guid.NewGuid();
        var payload = new[]
        {
            new NivelCargoDto(nivelId, "JR", "Junior", 1, 1),
            new NivelCargoDto(Guid.NewGuid(), "SR", "Senior", 2, 2)
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetNivelesAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Junior", result[0].Nombre);
        Assert.Equal("Senior", result[1].Nombre);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/niveles-cargo", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    // ──────────────────────────────────────────────
    // PR2B Task 1: UpdateAsync (PUT /api/v1/cargos/{id})
    // ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Http200WithPayload_ReturnsDtoAndHitsPutRoute()
    {
        var id = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var dto = new CargoDto(id, "C-001", "Analista Senior", "Desc actualizada", nivelId, "Senior");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new ActualizarCargoRequest("C-001", "Analista Senior", nivelId, "Desc actualizada");
        var result = await client.UpdateAsync(id, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal("C-001", result.Value.Codigo);
        Assert.Equal("Analista Senior", result.Value.Nombre);
        Assert.Equal("Senior", result.Value.NivelNombre);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{id}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UpdateAsync_Http400WithValidationProblemDetails_ReturnsFailureWithFieldErrors()
    {
        var id = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["codigo"] = new[] { "El código no puede superar los 50 caracteres." },
            ["nombre"] = new[] { "El nombre es obligatorio." }
        })
        {
            Status = 400,
            Title = "ValidationError",
            Detail = "Datos inválidos."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new ActualizarCargoRequest(
            new string('x', 51),
            string.Empty,
            nivelId);
        var result = await client.UpdateAsync(id, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("codigo", result.FieldErrors!.Keys);
        Assert.Contains("nombre", result.FieldErrors!.Keys);
        Assert.Equal("El código no puede superar los 50 caracteres.", result.FieldErrors!["codigo"][0]);
        Assert.Equal("El nombre es obligatorio.", result.FieldErrors!["nombre"][0]);
    }

    [Fact]
    public async Task UpdateAsync_Http409WithProblemDetails_ReturnsFailureWithConflict()
    {
        var id = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un cargo activo con el código C-DUP."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var request = new ActualizarCargoRequest("C-DUP", "Cargo Duplicado", nivelId);
        var result = await client.UpdateAsync(id, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("CodigoDuplicado", result.Error.Code);
        Assert.Equal("Ya existe un cargo activo con el código C-DUP.", result.Error.Message);
        Assert.Null(result.FieldErrors);
    }

    // ──────────────────────────────────────────────
    // PR3 Task 5: QueryAsync (segmented) + ReactivateAsync
    // ──────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_WithStatusEliminadas_SerializesStatusInUri()
    {
        var id = Guid.NewGuid();
        var payload = new PagedResult<CargoDto>(
            [new CargoDto(id, "DEL-001", "Eliminado", null, Guid.NewGuid())],
            TotalCount: 1,
            Page: 1,
            PageSize: 20);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.QueryAsync(new CargoListQuery(1, 20, null, null, "eliminadas"));

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal("/api/v1/cargos/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("status=eliminadas", handler.LastRequest?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithoutStatus_DoesNotIncludeStatusParameter()
    {
        var payload = new PagedResult<CargoDto>([], 0, 1, 20);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new CargoListQuery(1, 20, "ana", "nombre_asc"));

        Assert.Equal("/api/v1/cargos/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        var query = handler.LastRequest?.RequestUri?.Query ?? string.Empty;
        Assert.Contains("search=ana", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status=", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithSort_SerializesSortInUri()
    {
        // REQ-CM-01: el sort debe viajar en query string para que el
        // backend lo aplique ANTES del Skip/Take. Si no se serializa,
        // la paginación con orden se rompe entre páginas.
        var payload = new PagedResult<CargoDto>(
            [new CargoDto(Guid.NewGuid(), "C-001", "Zeta", null, Guid.NewGuid())],
            TotalCount: 1,
            Page: 1,
            PageSize: 10);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new CargoListQuery(1, 10, null, "nombre_desc", null));

        Assert.Equal("/api/v1/cargos/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Contains("sort=nombre_desc", handler.LastRequest?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_WithoutSort_DoesNotIncludeSortParameter()
    {
        // Triangulación: cuando no hay sort, NO debe aparecer `sort=` en la URL
        // para no ensuciar el contrato con strings vacíos.
        var payload = new PagedResult<CargoDto>([], 0, 1, 10);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        _ = await client.QueryAsync(new CargoListQuery(1, 10, null, null, null));

        Assert.Equal("/api/v1/cargos/consulta", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.DoesNotContain("sort=", handler.LastRequest?.RequestUri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReactivateAsync_Http200_ReturnsDtoAndHitsReactivarRoute()
    {
        var id = Guid.NewGuid();
        var dto = new CargoDto(id, "DIRECTOR", "Director", null, Guid.NewGuid(), "Directivo");
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, dto));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.ReactivateAsync(id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal(HttpMethod.Patch, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{id}/reactivar", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ReactivateAsync_OnConflict_ReturnsConflictResult()
    {
        var id = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 409,
            Title = "CodigoDuplicado",
            Detail = "Ya existe un cargo activo con el mismo código."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.Conflict, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.ReactivateAsync(id);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoErrorType.Conflict, result.Error!.Type);
        Assert.Equal("CodigoDuplicado", result.Error.Code);
    }

    // ──────────────────────────────────────────────
    // Cobertura de contrato de transporte (issue #78):
    // fija que QueryAsync propaga excepciones nativas del pipeline HTTP
    // y respeta un CancellationToken pre-cancelado sin iniciar el envío.
    // Si el cliente capturara la excepción o disparara el handler con el
    // token ya cancelado, estos tests fallan.
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task QueryAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.QueryAsync(new CargoListQuery(1, 20, null, null, null)));
    }

    [Fact]
    public async Task QueryAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        var handler = new RecordingHandler();
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.QueryAsync(new CargoListQuery(1, 20, null, null, null), new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    // ──────────────────────────────────────────────
    // PR3a — subrecurso CargoSkill (T3.3: clientes + cobertura HTTP ↔ controller)
    //
    // Cada test en esta sección fija el contrato observable del cliente en el
    // subrecurso /api/v1/cargos/{cargoId}/skills/* y la equivalencia entre el
    // shape HTTP del backend (producido por CargosController de PR2) y el
    // resultado tipado que la Razor Page edita (PR3b). Si el contrato del
    // controller cambia, estos tests fallan ANTES de que el cambio llegue a
    // la UI.
    // ──────────────────────────────────────────────

    [Fact]
    public async Task GetSkillsAsync_Http200WithPayload_ReturnsParsedDtosAndHitsSubresourceRoute()
    {
        // AC de cargo-skill-query-contract Req 1: cada item del subrecurso
        // expone Skill, Nivel, SkillId, NivelRequeridoId, Ponderacion y
        // EsObligatoria. El cliente debe conservar esos 6 campos sin filtrar.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var habilidad = new HabilidadDto(skillId, "C-001", "Habilidad", null, "Cat");
        var nivel = new NivelHabilidadDto(nivelId, "JR", "Junior", 1, 1);
        var payload = new[]
        {
            new CargoSkillDetailDto(habilidad, nivel)
            {
                SkillId = skillId,
                NivelRequeridoId = nivelId,
                Ponderacion = 2.50m,
                EsObligatoria = true
            }
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, payload));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetSkillsAsync(cargoId);

        Assert.Single(result);
        Assert.Equal(skillId, result[0].SkillId);
        Assert.Equal(nivelId, result[0].NivelRequeridoId);
        Assert.Equal(2.50m, result[0].Ponderacion);
        Assert.True(result[0].EsObligatoria);
        Assert.Equal(habilidad, result[0].Skill);
        Assert.Equal(nivel, result[0].Nivel);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{cargoId}/skills", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetSkillsAsync_Http404_ReturnsEmptyListWithoutThrowing()
    {
        // AC de cargo-skill-ui-tabla-editable Req 2 escenario "Cargo sin
        // habilidades": la grilla parte de un estado vacío legible. Si el
        // cargo ya no existe, el endpoint responde 404; el cliente debe
        // tratarlo como estado vacío, NO como un error fatal.
        var cargoId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => Json<object?>(HttpStatusCode.NotFound, null));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.GetSkillsAsync(cargoId);

        Assert.Empty(result);
        Assert.Equal(HttpMethod.Get, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{cargoId}/skills", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http200WithPayload_ReturnsSuccessDtoAndHitsPutSubresourceRoute()
    {
        // AC de cargo-skill-asignar-editar Req 2: el PUT devuelve el DTO
        // completo del vínculo persistido (SkillId, NivelRequeridoId,
        // Ponderacion, EsObligatoria). El cliente debe serializar la request
        // SIN agregar cargoId/skillId al body (esos viven en la ruta).
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var dto = new CargoSkillDto(skillId, nivelId)
        {
            Ponderacion = 1.50m,
            EsObligatoria = true
        };
        CapturedJsonBody? captured = null;
        var handler = new RecordingHandler(req =>
        {
            captured = new CapturedJsonBody(req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty);
            return Json(HttpStatusCode.OK, dto);
        });
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(nivelId, 1.50m, true);

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(skillId, result.Value!.SkillId);
        Assert.Equal(nivelId, result.Value.NivelRequeridoId);
        Assert.Equal(1.50m, result.Value.Ponderacion);
        Assert.True(result.Value.EsObligatoria);
        Assert.Equal(HttpMethod.Put, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{cargoId}/skills/{skillId}", handler.LastRequest?.RequestUri?.AbsolutePath);
        Assert.Null(captured!.FindProperty("cargoId"));
        Assert.Null(captured.FindProperty("skillId"));
    }

    [Fact]
    public async Task UpsertSkillAsync_Http400WithPonderacionFieldError_ReturnsFailureWithFieldErrors()
    {
        // AC de cargo-skill-ponderacion-obligatoria Req 4: cuando el backend
        // emite un ValidationProblemDetails con el campo 'ponderacion', el
        // cliente debe traducirlo a CargoSkillCommandResult.Failure con
        // FieldErrors poblado bajo la misma clave. Esto alimenta la Razor
        // Page para que el error aparezca junto al input correcto.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var validation = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["ponderacion"] = new[] { "La ponderación no puede superar 100.00." }
        })
        {
            Status = 400,
            Title = "DatosInvalidos",
            Detail = "Uno o más campos del vínculo contienen errores de validación."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, validation));
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(nivelId, 150m);

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoSkillErrorType.Validation, result.Error!.Type);
        Assert.NotNull(result.FieldErrors);
        Assert.Contains("ponderacion", result.FieldErrors!.Keys);
        Assert.Equal("La ponderación no puede superar 100.00.", result.FieldErrors!["ponderacion"][0]);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http400WithoutErrors_ReturnsFailureWithValidationType()
    {
        // AC de cargo-skill-ponderacion-obligatoria Req 4 escenario degradado:
        // un 400 con ProblemDetails plano (sin 'errors') debe seguir
        // devolviendo un Failure tipado Validation, sin FieldErrors poblado
        // (la Razor Page lo renderiza como mensaje global, no por campo).
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 400,
            Title = "NivelHabilidadNoExiste",
            Detail = "El nivel de habilidad referenciado no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.BadRequest, problem));
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(Guid.NewGuid());

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoSkillErrorType.Validation, result.Error!.Type);
        Assert.Equal("NivelHabilidadNoExiste", result.Error.Code);
        Assert.Equal("El nivel de habilidad referenciado no existe.", result.Error.Message);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task UpsertSkillAsync_Http404_ReturnsFailureWithNotFound()
    {
        // AC de cargo-skill-asignar-editar Req 3 escenario "Nivel requerido
        // inexistente": si el cargo o la habilidad referenciada no existen,
        // el backend responde 404 y el cliente lo traduce a NotFound (no a
        // Validation), para que la UI lo distinga del error de validación.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var nivelId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 404,
            Title = "CargoNoEncontrado",
            Detail = "El cargo no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(nivelId);

        var result = await client.UpsertSkillAsync(cargoId, skillId, request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(CargoSkillErrorType.NotFound, result.Error!.Type);
        Assert.Equal("CargoNoEncontrado", result.Error.Code);
        Assert.Null(result.FieldErrors);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http204_ReturnsDeleteSuccessAndHitsDeleteSubresourceRoute()
    {
        // AC de cargo-skill-ui-tabla-editable Req 4 escenario "Quitar una
        // habilidad": el DELETE debe responder 204 y el cliente lo traduce
        // a un DeleteResult con Succeeded=true.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.True(result.Succeeded);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
        Assert.Equal(HttpMethod.Delete, handler.LastRequest?.Method);
        Assert.Equal($"/api/v1/cargos/{cargoId}/skills/{skillId}", handler.LastRequest?.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http404WithProblemDetails_ReturnsFailureWithNotFound()
    {
        // AC de cargo-skill-ui-tabla-editable Req 4 escenario degradado: si
        // el DELETE responde 404 (asociación inexistente), el cliente lo
        // traduce a Succeeded=false con Code/Message del ProblemDetails para
        // que la grilla muestre un mensaje legible sin stack trace.
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var problem = new ProblemDetails
        {
            Status = 404,
            Title = "AsociacionNoEncontrada",
            Detail = "La asociación entre el cargo y la habilidad no existe."
        };
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.NotFound, problem));
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal("AsociacionNoEncontrada", result.Code);
        Assert.Equal("La asociación entre el cargo y la habilidad no existe.", result.Message);
    }

    [Fact]
    public async Task DeleteSkillAsync_Http500WithNonJsonBody_ReturnsFailureWithoutCrashing()
    {
        // AC de cargo-skill-ui-tabla-editable Req 5: errores 5xx deben
        // traducirse en un Failure con StatusCode pero sincrash, sin filtrar
        // stack traces al usuario. La Razor Page usa StatusCode + Code/Message
        // nulos para mostrar un mensaje genérico "No se pudo completar la
        // operación".
        var cargoId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("not-json", System.Text.Encoding.UTF8, "text/plain")
        };
        var handler = new RecordingHandler(_ => response);
        var client = new CargoApiClient(NewHttpClient(handler));

        var result = await client.DeleteSkillAsync(cargoId, skillId);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Null(result.Code);
        Assert.Null(result.Message);
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task DeleteSkillAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        // AC de web-apiclient-transport-contract: la falla de transporte del
        // subrecurso (TaskCanceled o HttpRequest) debe propagarse sin que el
        // cliente la silencie. La Razor Page atrapa esto en handlers y
        // muestra un mensaje recuperable.
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Theory]
    [MemberData(nameof(HttpClientExceptionScenarios.TransportExceptionData), MemberType = typeof(HttpClientExceptionScenarios))]
    public async Task UpsertSkillAsync_TransportFails_PropagatesNativeException(
        string _, Func<Exception> exceptionFactory, Type expectedExceptionType)
    {
        // AC de web-apiclient-transport-contract aplicado al subrecurso
        // PUT: la falla de transporte (TaskCanceled o HttpRequest) debe
        // propagarse como excepción nativa, no enmascararse en un Failure.
        // Cubrir ambas ramas asegura que la Razor Page distingue
        // cancelación cooperativa de un corte real del backend.
        HttpMessageHandler handler = HttpClientExceptionScenarios.NewHandlerThrowing(exceptionFactory);
        var client = new CargoApiClient(NewHttpClient(handler));
        var request = new AsignarCargoSkillRequest(Guid.NewGuid());

        await Assert.ThrowsAsync(
            expectedExceptionType,
            async () => await client.UpsertSkillAsync(Guid.NewGuid(), Guid.NewGuid(), request));
    }

    [Fact]
    public async Task DeleteSkillAsync_CancellationAlreadyRequested_ThrowsAndDoesNotSendRequest()
    {
        // Contrato de cancelación cooperativa: un CancellationToken
        // pre-cancelado NO debe disparar el envío HTTP; el cliente debe
        // arrojar OperationCanceledException antes de tocar la red.
        var handler = new RecordingHandler();
        var client = new CargoApiClient(NewHttpClient(handler));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.DeleteSkillAsync(Guid.NewGuid(), Guid.NewGuid(), new CancellationToken(canceled: true)));

        Assert.Null(handler.LastRequest);
    }

    /// <summary>
    /// Helper minimalista para inspeccionar el cuerpo JSON serializado por
    /// <see cref="HttpRequestMessage"/>. Sólo busca claves de primer nivel,
    /// suficiente para blindar que el body del PUT no cargue <c>cargoId</c> /
    /// <c>skillId</c> (los ids viven en la ruta).
    /// </summary>
    private sealed class CapturedJsonBody
    {
        private readonly string _body;

        public CapturedJsonBody(string body)
        {
            _body = body;
        }

        public string? FindProperty(string name)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(_body);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                    && doc.RootElement.TryGetProperty(name, out _))
                {
                    return name;
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }

            return null;
        }
    }

    private static HttpClient NewHttpClient(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.test") };

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(payload)
        };
        return response;
    }
}