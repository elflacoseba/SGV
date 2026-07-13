using System.Reflection;
using SGV.Dominio.Habilidades;
using SGV.Dominio.Organizacion;
using SGV.Dominio.Ocupaciones;
using SGV.Dominio.Personas;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Reflection guard: asserts that <c>PersistenceToDomainMapper.ToDomain(TEntity)</c>
/// for Cargo does NOT call the internal <c>SetProperty</c> helper that uses
/// <see cref="PropertyInfo.SetValue(object, object?)"/> with
/// <see cref="BindingFlags.NonPublic"/>. That helper bypasses the C# init-only
/// modifier at runtime and is the debt addressed by issue #124.
/// </summary>
public sealed class CargoMapperTests
{
    [Fact]
    public void ToDomain_Cargo_NoLlamaSetPropertyReflectionHelper()
    {
        var assembly = typeof(CargoRepository).Assembly;
        var mapperType = assembly.GetType(
            "SGV.Infraestructura.Persistencia.Mapeos.PersistenceToDomainMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            "ToDomain",
            new[] { typeof(CargoEntity) })
            ?? throw new InvalidOperationException(
                "PersistenceToDomainMapper.ToDomain(CargoEntity) not found.");
        var methodBody = method.GetMethodBody()
            ?? throw new InvalidOperationException(
                "ToDomain has no IL body (abstract/extern?).");
        var il = methodBody.GetILAsByteArray()
            ?? throw new InvalidOperationException(
                "ToDomain IL body returned no bytes.");
        var module = method.Module;

        MethodInfo? setPropertyCall = null;
        for (var i = 0; i < il.Length; i++)
        {
            // call = 0x28, callvirt = 0x6F. Both consume a 4-byte metadata token.
            if ((il[i] != 0x28 && il[i] != 0x6F) || i + 4 >= il.Length)
            {
                continue;
            }

            var token = BitConverter.ToInt32(il, i + 1);
            try
            {
                if (module.ResolveMethod(token) is MethodInfo called
                    && called.Name == "SetProperty"
                    && called.DeclaringType == mapperType)
                {
                    setPropertyCall = called;
                    break;
                }
            }
            catch (ArgumentException)
            {
                // Token may resolve to a field reference (ld*fld) rather than a method.
            }

            i += 4;
        }

        Assert.Null(setPropertyCall);
    }

    // NOTE: Behavior tests for Cargo.Reconstitute are added in CU-3 alongside the
    // implementation. CU-1 only delivers the RED IL guard.
}