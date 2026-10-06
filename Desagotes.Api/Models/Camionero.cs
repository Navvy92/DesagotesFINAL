using System;
using System.Collections.Generic;

namespace Desagotes.Api.Models;

public partial class Camionero
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public bool Activo { get; set; }

    public virtual Jornada? Jornadum { get; set; }
}
