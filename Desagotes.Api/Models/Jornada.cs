using System;
using System.Collections.Generic;

namespace Desagotes.Api.Models;

public partial class Jornada
{
    public int Id { get; set; }

    public int CamioneroId { get; set; }

    public int VehiculoId { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime CheckinAt { get; set; }

    public string CheckinFoto { get; set; } = null!;

    public double CheckinLat { get; set; }

    public double CheckinLng { get; set; }

    public float? CheckinPrecisionM { get; set; }

    public DateTime? CheckoutAt { get; set; }

    public string? CheckoutFoto { get; set; }

    public int? RemitosDeclarados { get; set; }

    public virtual Camionero Camionero { get; set; } = null!;

    public virtual ICollection<Remito> Remitos { get; set; } = new List<Remito>();

    public virtual Vehiculo Vehiculo { get; set; } = null!;

    public bool CierreAdmin { get; set; }
    public string? CierreAdminPor { get; set; }
    public DateTime? CierreAdminAt { get; set; }
    public string? NotaAdmin { get; set; }
}
