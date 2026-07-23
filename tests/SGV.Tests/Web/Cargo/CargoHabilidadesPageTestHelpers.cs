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

public sealed partial class CargoHabilidadesPageTests
{
    private static FormUrlEncodedContent BuildAsignarForm(
        string antiforgeryToken,
        Guid skillId,
        Guid nivelId,
        string ponderacion = "50.00") =>
        new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["AsignarInput.SkillId"] = skillId.ToString(),
            ["AsignarInput.NivelRequeridoId"] = nivelId.ToString(),
            ["AsignarInput.Ponderacion"] = ponderacion
        });

    /// <summary>
    /// Construye el form body para POST a <c>?handler=Actualizar</c> con
    /// la convención indexada <c>Actualizar[{skillId}].Campo</c> que el
    /// <c>design.md</c> sección 4 fija. La fila se identifica por su
    /// <paramref name="skillId"/>; los campos editables viajan bajo el
    /// prefijo para que el PageModel pueda anclar errores por fila sin
    /// bindear un dictionary completo (ver nota en
    /// <see cref="ApplyActualizarFailureToModelState"/> sobre la decisión
    /// de extraer manualmente desde <c>Request.Form</c>).
    /// </summary>
    private static FormUrlEncodedContent BuildActualizarForm(
        string antiforgeryToken,
        Guid skillId,
        Guid nivelId,
        string ponderacion = "50.00",
        string esObligatoria = "true") =>
        new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            [$"Actualizar[{skillId}].NivelRequeridoId"] = nivelId.ToString(),
            [$"Actualizar[{skillId}].Ponderacion"] = ponderacion,
            [$"Actualizar[{skillId}].EsObligatoria"] = esObligatoria
        });
}
