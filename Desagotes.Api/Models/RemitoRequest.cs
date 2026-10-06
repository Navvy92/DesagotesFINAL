namespace Desagotes.Api.Models;

public class RemitoRequest
{
    public int JornadaId { get; set; }
    public int Talonario { get; set; }
    public int NroRemito { get; set; }
    public string Cliente { get; set; } = "";
    public string? NroPedido { get; set; }
    public IFormFile FotoRemito { get; set; } = null!;
    public IFormFile FotoCamara { get; set; } = null!;
}