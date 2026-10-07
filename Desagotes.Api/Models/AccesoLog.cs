namespace Desagotes.Api.Models;

public class AccesoLog
{
    public int Id { get; set; }
    public int? CamioneroId { get; set; }
    public string Evento { get; set; } = null!;
    public string? Ip { get; set; }
    public string? Dispositivo { get; set; }
    public DateTime CreadoAt { get; set; }
}