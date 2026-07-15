using SGV.Contracts.Personas.Consultas.Dtos;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Unit tests para el comportamiento del fake en memoria
/// <see cref="FakePersonaApiClient"/>: específicamente el manejo del
/// segmento <see cref="PersonaSegmentoListado"/> (activas / eliminadas) y
/// la paginación. Espejo de <c>FakeCargoApiClientTests</c> para el módulo
/// de Personas.
/// </summary>
public class FakePersonaApiClientTests
{
    [Theory]
    [InlineData(PersonaSegmentoListado.Activas)]
    [InlineData(PersonaSegmentoListado.Eliminadas)]
    public async Task QueryAsync_WithSegmento_ReturnsExpectedSubset(PersonaSegmentoListado segmento)
    {
        // AC: el segmento Activas/Eliminadas filtra exactamente sobre la
        // marca de baja lógica interna del fake; los ids no eliminados
        // aparecen sólo bajo Activas y viceversa.
        var activa = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", null, null, null, null, true);
        var eliminada = new PersonaDto(Guid.NewGuid(), "L-002", "Juan", "Pérez", null, null, null, null, false);
        var apiClient = FakePersonaApiClient.WithPersonaList(activa, eliminada);

        await apiClient.DesactivarAsync(eliminada.Id);

        var result = await apiClient.QueryAsync(new PersonaListQuery(1, 20, null, null, segmento));

        if (segmento == PersonaSegmentoListado.Activas)
        {
            Assert.Single(result.Items);
            Assert.Equal(activa.Id, result.Items[0].Id);
        }
        else
        {
            Assert.Single(result.Items);
            Assert.Equal(eliminada.Id, result.Items[0].Id);
        }
    }

    [Fact]
    public async Task IsDeleted_AfterDesactivarAsync_ReturnsTrue()
    {
        var persona = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(persona);

        Assert.False(apiClient.IsDeleted(persona.Id));

        await apiClient.DesactivarAsync(persona.Id);

        Assert.True(apiClient.IsDeleted(persona.Id));
    }

    [Fact]
    public async Task QueryAsync_WithSearchFilterAcrossFiveFields_AppliesCaseInsensitiveSubstring()
    {
        // REQ-CM-01 (spec §"Listado segmentado"): la búsqueda aplica a
        // Legajo|Nombres|Apellidos|Email|NumeroDocumento case-insensitive.
        // Triangulamos tres campos diferentes con el mismo término.
        var ana = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", "ana@example.com", "DNI", "30123456", null, true);
        var juan = new PersonaDto(Guid.NewGuid(), "L-002", "Juan", "Pérez", "juan@example.com", "DNI", "28999888", null, true);
        var maria = new PersonaDto(Guid.NewGuid(), "L-003", "María", "García", null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(ana, juan, maria);

        // Búsqueda por legajo
        var byLegajo = await apiClient.QueryAsync(new PersonaListQuery(1, 20, "L-002", null, PersonaSegmentoListado.Activas));
        Assert.Single(byLegajo.Items);
        Assert.Equal(juan.Id, byLegajo.Items[0].Id);

        // Búsqueda por email
        var byEmail = await apiClient.QueryAsync(new PersonaListQuery(1, 20, "ANA@EXAMPLE", null, PersonaSegmentoListado.Activas));
        Assert.Single(byEmail.Items);
        Assert.Equal(ana.Id, byEmail.Items[0].Id);

        // Búsqueda por apellido (compartido entre ana y maria)
        var byApellido = await apiClient.QueryAsync(new PersonaListQuery(1, 20, "GARCÍA", null, PersonaSegmentoListado.Activas));
        Assert.Equal(2, byApellido.Items.Count);
    }

    [Fact]
    public async Task QueryAsync_DefaultSort_OrdersByApellidosAscending()
    {
        // AC: cuando no se especifica sort, el fake cae a apellidos_asc
        // (consistente con la convención del backend de Personas).
        var ana = new PersonaDto(Guid.NewGuid(), null, "Ana", "Zapata", null, null, null, null, true);
        var juan = new PersonaDto(Guid.NewGuid(), null, "Juan", "Acosta", null, null, null, null, true);
        var apiClient = FakePersonaApiClient.WithPersonaList(ana, juan);

        var result = await apiClient.QueryAsync(new PersonaListQuery(1, 20, null, null, PersonaSegmentoListado.Activas));

        // OrdinalIgnoreCase: Acosta < Zapata
        Assert.Equal(juan.Id, result.Items[0].Id);
        Assert.Equal(ana.Id, result.Items[1].Id);
    }
}