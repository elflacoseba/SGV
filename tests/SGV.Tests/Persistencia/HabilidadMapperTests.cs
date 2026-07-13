using System.Reflection;
using SGV.Dominio.Habilidades;
using SGV.Infraestructura.Persistencia.Entidades;
using SGV.Infraestructura.Persistencia.Repositorios;
using Xunit;

namespace SGV.Tests.Persistencia;

/// <summary>
/// Reflection guard: asserts that <c>PersistenceToDomainMapper.ToDomain(HabilidadEntity)</c>
/// does NOT call the internal <c>SetProperty</c> reflection helper. See issue #124.
/// </summary>
public sealed class HabilidadMapperTests
{
    [Fact]
    public void ToDomain_Habilidad_NoLlamaSetPropertyReflectionHelper()
    {
        var assembly = typeof(HabilidadRepository).Assembly;
        var mapperType = assembly.GetType(
            "SGV.Infraestructura.Persistencia.Mapeos.PersistenceToDomainMapper",
            throwOnError: true)!;
        var method = mapperType.GetMethod(
            "ToDomain",
            new[] { typeof(HabilidadEntity) })
            ?? throw new InvalidOperationException(
                "PersistenceToDomainMapper.ToDomain(HabilidadEntity) not found.");
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
}