using Xunit;

namespace SGV.Tests.Setup;

/// <summary>
/// Colección xUnit que serializa los tests de
/// <see cref="SetupServicioTests"/> porque comparten la base
/// <c>sgv_test</c> y la limpian/siembran entre tests. Sin esta
/// colección xUnit paraleliza entre clases y los tests fallan
/// de forma intermitente por colisiones de estado (FK
/// huérfanas, <c>Personas</c> insertadas por tests vecinos, etc).
/// </summary>
[CollectionDefinition("SetupServicio")]
public sealed class SetupServicioCollection
{
}
