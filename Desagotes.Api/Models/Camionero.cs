using System;
using System.Collections.Generic;

namespace Desagotes.Api.Models;

public partial class Camionero
{
    public string? PinHash { get; set; }
    public int PinVersion { get; set; }
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }

    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public bool Activo { get; set; }

    public virtual Jornada? Jornadum { get; set; }
}
