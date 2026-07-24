namespace SGV.Contracts.Setup;

/// <summary>
/// Resultado exitoso del setup one-time. Expone los identificadores
/// del <see cref="PersonaId"/> recién creado y del
/// <see cref="UserId"/> Identity asociado, junto al <see cref="UserName"/>
/// final para que la Web pueda mostrarlo tras el PRG al SignIn.
/// </summary>
public sealed record SetupResult(Guid PersonaId, string UserId, string UserName);
