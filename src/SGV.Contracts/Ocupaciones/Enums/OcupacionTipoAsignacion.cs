using System.Text.Json.Serialization;

namespace SGV.Contracts.Ocupaciones.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OcupacionTipoAsignacion
{
    Permanente = 0,
    Interina = 1,
    Temporal = 2
}
