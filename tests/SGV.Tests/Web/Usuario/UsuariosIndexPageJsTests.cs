using System.Diagnostics;
using System.Text.Json;
using SGV.Tests.Web.Collections;
using Xunit;

namespace SGV.Tests.Web.Usuario;

/// <summary>
/// Harness Node-based tests for <c>src/SGV.Web/wwwroot/js/pages/usuarios-index.js</c>
/// introduced by PR 2 of the change
/// <c>2026-07-17-fix-popups-usuarios-riesgos</c>. Cubre los tres
/// <c>wire*Confirmation</c> (Bloquear / Desbloquear / Eliminar) y los tres
/// escenarios canónicos del contrato REQ-UCB-01, REQ-UCB-02 y REQ-ULD-05:
/// confirmado, cancelado por botón Cancelar y descartado por Esc o
/// backdrop. Espejo estructural de <see cref="SGV.Tests.Web.Cargo.CargoIndexPageTests"/>
/// y <see cref="SGV.Tests.Web.Puesto.PuestoIndexPageTests"/>.
/// </summary>
[Collection("WebIntegration")]
public sealed class UsuariosIndexPageJsTests
{
    private readonly WebIntegrationFixture _fixture;

    public UsuariosIndexPageJsTests(WebIntegrationFixture fixture) => _fixture = fixture;

    // ──────────────────────────────────────────────
    // Bloquear
    // ──────────────────────────────────────────────

    [Fact]
    public async Task WireUsuarioBloquearConfirmation_WhenConfirmed_SubmitsFormOnce()
    {
        // REQ-UCB-01 escenario "Confirmar bloquea": isConfirmed=true debe
        // disparar exactamente un submit del form data-usuario-bloquear-form
        // y la configuración de Swal.fire debe coincidir con la canónica.
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Bloquear,
            dismiss: null);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Bloquear usuario", result.Title);
        Assert.Equal("Bloquear", result.ConfirmButtonText);
        Assert.Equal("Cancelar", result.CancelButtonText);
        Assert.Equal("warning", result.Icon);
        Assert.True(result.ShowCancelButton);
        Assert.True(result.ReverseButtons);
        Assert.True(result.FocusCancel);
        Assert.True(result.AllowEscapeKey);
        Assert.True(result.AllowOutsideClick);
        Assert.Null(result.LastDismiss);
        Assert.Equal("btn btn-secondary", result.ConfirmButtonClass);
        Assert.Equal("btn btn-light", result.CancelButtonClass);
    }

    [Fact]
    public async Task WireUsuarioBloquearConfirmation_WhenCancelled_DoesNotSubmitForm()
    {
        // REQ-UCB-01 escenario "Cancelar no bloquea": dismiss='cancel' no
        // dispara submit ni deja el form en estado intermedio.
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Bloquear,
            dismiss: "cancel");

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("cancel", result.LastDismiss);
        Assert.True(result.ShowCancelButton);
        Assert.True(result.ReverseButtons);
    }

    [Theory]
    [InlineData("esc")]
    [InlineData("backdrop")]
    public async Task WireUsuarioBloquearConfirmation_WhenDismissedByEscOrBackdrop_DoesNotSubmitForm(string dismiss)
    {
        // REQ-UCB-01 escenario "Esc/backdrop": el handler debe ignorar el
        // descarte y no enviar el form. Verificamos que el mock devolvió
        // el dismiss correcto Y que submit no se invocó.
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Bloquear,
            dismiss: dismiss);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal(dismiss, result.LastDismiss);
    }

    // ──────────────────────────────────────────────
    // Desbloquear
    // ──────────────────────────────────────────────

    [Fact]
    public async Task WireUsuarioDesbloquearConfirmation_WhenConfirmed_SubmitsFormOnce()
    {
        // REQ-UCB-02 escenario "Confirmar desbloquea": contrato análogo a
        // Bloquear pero con botón Confirmar btn-success.
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Desbloquear,
            dismiss: null);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Desbloquear usuario", result.Title);
        Assert.Equal("Desbloquear", result.ConfirmButtonText);
        Assert.Equal("Cancelar", result.CancelButtonText);
        Assert.Equal("warning", result.Icon);
        Assert.True(result.ShowCancelButton);
        Assert.True(result.ReverseButtons);
        Assert.True(result.FocusCancel);
        Assert.Null(result.LastDismiss);
        Assert.Equal("btn btn-success", result.ConfirmButtonClass);
        Assert.Equal("btn btn-light", result.CancelButtonClass);
    }

    [Fact]
    public async Task WireUsuarioDesbloquearConfirmation_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Desbloquear,
            dismiss: "cancel");

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("cancel", result.LastDismiss);
        Assert.True(result.ReverseButtons);
    }

    [Theory]
    [InlineData("esc")]
    [InlineData("backdrop")]
    public async Task WireUsuarioDesbloquearConfirmation_WhenDismissedByEscOrBackdrop_DoesNotSubmitForm(string dismiss)
    {
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Desbloquear,
            dismiss: dismiss);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal(dismiss, result.LastDismiss);
    }

    // ──────────────────────────────────────────────
    // Eliminar
    // ──────────────────────────────────────────────

    [Fact]
    public async Task WireUsuarioDeleteConfirmation_WhenConfirmed_SubmitsFormOnce()
    {
        // REQ-ULD-05 escenario "Confirmar elimina": botón
        // 'Eliminar definitivamente' mapea a btn-danger y el texto
        // advierte la irreversibilidad sin exponer PII.
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Eliminar,
            dismiss: null);

        Assert.Equal(1, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("Eliminar usuario", result.Title);
        Assert.Equal("Eliminar definitivamente", result.ConfirmButtonText);
        Assert.Equal("Cancelar", result.CancelButtonText);
        Assert.Equal("warning", result.Icon);
        Assert.True(result.ShowCancelButton);
        Assert.True(result.ReverseButtons);
        Assert.True(result.FocusCancel);
        Assert.Null(result.LastDismiss);
        Assert.Equal("btn btn-danger", result.ConfirmButtonClass);
        Assert.Equal("btn btn-light", result.CancelButtonClass);
        Assert.DoesNotContain("agarcía", result.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jperez", result.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WireUsuarioDeleteConfirmation_WhenCancelled_DoesNotSubmitForm()
    {
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Eliminar,
            dismiss: "cancel");

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal("cancel", result.LastDismiss);
        Assert.True(result.ReverseButtons);
    }

    [Theory]
    [InlineData("esc")]
    [InlineData("backdrop")]
    public async Task WireUsuarioDeleteConfirmation_WhenDismissedByEscOrBackdrop_DoesNotSubmitForm(string dismiss)
    {
        var result = await ExecuteUsuarioConfirmationScriptAsync(
            UsuarioConfirmationKind.Eliminar,
            dismiss: dismiss);

        Assert.Equal(0, result.SubmitCount);
        Assert.True(result.PreventDefaultCalled);
        Assert.Equal(dismiss, result.LastDismiss);
    }

    // ──────────────────────────────────────────────
    // Helpers de soporte (JS harness)
    // ──────────────────────────────────────────────

    private enum UsuarioConfirmationKind
    {
        Bloquear,
        Desbloquear,
        Eliminar
    }

    private sealed record UsuarioScriptExecutionResult(
        int SubmitCount,
        bool PreventDefaultCalled,
        bool ShowCancelButton,
        bool ReverseButtons,
        bool FocusCancel,
        bool AllowEscapeKey,
        bool AllowOutsideClick,
        string? Title,
        string? Text,
        string? Icon,
        string? ConfirmButtonText,
        string? CancelButtonText,
        string? ConfirmButtonClass,
        string? CancelButtonClass,
        string? LastDismiss);

    private static async Task<UsuarioScriptExecutionResult> ExecuteUsuarioConfirmationScriptAsync(
        UsuarioConfirmationKind kind,
        string? dismiss)
    {
        var scriptConfig = kind switch
        {
            UsuarioConfirmationKind.Bloquear => new
            {
                Export = "wireUsuarioBloquearConfirmation",
                FormSelector = "[data-usuario-bloquear-form]",
                ButtonSelector = "[data-usuario-bloquear-button]",
                ErrorMessage = "Usuario bloquear confirmation click handler was not wired.",
                HarnessPrefix = "usuario-bloquear-confirmation"
            },
            UsuarioConfirmationKind.Desbloquear => new
            {
                Export = "wireUsuarioDesbloquearConfirmation",
                FormSelector = "[data-usuario-desbloquear-form]",
                ButtonSelector = "[data-usuario-desbloquear-button]",
                ErrorMessage = "Usuario desbloquear confirmation click handler was not wired.",
                HarnessPrefix = "usuario-desbloquear-confirmation"
            },
            UsuarioConfirmationKind.Eliminar => new
            {
                Export = "wireUsuarioDeleteConfirmation",
                FormSelector = "[data-usuario-delete-form]",
                ButtonSelector = "[data-usuario-delete-button]",
                ErrorMessage = "Usuario delete confirmation click handler was not wired.",
                HarnessPrefix = "usuario-delete-confirmation"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/SGV.Web/wwwroot/js/pages/usuarios-index.js"));
        var harnessPath = Path.Combine(
            Path.GetTempPath(),
            $"{scriptConfig.HarnessPrefix}-{Guid.NewGuid():N}.cjs");

        // El mock de Swal.fire devuelve { isConfirmed: true } cuando dismiss es null,
        // o { isConfirmed: false, dismiss: <dismiss> } en cualquier otro caso. Esto
        // reproduce el contrato real de SweetAlert2 v11.x. El mock guarda el último
        // dismiss devuelto en lastReturnedDismiss para que el harness pueda
        // serializarlo y el test assertar que el contrato se respeta.
        var resolvedResult = dismiss is null
            ? "{ isConfirmed: true }"
            : $"{{ isConfirmed: false, dismiss: {JsonSerializer.Serialize(dismiss)} }}";

        var harnessSource = $$"""
const { {{scriptConfig.Export}} } = require({{JsonSerializer.Serialize(scriptPath)}});

let clickHandler = null;
let submitCount = 0;
let preventDefaultCalled = false;
let swalConfig = null;
let lastReturnedDismiss = null;

const button = {
  addEventListener(type, handler) {
    if (type === 'click') {
      clickHandler = handler;
    }
  }
};

const form = {
  querySelector(selector) {
    return selector === {{JsonSerializer.Serialize(scriptConfig.ButtonSelector)}} ? button : null;
  },
  submit() {
    submitCount += 1;
  }
};

const root = {
  querySelectorAll(selector) {
    return selector === {{JsonSerializer.Serialize(scriptConfig.FormSelector)}} ? [form] : [];
  }
};

const Swal = {
  fire(config) {
    swalConfig = config;
    var result = {{resolvedResult}};
    lastReturnedDismiss = result.dismiss === undefined ? null : result.dismiss;
    return Promise.resolve(result);
  }
};

async function main() {
  {{scriptConfig.Export}}(root, Swal);

  if (!clickHandler) {
    throw new Error({{JsonSerializer.Serialize(scriptConfig.ErrorMessage)}});
  }

  clickHandler({
    preventDefault() {
      preventDefaultCalled = true;
    }
  });

  await Promise.resolve();
  await Promise.resolve();

  var confirmClass = (swalConfig && swalConfig.customClass && swalConfig.customClass.confirmButton) || null;
  var cancelClass = (swalConfig && swalConfig.customClass && swalConfig.customClass.cancelButton) || null;

  process.stdout.write(JSON.stringify({
    submitCount: submitCount,
    preventDefaultCalled: preventDefaultCalled,
    showCancelButton: Boolean(swalConfig && swalConfig.showCancelButton),
    reverseButtons: Boolean(swalConfig && swalConfig.reverseButtons),
    focusCancel: Boolean(swalConfig && swalConfig.focusCancel),
    allowEscapeKey: Boolean(swalConfig && swalConfig.allowEscapeKey),
    allowOutsideClick: Boolean(swalConfig && swalConfig.allowOutsideClick),
    title: swalConfig ? swalConfig.title : null,
    text: swalConfig ? swalConfig.text : null,
    icon: swalConfig ? swalConfig.icon : null,
    confirmButtonText: swalConfig ? swalConfig.confirmButtonText : null,
    cancelButtonText: swalConfig ? swalConfig.cancelButtonText : null,
    confirmButtonClass: confirmClass,
    cancelButtonClass: cancelClass,
    lastDismiss: lastReturnedDismiss
  }));
}

main().catch(error => {
  process.stderr.write(error.stack || String(error));
  process.exit(1);
});
""";

        await File.WriteAllTextAsync(harnessPath, harnessSource);

        try
        {
            var startInfo = new ProcessStartInfo("node", $"\"{harnessPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            var standardOutput = await process.StandardOutput.ReadToEndAsync();
            var standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(
                process.ExitCode == 0,
                $"Node harness failed with exit code {process.ExitCode}: {standardError}");

            var result = JsonSerializer.Deserialize<UsuarioScriptExecutionResult>(
                standardOutput,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            Assert.NotNull(result);
            return result!;
        }
        finally
        {
            if (File.Exists(harnessPath))
            {
                File.Delete(harnessPath);
            }
        }
    }
}