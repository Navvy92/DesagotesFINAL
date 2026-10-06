using ClosedXML.Excel;
using Desagotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Desagotes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/exportar")]
public class ExportController : ControllerBase
{
    // Argentina no usa horario de verano: UTC-3 todo el año
    private static readonly TimeSpan Argentina = TimeSpan.FromHours(-3);
    private const int MaxDias = 93;
    private const int ColumnasPlanilla = 16;   // A..P: las que pega tu compañero

    private readonly DesagotesContext _db;

    public ExportController(DesagotesContext db)
    {
        _db = db;
    }

    private record Fila(int Talonario, int NroRemito, string Cliente, string? NroPedido,
                        DateTime Entrada, DateTime? Salida, string Camionero, string Patente, string Estado);

    private record FilaJornada(string Camionero, string Patente, DateTime Entrada, DateTime? Salida,
                               string Estado, int? Declarados, int Cargados, bool CierreAdmin, string? Nota);

    // De UTC (como se guarda) a hora argentina, sin zona, para que Excel no la convierta
    private static DateTime Local(DateTime utc) =>
        DateTime.SpecifyKind(utc + Argentina, DateTimeKind.Unspecified);

    private static double? Horas(DateTime entrada, DateTime? salida) =>
        salida == null ? null : Math.Round((salida.Value - entrada).TotalHours, 2);

    // Las mismas horas como fracción de día, para mostrarlas como hh:mm en Excel
    private static double? HorasHHMM(DateTime entrada, DateTime? salida) =>
        salida == null ? null : (salida.Value - entrada).TotalHours / 24.0;

    // Formato de su planilla: AAMM + día SIN cero adelante + talonario-remito
    private static string CodImag(Fila f) =>
        $"{Local(f.Entrada).ToString("yyMM d", CultureInfo.InvariantCulture)} {f.Talonario}-{f.NroRemito}";

    // El pedido como número si son solo dígitos (su planilla lo tiene numérico)
    private static object? Pedido(string? pedido)
    {
        if (long.TryParse(pedido, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            return n;
        return pedido;
    }

    private static void EscribirTabla<T>(IXLWorksheet hoja, IReadOnlyList<T> filas,
        (string Titulo, Func<T, object?> Valor, string? Formato)[] columnas, int columnasVerdes)
    {
        for (var c = 0; c < columnas.Length; c++)
            hoja.Cell(1, c + 1).Value = columnas[c].Titulo;

        var encabezado = hoja.Range(1, 1, 1, columnas.Length);
        encabezado.Style.Font.Bold = true;
        encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
        if (columnasVerdes > 0)
            hoja.Range(1, 1, 1, columnasVerdes).Style.Fill.BackgroundColor = XLColor.FromHtml("#E2F0D9");

        for (var i = 0; i < filas.Count; i++)
        {
            for (var c = 0; c < columnas.Length; c++)
            {
                var valor = columnas[c].Valor(filas[i]);
                if (valor == null) continue;   // celda en blanco

                var celda = hoja.Cell(i + 2, c + 1);
                celda.Value = XLCellValue.FromObject(valor);
                if (columnas[c].Formato != null)
                    celda.Style.NumberFormat.Format = columnas[c].Formato!;
            }
        }

        hoja.SheetView.FreezeRows(1);
        hoja.Range(1, 1, filas.Count + 1, columnas.Length).SetAutoFilter();
        hoja.Columns().AdjustToContents();
    }

    [HttpGet("remitos")]
    public async Task<IActionResult> Remitos(string? desde, string? hasta, string? cliente)
    {
        const string formato = "yyyy-MM-dd";
        if (!DateOnly.TryParseExact(desde, formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ||
            !DateOnly.TryParseExact(hasta, formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var h))
            return BadRequest(new { mensaje = "Elegí las fechas desde y hasta." });

        if (h < d)
            return BadRequest(new { mensaje = "La fecha 'hasta' es anterior a 'desde'." });

        if (h.DayNumber - d.DayNumber > MaxDias)
            return BadRequest(new { mensaje = $"El rango máximo es de {MaxDias} días." });

        // Los días se interpretan en hora argentina y se pasan a UTC para consultar
        var desdeUtc = new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), Argentina).UtcDateTime;
        var hastaUtc = new DateTimeOffset(h.AddDays(1).ToDateTime(TimeOnly.MinValue), Argentina).UtcDateTime;

        var cli = string.IsNullOrWhiteSpace(cliente) ? null : cliente.Trim().ToUpperInvariant();
        var remitos = _db.Remitos.AsQueryable();
        if (cli != null)
            remitos = remitos.Where(r => r.Cliente == cli);

        var datos = await (
            from r in remitos
            join j in _db.Jornada on r.JornadaId equals j.Id
            join c in _db.Camioneros on j.CamioneroId equals c.Id
            join v in _db.Vehiculos on j.VehiculoId equals v.Id
            where j.CheckinAt >= desdeUtc && j.CheckinAt < hastaUtc
            orderby j.CheckinAt, r.Talonario, r.NroRemito
            select new
            {
                r.Talonario,
                r.NroRemito,
                r.Cliente,
                r.NroPedido,
                Entrada = j.CheckinAt,
                Salida = j.CheckoutAt,
                Camionero = c.Nombre,
                v.Patente,
                j.Estado
            }).ToListAsync();

        // Una fila por jornada (incluye las que no tienen remitos). No usa el filtro de cliente.
        var datosJornadas = await (
            from j in _db.Jornada
            join c in _db.Camioneros on j.CamioneroId equals c.Id
            join v in _db.Vehiculos on j.VehiculoId equals v.Id
            where j.CheckinAt >= desdeUtc && j.CheckinAt < hastaUtc
            orderby j.CheckinAt
            select new
            {
                Camionero = c.Nombre,
                v.Patente,
                Entrada = j.CheckinAt,
                Salida = j.CheckoutAt,
                j.Estado,
                Declarados = j.RemitosDeclarados,
                Cargados = _db.Remitos.Count(r => r.JornadaId == j.Id),
                j.CierreAdmin,
                j.NotaAdmin
            }).ToListAsync();

        if (datos.Count == 0 && datosJornadas.Count == 0)
            return NotFound(new { mensaje = "No hay remitos ni jornadas en ese rango." });

        var filas = datos
            .Select(x => new Fila(x.Talonario, x.NroRemito, x.Cliente, x.NroPedido,
                                  x.Entrada, x.Salida, x.Camionero, x.Patente, x.Estado))
            .ToList();

        var jornadas = datosJornadas
            .Select(x => new FilaJornada(x.Camionero, x.Patente, x.Entrada, x.Salida, x.Estado,
                                         x.Declarados, x.Cargados, x.CierreAdmin, x.NotaAdmin))
            .ToList();

        const string fHora = "d/m/yyyy hh:mm";

        // ---------- Hoja 1: Remitos (A..P idénticas a la planilla; el resto es referencia) ----------
        var columnas = new (string Titulo, Func<Fila, object?> Valor, string? Formato)[]
        {
            ("COD_IMAG",      f => CodImag(f), null),
            ("N°",            f => f.Talonario, null),
            ("REMITO",        f => f.NroRemito, null),
            ("N°",            f => $"{f.Talonario}-{f.NroRemito}", null),
            ("FECHA",         f => Local(f.Entrada).Date, "d/m/yyyy"),
            ("ID",            f => f.Cliente == "TELECOM" ? Pedido(f.NroPedido) : null, null),
            ("OT_CAM",        f => null, null),
            ("INF.",          f => null, null),
            ("DOM",           f => f.Patente, null),
            ("CHOFER",        f => f.Camionero, null),
            ("Observacion",   f => null, null),
            ("ID_OBRA_CL",    f => null, null),
            ("PEDIDO TMA",    f => f.Cliente == "TELEFONICA" ? Pedido(f.NroPedido) : null, null),
            ("DIRECCION",     f => null, null),
            ("LOCALIDAD",     f => null, null),
            ("DESCRIPCION",   f => null, null),
            // Referencia (no se pegan): sirven para ver el horario de la jornada
            ("CLIENTE (ref)",              f => f.Cliente, null),
            ("ESTADO (ref)",               f => f.Estado, null),
            ("ENTRADA (ref)",              f => Local(f.Entrada), fHora),
            ("SALIDA (ref)",               f => f.Salida == null ? null : Local(f.Salida.Value), fHora),
            ("HORAS DE LA JORNADA (ref)",  f => Horas(f.Entrada, f.Salida), "0.00"),
        };

        using var libro = new XLWorkbook();
        EscribirTabla(libro.Worksheets.Add("Remitos"), filas, columnas, ColumnasPlanilla);

        // ---------- Hoja 2: Jornadas (una fila por jornada: acá se suman las horas) ----------
        var colsJornada = new (string Titulo, Func<FilaJornada, object?> Valor, string? Formato)[]
        {
            ("FECHA",              j => Local(j.Entrada).Date, "d/m/yyyy"),
            ("CHOFER",             j => j.Camionero, null),
            ("DOM",                j => j.Patente, null),
            ("ENTRADA",            j => Local(j.Entrada), fHora),
            ("SALIDA",             j => j.Salida == null ? null : Local(j.Salida.Value), fHora),
            ("HORAS",              j => Horas(j.Entrada, j.Salida), "0.00"),
            ("HORAS (hh:mm)",      j => HorasHHMM(j.Entrada, j.Salida), "[h]:mm"),
            ("REMITOS DECLARADOS", j => j.Declarados, null),
            ("REMITOS CARGADOS",   j => j.Cargados, null),
            ("ESTADO",             j => j.Estado, null),
            ("CERRADA POR ADMIN",  j => j.CierreAdmin ? "SÍ" : null, null),
            ("NOTA ADMIN",         j => j.Nota, null),
        };

        EscribirTabla(libro.Worksheets.Add("Jornadas"), jornadas, colsJornada, 0);

        using var ms = new MemoryStream();
        libro.SaveAs(ms);

        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"remitos_{d:yyyyMMdd}_{h:yyyyMMdd}.xlsx");
    }
}