namespace Desagotes.Api.Models;

public class CheckoutRequest
{
    public int CamioneroId { get; set; }
    public int RemitosDeclarados { get; set; }
    public IFormFile Foto { get; set; } = null!;
}