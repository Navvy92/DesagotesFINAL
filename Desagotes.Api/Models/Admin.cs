using System;
using System.Collections.Generic;

namespace Desagotes.Api.Models;

public partial class Admin
{
    public int Id { get; set; }

    public string Usuario { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;
}
