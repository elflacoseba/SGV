using System.Text.Json.Serialization;

namespace SGV.Contracts.Ocupaciones.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OcupacionEstado
{
    Vigente = 0,
    Finalizada = 1,
    Eliminada = 2
}
