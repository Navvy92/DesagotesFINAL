namespace Desagotes.Api.Models;

public class CheckinRequest
{
    public int CamioneroId { get; set; }
    public int VehiculoId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public float? PrecisionM { get; set; }
    public IFormFile Foto { get; set; } = null!;
}