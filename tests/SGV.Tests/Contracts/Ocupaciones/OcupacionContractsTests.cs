using System.Text.Json;
using SGV.Contracts.Comun;
using SGV.Contracts.Ocupaciones.Comandos;
using SGV.Contracts.Ocupaciones.Dtos;
using SGV.Contracts.Ocupaciones.Enums;
using Xunit;

namespace SGV.Tests.Contracts.Ocupaciones;

public sealed class OcupacionContractsTests
{
    [Fact]
    public void OcupacionDto_SerializesCompleteWireShapeWithNamedEnums()
    {
        var dto = new OcupacionDto(
            Guid.Parse("80000000-0000-0000-0000-000000000001"),
            Guid.Parse("81000000-0000-0000-0000-000000000001"),
            "Ana Pérez",
            Guid.Parse("82000000-0000-0000-0000-000000000001"),
            "Analista",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            OcupacionTipoAsignacion.Temporal,
            "Cobertura",
            OcupacionEstado.Finalizada);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, JsonSerializerOptions.Web));
        var root = json.RootElement;

        Assert.Equal(dto.Id, root.GetProperty("id").GetGuid());
        Assert.Equal(dto.PersonaId, root.GetProperty("personaId").GetGuid());
        Assert.Equal("Ana Pérez", root.GetProperty("personaNombre").GetString());
        Assert.Equal(dto.PuestoId, root.GetProperty("puestoId").GetGuid());
        Assert.Equal("Analista", root.GetProperty("puestoNombre").GetString());
        Assert.Equal("2026-07-01", root.GetProperty("fechaInicio").GetString());
        Assert.Equal("2026-07-31", root.GetProperty("fechaFin").GetString());
        Assert.Equal("Temporal", root.GetProperty("tipoAsignacion").GetString());
        Assert.Equal("Cobertura", root.GetProperty("observaciones").GetString());
        Assert.Equal("Finalizada", root.GetProperty("estado").GetString());
    }

    [Fact]
    public void OcupacionCommandResult_FailurePreservesCategoriaCodeAndFieldErrors()
    {
        IReadOnlyDictionary<string, string[]> fieldErrors = new Dictionary<string, string[]>
        {
            ["personaId"] = ["La persona es obligatoria."]
        };
        var error = new OcupacionError(
            ErrorCategoria.Validation,
            OcupacionErrorCodigo.PersonaInactiva,
            "Datos inválidos");

        var result = OcupacionCommandResult.Failure(error, fieldErrors);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(ErrorCategoria.Validation, result.Error!.Categoria);
        Assert.Equal(OcupacionErrorCodigo.PersonaInactiva, result.Error.Code);
        Assert.Equal(fieldErrors, result.FieldErrors);
    }

    [Fact]
    public void OcupacionCommandResult_SuccessContainsValueWithoutError()
    {
        var dto = new OcupacionDto(
            Guid.NewGuid(), Guid.NewGuid(), "Ana Pérez", Guid.NewGuid(), "Analista",
            new DateOnly(2026, 7, 1), null, OcupacionTipoAsignacion.Permanente, null,
            OcupacionEstado.Vigente);

        var result = OcupacionCommandResult.Success(dto);

        Assert.True(result.IsSuccess);
        Assert.Same(dto, result.Value);
        Assert.Null(result.Error);
        Assert.Null(result.FieldErrors);
    }
}
