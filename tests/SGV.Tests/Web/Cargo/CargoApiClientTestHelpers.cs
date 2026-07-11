using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Habilidades.Consultas.Dtos;
using SGV.Contracts.Organizacion.Comandos;
using SGV.Contracts.Organizacion.Consultas.Dtos;
using SGV.Tests.Web._Shared;
using SGV.Web.Integration.Organizacion;
using Xunit;
using CargoListQuery = SGV.Web.Integration.Organizacion.CargoListQuery;
using RecordingHandler = SGV.Tests.Web._Shared.HttpClientExceptionScenarios.RecordingHandler;

namespace SGV.Tests.Web.Cargo;

public partial class CargoApiClientTests
{
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
