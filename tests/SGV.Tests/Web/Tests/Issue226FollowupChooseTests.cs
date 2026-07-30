using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace SGV.Tests.Web.Tests;

/// <summary>
/// Tests de inspección del código fuente del script
/// <c>src/SGV.Web/wwwroot/js/pages/usuario-persona-buscador.js</c> para
/// validar el contrato USBJS-02/03 revisado como parte del follow-up de
/// la issue #226 (modal no cerraba ni devolvía la persona elegida al
/// seleccionarla en el Caso 6 del empty state de
/// <c>_PersonaCard.cshtml</c>).
///
/// El fix #224 (USBJS-02) decidió que cuando los elementos del contrato
/// (<c>displayInput</c>, <c>cardText</c>, <c>card</c>, <c>empty</c>) no
/// están presentes en el DOM — Caso 6 puro, típico de Create — el script
/// aborta el flujo de <c>choose()</c> con un <c>console.warn</c>. Eso
/// dejó el bug del usuario: modal sin cerrar, persona sin renderizar,
/// <c>change</c> sin disparar. El fix de este change relaja esa decisión
/// y agrega render dinámico de la card con Quitar/Cambiar.
///
/// Estos tests NO reemplazan a una suite runtime con Playwright/Selenium
/// (que requeriría agregar deps); blindan el contrato a nivel de source
/// inspection para detectar regresiones futuras del mismo tipo.
/// </summary>
public sealed class Issue226FollowupChooseTests
{
    private static string ReadScriptSource()
    {
        // El test runner corre desde `tests/SGV.Tests/bin/Debug/net10.0/`.
        // Subimos hasta 6 niveles buscando el archivo. La estrategia
        // robusta es: probar el cwd, después subir un nivel y reintentar.
        var fileName = "usuario-persona-buscador.js";
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var level = 0; level < 8 && dir is not null; level++)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src", "SGV.Web", "wwwroot", "js", "pages", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            $"No se encontró src/SGV.Web/wwwroot/js/pages/{fileName} "
            + $"subiendo 8 niveles desde cwd={Directory.GetCurrentDirectory()}.");
    }

    // ──────────────────────────────────────────────────────────────────
    // USBJS-02 revisión: choose() NO aborta antes de cerrar el modal.
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Choose_DoesNotHaveEarlyReturnBeforeModalHide()
    {
        var source = ReadScriptSource();

        // El bug original: dentro de choose(), después del set de
        // hiddenInput, había un `if (!displayInput || ...) { return; }`
        // que abortaba antes del Modal.hide(). Verificamos que ese patrón
        // problemático ya NO existe en choose().
        var chooseMatch = Regex.Match(
            source,
            @"function choose\(persona\)\s*\{[\s\S]*?\n\s{4}\}",
            RegexOptions.Multiline);
        Assert.True(
            chooseMatch.Success,
            "No se encontró la función choose() en el script. ¿Se renombró?");

        var chooseBody = chooseMatch.Value;

        // Patrón problemático: `return;` dentro de un bloque if temprano
        // en choose() (antes del dispatchEvent('change') y del .hide()).
        // El fix garantiza que el `return` solo se permite en
        // renderDynamicCard() (camino defensivo), nunca en choose().
        var earlyReturn = Regex.Match(
            chooseBody,
            @"\n\s{4}return\s*;\s*\n",
            RegexOptions.Multiline);
        Assert.False(
            earlyReturn.Success,
            "choose() contiene un `return;` temprano. El bug original era "
            + "que esto abortaba el flujo antes de cerrar el modal y "
            + "disparar el change. El fix de este change elimina ese "
            + "early return para que el flujo complete siempre.\n\n"
            + $"Cuerpo de choose():\n{chooseBody}");
    }

    [Fact]
    public void Choose_AlwaysDispatchesChangeEvent()
    {
        var source = ReadScriptSource();

        // El bug original: el `dispatchEvent(new Event('change'))` estaba
        // dentro del bloque if (después del return temprano). El fix lo
        // mueve afuera del if para garantizar que se dispare siempre.
        var chooseMatch = Regex.Match(
            source,
            @"function choose\(persona\)\s*\{[\s\S]*?\n\s{4}\}",
            RegexOptions.Multiline);
        Assert.True(chooseMatch.Success, "No se encontró la función choose().");
        var chooseBody = chooseMatch.Value;

        var dispatch = Regex.Match(
            chooseBody,
            @"hiddenInput\.dispatchEvent\(new Event\('change'",
            RegexOptions.Multiline);
        Assert.True(
            dispatch.Success,
            "choose() no contiene `hiddenInput.dispatchEvent(new Event('change'))`. "
            + "El bug original era que esto estaba dentro de un if que "
            + "abortaba en Caso 6. El fix garantiza que se dispare "
            + "siempre.");

        // Verificamos que el dispatch está DESPUÉS del set de hiddenInput.
        var hiddenInputSet = chooseBody.IndexOf("hiddenInput.value = persona.id", StringComparison.Ordinal);
        var dispatchPos = chooseBody.IndexOf("hiddenInput.dispatchEvent", StringComparison.Ordinal);
        Assert.True(
            hiddenInputSet >= 0 && dispatchPos > hiddenInputSet,
            "El dispatch del change debe estar DESPUÉS del set de "
            + "hiddenInput. Si no, se dispara antes de tener el id de la "
            + "persona.\n\n"
            + $"Cuerpo de choose():\n{chooseBody}");
    }

    [Fact]
    public void Choose_AlwaysHidesModal()
    {
        var source = ReadScriptSource();

        var chooseMatch = Regex.Match(
            source,
            @"function choose\(persona\)\s*\{[\s\S]*?\n\s{4}\}",
            RegexOptions.Multiline);
        Assert.True(chooseMatch.Success, "No se encontró la función choose().");
        var chooseBody = chooseMatch.Value;

        var hideCall = Regex.Match(
            chooseBody,
            @"Modal\.getOrCreateInstance\(modal\)\.hide\(\)",
            RegexOptions.Multiline);
        Assert.True(
            hideCall.Success,
            "choose() no llama a `Modal.getOrCreateInstance(modal).hide()`. "
            + "El bug original era que esta llamada estaba después de un "
            + "early return que abortaba el flujo en el Caso 6. El fix "
            + "garantiza que el modal se cierre SIEMPRE.");

        // El .hide() debe estar DESPUÉS del set de hiddenInput y del dispatch.
        var hiddenInputSet = chooseBody.IndexOf("hiddenInput.value = persona.id", StringComparison.Ordinal);
        var dispatchPos = chooseBody.IndexOf("hiddenInput.dispatchEvent", StringComparison.Ordinal);
        var hidePos = chooseBody.IndexOf("Modal.getOrCreateInstance(modal).hide()", StringComparison.Ordinal);
        Assert.True(
            hiddenInputSet >= 0 && dispatchPos > hiddenInputSet && hidePos > dispatchPos,
            "El Modal.hide() debe estar DESPUÉS del set de hiddenInput y "
            + "del dispatch del change. Si no, se cierra el modal antes de "
            + "notificar a los listeners.\n\n"
            + $"Cuerpo de choose():\n{chooseBody}");
    }

    [Fact]
    public void Choose_AlwaysEnablesSubmitWhenPresent()
    {
        var source = ReadScriptSource();

        var chooseMatch = Regex.Match(
            source,
            @"function choose\(persona\)\s*\{[\s\S]*?\n\s{4}\}",
            RegexOptions.Multiline);
        Assert.True(chooseMatch.Success, "No se encontró la función choose().");
        var chooseBody = chooseMatch.Value;

        // El submit.disabled = false debe estar DESPUÉS del cierre del
        // if/else del contrato, no anidado dentro del then (que era el
        // bug original — el early return dejaba el submit deshabilitado).
        // Buscamos el cierre del else con un patrón flexible (cualquier
        // indentación).
        var contractElseEnd = Regex.Match(
            chooseBody,
            @"\}\s*else\s*\{[\s\S]*?renderDynamicCard\([^)]*\)\s*;[\s\S]*?\n\s+\}",
            RegexOptions.Multiline);
        Assert.True(
            contractElseEnd.Success,
            "choose() debe tener un if/else del contrato que termine antes "
            + "del submit.disabled. No se encontró el cierre del else.\n\n"
            + $"Cuerpo de choose():\n{chooseBody}");

        var elseEndPos = contractElseEnd.Index + contractElseEnd.Length;
        var submitEnable = Regex.Match(
            chooseBody.Substring(elseEndPos),
            @"submit\.disabled\s*=\s*false",
            RegexOptions.Multiline);
        Assert.True(
            submitEnable.Success,
            "choose() debe habilitar `submit.disabled = false` DESPUÉS "
            + "del cierre del if/else del contrato. El bug original era "
            + "que esto estaba dentro del then, anidado bajo el early "
            + "return que abortaba en Caso 6, dejando el submit "
            + "deshabilitado para el usuario.\n\n"
            + $"Resto de choose() después del if/else:\n{chooseBody.Substring(elseEndPos)}");
    }

    // ──────────────────────────────────────────────────────────────────
    // USBJS-02 revisión: renderDynamicCard existe y se llama en el camino
    // del Caso 6.
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Script_DefinesRenderDynamicCardFunction()
    {
        var source = ReadScriptSource();

        var renderFn = Regex.Match(
            source,
            @"function\s+renderDynamicCard\s*\(\s*text\s*\)\s*\{",
            RegexOptions.Multiline);
        Assert.True(
            renderFn.Success,
            "El script debe definir `function renderDynamicCard(text)`. "
            + "Esta función construye la card mínima con Quitar/Cambiar "
            + "cuando la partial no emite la card (Caso 6: empty state "
            + "puro). Sin ella, el usuario no ve la persona seleccionada.");
    }

    [Fact]
    public void Choose_CallsRenderDynamicCardInEmptyCase()
    {
        var source = ReadScriptSource();

        var chooseMatch = Regex.Match(
            source,
            @"function choose\(persona\)\s*\{[\s\S]*?\n\s{4}\}",
            RegexOptions.Multiline);
        Assert.True(chooseMatch.Success, "No se encontró la función choose().");
        var chooseBody = chooseMatch.Value;

        // Dentro de choose(), debe haber un else que llame a
        // renderDynamicCard(text).
        var emptyCaseElse = Regex.Match(
            chooseBody,
            @"\}\s*else\s*\{[\s\S]*?renderDynamicCard\(\s*text\s*\)[\s\S]*?\}",
            RegexOptions.Multiline);
        Assert.True(
            emptyCaseElse.Success,
            "choose() debe tener un `else` que llame a "
            + "`renderDynamicCard(text)` cuando los elementos del "
            + "contrato no están presentes (Caso 6). Sin esto, el Caso "
            + "6 no muestra la persona seleccionada.\n\n"
            + $"Cuerpo de choose():\n{chooseBody}");
    }

    [Fact]
    public void RenderDynamicCard_CreatesCardTextQuitarAndCambiarElements()
    {
        var source = ReadScriptSource();

        var renderFn = Regex.Match(
            source,
            @"function\s+renderDynamicCard\s*\(\s*text\s*\)\s*\{[\s\S]*?\n\s{4}\}",
            RegexOptions.Multiline);
        Assert.True(
            renderFn.Success,
            "No se encontró la función renderDynamicCard.");
        var renderBody = renderFn.Value;

        // Debe crear el wrapper con data-usuario-persona-card.
        Assert.Contains(
            "data-usuario-persona-card",
            renderBody,
            System.StringComparison.Ordinal);
        // Debe crear el span con data-usuario-persona-display-text.
        Assert.Contains(
            "data-usuario-persona-display-text",
            renderBody,
            System.StringComparison.Ordinal);
        // Debe crear el botón Quitar.
        Assert.Contains(
            "data-usuario-persona-quitar",
            renderBody,
            System.StringComparison.Ordinal);
        // Debe crear el botón Cambiar (que re-abre el modal).
        Assert.Contains(
            "data-usuario-persona-buscar",
            renderBody,
            System.StringComparison.Ordinal);
        // El botón Cambiar debe llevar data-bs-toggle y data-bs-target con `#`.
        Assert.Contains(
            "data-bs-toggle",
            renderBody,
            System.StringComparison.Ordinal);
        // En el JS se construye como `'#' + modal.id` (concatenación).
        Assert.Contains(
            "'#' + modal.id",
            renderBody,
            System.StringComparison.Ordinal);
        Assert.Contains(
            "data-bs-target",
            renderBody,
            System.StringComparison.Ordinal);
    }

    // ──────────────────────────────────────────────────────────────────
    // USBJS-03 revisión: handleQuitar es una función nombrada reusable.
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Script_DefinesHandleQuitarFunction()
    {
        var source = ReadScriptSource();

        var handleFn = Regex.Match(
            source,
            @"function\s+handleQuitar\s*\(\s*\)\s*\{",
            RegexOptions.Multiline);
        Assert.True(
            handleFn.Success,
            "El script debe definir `function handleQuitar()` como "
            + "función nombrada. Esto permite reutilizarla desde el "
            + "render dinámico del Caso 6 (los botones Quitar creados "
            + "dinámicamente bindean este mismo handler).");
    }

    [Fact]
    public void InitialQuitarButtons_BindToHandleQuitar()
    {
        var source = ReadScriptSource();

        // El forEach inicial debe pasar handleQuitar como callback (no
        // una función anónima con lógica propia). Verificamos que la
        // callback invoca `handleQuitar` directamente.
        var foreachMatch = Regex.Match(
            source,
            @"querySelectorAll\('\[data-usuario-persona-quitar\]'\)\.forEach\([\s\S]*?\);",
            RegexOptions.Multiline);
        Assert.True(
            foreachMatch.Success,
            "No se encontró el forEach que bindea los botones Quitar iniciales.");
        var foreachBody = foreachMatch.Value;

        // Dentro del callback debe aparecer `addEventListener('click', handleQuitar)`.
        Assert.Contains(
            "addEventListener('click', handleQuitar)",
            foreachBody,
            System.StringComparison.Ordinal);
    }

    [Fact]
    public void HandleQuitar_ClearsDynamicDisplayAndShowsEmpty()
    {
        var source = ReadScriptSource();

        var handleFn = Regex.Match(
            source,
            @"function\s+handleQuitar\s*\(\s*\)\s*\{[\s\S]*?\n\s{4}\}",
            RegexOptions.Multiline);
        Assert.True(handleFn.Success, "No se encontró handleQuitar.");
        var handleBody = handleFn.Value;

        // Debe limpiar el display dinámico (replaceChildren) y mostrar el empty.
        Assert.Contains(
            "display.replaceChildren",
            handleBody,
            System.StringComparison.Ordinal);
        Assert.Contains(
            "empty.hidden = false",
            handleBody,
            System.StringComparison.Ordinal);

        // Debe limpiar hiddenInput y currentPersonaId siempre.
        Assert.Contains(
            "hiddenInput.value = ''",
            handleBody,
            System.StringComparison.Ordinal);
        Assert.Contains(
            "modal.dataset.currentPersonaId = ''",
            handleBody,
            System.StringComparison.Ordinal);

        // Debe disparar el change.
        Assert.Contains(
            "hiddenInput.dispatchEvent(new Event('change'",
            handleBody,
            System.StringComparison.Ordinal);
    }
}
