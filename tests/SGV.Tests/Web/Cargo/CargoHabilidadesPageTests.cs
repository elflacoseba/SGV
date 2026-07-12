using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Collections;
using SGV.Tests.Web.Habilidad;
using Xunit;

namespace SGV.Tests.Web.Cargo;

[Collection("WebIntegration")]
public sealed partial class CargoHabilidadesPageTests
{
    private readonly WebIntegrationFixture _fixture;

    public CargoHabilidadesPageTests(WebIntegrationFixture fixture) => _fixture = fixture;
}