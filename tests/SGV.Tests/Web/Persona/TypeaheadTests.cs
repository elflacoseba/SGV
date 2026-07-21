using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGV.Contracts.Personas.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Web.Integration.Personas;
using Xunit;

namespace SGV.Tests.Web.Persona;

/// <summary>
/// Tests del partial reutilizable <c>_PersonaTypeahead.cshtml</c>
/// introducido en PR 3/4. Verifica el contrato observable del partial sin
/// requerir una página host: se renderiza directamente vía
/// <see cref="ICompositeViewEngine"/> para triangular los hooks del lado
/// servidor (data-attributes, input hidden, JSON embebido).
/// </summary>
[Collection("WebIntegration")]
public sealed class TypeaheadTests
{
    private readonly WebIntegrationFixture _fixture;

    public TypeaheadTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // T-XX 1: el partial renderiza con el viewmodel provisto
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RenderPartial_WithPersonasAndSelectedId_ProducesExpectedDataAttributes()
    {
        // AC: el partial emite data-persona-typeahead-selected-id en el
        // contenedor raíz y value="{id}" en el input hidden, junto con el
        // JSON embebido (script[type=application/json]) que el JS consume
        // para el filtrado client-side.
        var ana = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", "ana@example.com", null, null, "DNI", "30123456", null, true);
        var juan = new PersonaDto(Guid.NewGuid(), "L-002", "Juan", "Pérez", null, null, null, null, null, null, true);
        var model = new PersonaTypeaheadViewModel(
            AllPersonas: [ana, juan],
            SelectedId: ana.Id,
            InputName: "PersonaId",
            MinChars: 2);

        var html = await RenderPartialAsync(model);

        // Contenedor raíz con hook de selección.
        Assert.Contains("data-persona-typeahead", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"data-persona-typeahead-selected-id=\"{ana.Id}\"",
            html,
            StringComparison.OrdinalIgnoreCase);

        // Input hidden con el id seleccionado y nombre configurable.
        Assert.Contains("data-persona-typeahead-hidden", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"value=\"{ana.Id}\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"PersonaId\"", html, StringComparison.OrdinalIgnoreCase);

        // JSON embebido contiene ambas personas con la forma camelCase
        // (cliente lo consume via getAttribute del contenedor). El Razor
        // escapa el JSON al contexto atributo, así que las comillas vienen
        // como &quot;; comprobamos la presencia vía los ids serializados.
        Assert.Contains("&quot;id&quot;", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ana.Id.ToString(), html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(juan.Id.ToString(), html, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // T-XX 2: filtro MinChars se serializa en data-min-chars
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task RenderPartial_WithMinChars_ExposesItInDataMinCharsAttribute(int minChars)
    {
        // AC: el JS del typeahead lee data-min-chars para mostrar el hint
        // inicial y filtrar sólo cuando el término >= minChars. Si el
        // atributo no aparece, el script cae a un default que puede no
        // coincidir con la expectativa del usuario.
        var ana = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var model = new PersonaTypeaheadViewModel(
            AllPersonas: [ana],
            SelectedId: null,
            MinChars: minChars);

        var html = await RenderPartialAsync(model);

        Assert.Contains(
            $"data-min-chars=\"{minChars}\"",
            html,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenderPartial_WithNoPersonas_RendersEmptyResultsHint()
    {
        // AC: cuando AllPersonas está vacío, el partial emite el hint
        // "Escribí al menos N caracteres" sin exponer ids en el JSON
        // embebido. El JS no debe poder seleccionar nada.
        var model = new PersonaTypeaheadViewModel(
            AllPersonas: Array.Empty<PersonaDto>(),
            SelectedId: null,
            MinChars: 2);

        var html = await RenderPartialAsync(model);

        Assert.Contains("data-persona-typeahead", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-persona-typeahead-hint", html, StringComparison.OrdinalIgnoreCase);

        // El JSON embebido debe estar presente pero con items=[].
        var match = Regex.Match(html, @"<script type=""application/json"" data-persona-typeahead-data>([\s\S]*?)</script>", RegexOptions.IgnoreCase);
        Assert.True(match.Success, "Esperaba encontrar el <script type=application/json> con los datos del typeahead.");

        var json = match.Groups[1].Value;
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    // ──────────────────────────────────────────────
    // T-XX 3: hook de selección setea data-persona-typeahead-selected-id
    // ──────────────────────────────────────────────

    [Fact]
    public async Task RenderPartial_WithNullSelectedId_RendersEmptySelectedIdAttribute()
    {
        // AC: cuando SelectedId es null, el partial emite
        // data-persona-typeahead-selected-id="" para que el JS pueda
        // detectar "sin selección" sin parsear string.Empty contra null.
        var ana = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var model = new PersonaTypeaheadViewModel(
            AllPersonas: [ana],
            SelectedId: null,
            MinChars: 2);

        var html = await RenderPartialAsync(model);

        Assert.Contains("data-persona-typeahead-selected-id=\"\"", html, StringComparison.OrdinalIgnoreCase);
        // El input hidden también debe tener value="" cuando no hay selección.
        // Lo verificamos como dos Contains separados para evitar regex
        // frágil: el atributo value="" y el data-attribute del hidden.
        Assert.Contains("value=\"\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-persona-typeahead-hidden", html, StringComparison.OrdinalIgnoreCase);
        // Y ambos deben estar dentro del mismo <input type="hidden">: el
        // hidden input es el único del partial.
        Assert.Matches(
            new Regex(@"<input[^>]*type=""hidden""[^>]*>"),
            html);
    }

    [Fact]
    public async Task RenderPartial_WithCustomInputName_PropagatesToHiddenInput()
    {
        // AC: el nombre del input hidden es configurable vía InputName para
        // evitar colisiones cuando el partial se embebe en hosts con field
        // names distintos (e.g. Usuario.PersonaId).
        var ana = new PersonaDto(Guid.NewGuid(), "L-001", "Ana", "García", null, null, null, null, null, null, true);
        var model = new PersonaTypeaheadViewModel(
            AllPersonas: [ana],
            SelectedId: ana.Id,
            InputName: "CustomFieldName",
            MinChars: 2);

        var html = await RenderPartialAsync(model);

        Assert.Contains("name=\"CustomFieldName\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-input-name=\"CustomFieldName\"", html, StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private async Task<string> RenderPartialAsync(PersonaTypeaheadViewModel model)
    {
        await using var lease = await _fixture.CreatePersonaLeaseAsync(new FakePersonaApiClient());

        // IViewBufferScope está registrado como Scoped; creamos un scope
        // dedicado para resolver todos los servicios del ViewContext desde
        // un único IServiceProvider con la duración correcta.
        using var scope = lease.Factory.Services.CreateScope();
        var scopedServices = scope.ServiceProvider;

        var engine = scopedServices.GetRequiredService<ICompositeViewEngine>();
        var metadataProvider = scopedServices.GetRequiredService<IModelMetadataProvider>();
        var tempDataProvider = scopedServices.GetRequiredService<ITempDataProvider>();

        // Construimos un ActionContext mínimo (sin controller/route real).
        var httpContext = new DefaultHttpContext { RequestServices = scopedServices };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());

        // Buscamos el partial en su ubicación convencional. Usamos la
        // convención RazorPage: el partial vive bajo Pages/Personas/Shared/.
        var viewName = "~/Pages/Personas/Shared/_PersonaTypeahead.cshtml";
        var viewResult = engine.GetView(null, viewName, isMainPage: false);

        if (!viewResult.Success)
        {
            // Fallback: algunos view engines resuelven por nombre sin
            // extensión. Probamos la convención corta.
            viewResult = engine.FindView(actionContext, "_PersonaTypeahead", isMainPage: false);
        }

        Assert.True(viewResult.Success, $"No se pudo resolver el view '{viewName}'. Buscado en: {string.Join(", ", viewResult.SearchedLocations)}");

        var viewData = new ViewDataDictionary<PersonaTypeaheadViewModel>(metadataProvider, new ModelStateDictionary())
        {
            Model = model
        };

        using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewData,
            new TempDataDictionary(httpContext, tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}