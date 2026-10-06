using System;
using System.Collections.Generic;

namespace Desagotes.Api.Models;

public partial class Remito
{
    public int Id { get; set; }

    public int JornadaId { get; set; }

    public string? NroPedido { get; set; }

    public string FotoRemito { get; set; } = null!;

    public string FotoCamara { get; set; } = null!;

    public DateTime CargadoAt { get; set; }

    public int Talonario { get; set; }

    public int NroRemito { get; set; }

    public string Cliente { get; set; } = null!;

    public virtual Jornada Jornada { get; set; } = null!;
}
