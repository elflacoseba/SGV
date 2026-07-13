using Microsoft.AspNetCore.Mvc;
using SGV.Contracts.Comun;
using Xunit;
using CargosCreateModel = SGV.Web.Pages.Organizacion.Cargos.CreateModel;
using CargosEditModel = SGV.Web.Pages.Organizacion.Cargos.EditModel;
using CargosHabilidadesModel = SGV.Web.Pages.Organizacion.Cargos.HabilidadesModel;
using CargosIndexModel = SGV.Web.Pages.Organizacion.Cargos.IndexModel;
using HabilidadesCreateModel = SGV.Web.Pages.Organizacion.Habilidades.CreateModel;
using HabilidadesEditModel = SGV.Web.Pages.Organizacion.Habilidades.EditModel;
using HabilidadesIndexModel = SGV.Web.Pages.Organizacion.Habilidades.IndexModel;
using PuestosCreateModel = SGV.Web.Pages.Organizacion.Puestos.CreateModel;
using PuestosEditModel = SGV.Web.Pages.Organizacion.Puestos.EditModel;
using PuestosIndexModel = SGV.Web.Pages.Organizacion.Puestos.IndexModel;
using UnidadesOrganizativasCreateModel = SGV.Web.Pages.Organizacion.UnidadesOrganizativas.CreateModel;
using UnidadesOrganizativasDetailsModel = SGV.Web.Pages.Organizacion.UnidadesOrganizativas.DetailsModel;
using UnidadesOrganizativasEditModel = SGV.Web.Pages.Organizacion.UnidadesOrganizativas.EditModel;
using UnidadesOrganizativasIndexModel = SGV.Web.Pages.Organizacion.UnidadesOrganizativas.IndexModel;

namespace SGV.Tests.Web;

/// <summary>
/// Helper de exhaustividad para los PageModels del change #125 (Slice 3).
/// Cada switch sobre <see cref="ErrorCategoria"/> en los 14 PageModels
/// debe cubrir las 7 variantes sin <c>default</c> silencioso (design §8.1,
/// F3). El helper centraliza la iteración para que cada test sólo deba
/// pasar el lambda que invoca el switch del PageModel bajo prueba.
/// </summary>
public static class PageModelExhaustivity
{
    /// <summary>
    /// Itera los 7 valores de <see cref="ErrorCategoria"/> y assertea que
    /// el lambda <paramref name="mapCategoriaToMessage"/> produce un
    /// mensaje no-vacío para cada uno. Falla el test si el lambda tira
    /// <see cref="System.Runtime.CompilerServices.SwitchExpressionException"/>
    /// (cubierta no-anticipada) o retorna null/whitespace.
    /// </summary>
    public static void AssertCoversAllCategorias(
        Func<ErrorCategoria, string> mapCategoriaToMessage,
        string pageModelName)
    {
        ArgumentNullException.ThrowIfNull(mapCategoriaToMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageModelName);

        foreach (var categoria in Enum.GetValues<ErrorCategoria>())
        {
            var message = mapCategoriaToMessage(categoria);
            Assert.False(
                string.IsNullOrWhiteSpace(message),
                $"[{pageModelName}] Categoria.{categoria} no produce mensaje (switch no cubre la variante).");
        }
    }
}

/// <summary>
/// Smoke tests parametrizados sobre los 14 PageModels del change #125
/// (Slice 3). Cada test invoca el switch interno del PageModel con cada
/// <see cref="ErrorCategoria"/> y assertea que retorna un mensaje
/// no-vacío, compensando la falta de <c>default</c> en los switches
/// exhaustivos.
/// <para>
/// RED pre-GREEN: los <c>MapCategoriaToMessage</c> aún no existen en
/// los PageModels; los tests fallan en compilación. GREEN: agregados en
/// T-3.5..T-3.9.
/// </para>
/// </summary>
public sealed class PageModelExhaustivityTests
{
    // ─────────────────────────────────────────────────────────────────
    // Habilidades (3 PageModels)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Habilidades_CreateModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            HabilidadesCreateModel.MapCategoriaToMessage,
            nameof(HabilidadesCreateModel));
    }

    [Fact]
    public void Habilidades_EditModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            HabilidadesEditModel.MapCategoriaToMessage,
            nameof(HabilidadesEditModel));
    }

    [Fact]
    public void Habilidades_IndexModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            HabilidadesIndexModel.MapCategoriaToMessage,
            nameof(HabilidadesIndexModel));
    }

    // ─────────────────────────────────────────────────────────────────
    // Cargos (4 PageModels)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Cargos_IndexModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            CargosIndexModel.MapCategoriaToMessage,
            nameof(CargosIndexModel));
    }

    [Fact]
    public void Cargos_CreateModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            CargosCreateModel.MapCategoriaToMessage,
            nameof(CargosCreateModel));
    }

    [Fact]
    public void Cargos_EditModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            CargosEditModel.MapCategoriaToMessage,
            nameof(CargosEditModel));
    }

    [Fact]
    public void Cargos_HabilidadesModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            CargosHabilidadesModel.MapCategoriaToMessage,
            nameof(CargosHabilidadesModel));
    }

    // ─────────────────────────────────────────────────────────────────
    // Puestos (3 PageModels)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Puestos_IndexModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            PuestosIndexModel.MapCategoriaToMessage,
            nameof(PuestosIndexModel));
    }

    [Fact]
    public void Puestos_CreateModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            PuestosCreateModel.MapCategoriaToMessage,
            nameof(PuestosCreateModel));
    }

    [Fact]
    public void Puestos_EditModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            PuestosEditModel.MapCategoriaToMessage,
            nameof(PuestosEditModel));
    }

    // ─────────────────────────────────────────────────────────────────
    // UnidadesOrganizativas (4 PageModels)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void UnidadesOrganizativas_IndexModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            UnidadesOrganizativasIndexModel.MapCategoriaToMessage,
            nameof(UnidadesOrganizativasIndexModel));
    }

    [Fact]
    public void UnidadesOrganizativas_CreateModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            UnidadesOrganizativasCreateModel.MapCategoriaToMessage,
            nameof(UnidadesOrganizativasCreateModel));
    }

    [Fact]
    public void UnidadesOrganizativas_EditModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            UnidadesOrganizativasEditModel.MapCategoriaToMessage,
            nameof(UnidadesOrganizativasEditModel));
    }

    [Fact]
    public void UnidadesOrganizativas_DetailsModel_CoversAllCategorias()
    {
        PageModelExhaustivity.AssertCoversAllCategorias(
            UnidadesOrganizativasDetailsModel.MapCategoriaToMessage,
            nameof(UnidadesOrganizativasDetailsModel));
    }
}