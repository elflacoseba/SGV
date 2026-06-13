namespace SGV.Dominio.Comun;

public static class ValidacionesDominio
{
    public static string Requerido(string valor, string nombreCampo, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException($"El campo {nombreCampo} es obligatorio.", nombreCampo);
        }

        var normalizado = valor.Trim();
        if (normalizado.Length > longitudMaxima)
        {
            throw new ArgumentException($"El campo {nombreCampo} no puede superar {longitudMaxima} caracteres.", nombreCampo);
        }

        return normalizado;
    }

    public static string? Opcional(string? valor, string nombreCampo, int longitudMaxima)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim();
        if (normalizado.Length > longitudMaxima)
        {
            throw new ArgumentException($"El campo {nombreCampo} no puede superar {longitudMaxima} caracteres.", nombreCampo);
        }

        return normalizado;
    }

    public static decimal Porcentaje(decimal valor, string nombreCampo)
    {
        if (valor < 0 || valor > 100)
        {
            throw new ArgumentOutOfRangeException(nombreCampo, "El porcentaje debe estar entre 0 y 100.");
        }

        return valor;
    }
}
