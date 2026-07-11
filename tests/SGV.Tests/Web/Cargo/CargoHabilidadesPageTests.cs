using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web.Habilidad;
using Xunit;

namespace SGV.Tests.Web.Cargo;

public sealed partial class CargoHabilidadesPageTests : IClassFixture<CargoWebTestFixture>
{
    private readonly CargoWebTestFixture _fixture;

    public CargoHabilidadesPageTests(CargoWebTestFixture fixture) => _fixture = fixture;
}
