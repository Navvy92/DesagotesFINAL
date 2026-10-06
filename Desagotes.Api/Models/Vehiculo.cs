using System;
using System.Collections.Generic;

namespace Desagotes.Api.Models;

public partial class Vehiculo
{
    public int Id { get; set; }

    public string Patente { get; set; } = null!;

    public bool Activo { get; set; }

    public virtual Jornada? Jornadum { get; set; }
}
