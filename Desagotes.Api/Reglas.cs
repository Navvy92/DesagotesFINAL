namespace Desagotes.Api;

public static class Reglas
{
    // 0 = sin plazo (etapa piloto). Se configura en appsettings.json
    public static int PlazoHorasRemitos { get; set; } = 48;
    public static bool HayPlazo => PlazoHorasRemitos > 0;
}