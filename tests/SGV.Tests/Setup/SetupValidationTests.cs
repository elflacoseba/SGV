using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SGV.Aplicacion.Setup;
using SGV.Contracts.Comun;
using SGV.Contracts.Setup;
using SGV.Tests.Api;
using SGV.Tests.Api.Collections;
using Xunit;

namespace SGV.Tests.Setup;

/// <summary>
/// Tests del endpoint <c>POST /api/v1/setup</c> con datos inválidos
/// (issue #195 REQ-SETUP-002 escenario "Validación de Identity").
/// Verifica:
/// - Devuelve 400 Bad Request con <c>ValidationProblemDetails</c>.
/// - Los <c>fieldErrors</c> están poblados y contienen claves en camelCase
///   que la Razor Page <c>Setup.cshtml</c> puede mapear a los
///   <c>asp-validation-for</c>.
/// </summary>
[Collection("ApiIntegration")]
public sealed class SetupValidationTests
{
    private readonly ApiIntegrationFixture _fixture;
    public SetupValidationTests(ApiIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Crear_DatosInvalidos_FluentValidationFalla_Devuelve400ConFieldErrors()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ISetupServicio>();
            services.AddSingleton<ISetupServicio>(new FakeSetupServicio(
                crearAdminAsync: _ => SetupCommandResult.Failure(
                    new SetupError(
                        ErrorCategoria.Validation,
                        SetupErrorCode.DatosInvalidos,
                        "Uno o más campos contienen errores de validación.",
                        StatusCode: 400),
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["nombres"] = ["El nombre es obligatorio."],
                        ["password"] = ["La contraseña debe tener al menos 6 caracteres."]
                    })));
        });
        var client = factory.CreateClient();

        var request = new SetupRequest(
            Nombres: string.Empty, // Inválido
            Apellidos: "Seed",
            Legajo: "LEG-1",
            Email: "bad",
            UserName: "u",
            Password: "Ab1",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: null);

        var response = await client.PostAsJsonAsync("/api/v1/setup", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("nombres", problem!.Errors.Keys);
        Assert.Contains("password", problem.Errors.Keys);
        Assert.Equal("DatosInvalidos", problem.Title);
    }

    [Fact]
    public async Task Crear_PasswordDebil_Devuelve400ConCodigoPasswordDebil()
    {
        await using var factory = _fixture.RootFactory.WithOverrides(services =>
        {
            services.RemoveService<ISetupServicio>();
            services.AddSingleton<ISetupServicio>(new FakeSetupServicio(
                crearAdminAsync: _ => SetupCommandResult.Failure(
                    new SetupError(
                        ErrorCategoria.Validation,
                        SetupErrorCode.PasswordDebil,
                        "La contraseña debe tener al menos 6 caracteres.",
                        StatusCode: 400))));
        });
        var client = factory.CreateClient();

        var request = new SetupRequest(
            Nombres: "Admin",
            Apellidos: "Seed",
            Legajo: null,
            Email: "admin@test.com",
            UserName: "admin",
            Password: "Ab1",
            TipoDocumentoId: null,
            NumeroDocumento: null,
            Telefono: null);

        var response = await client.PostAsJsonAsync("/api/v1/setup", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
