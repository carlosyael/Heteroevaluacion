using ClosedXML.Excel;
using HeteroevaluacionApp.Data;
using HeteroevaluacionApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HeteroevaluacionApp.Services;

public record ImportResult(int Added, int Updated, int Skipped, List<string> Errors);

public class StudentImportService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public StudentImportService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ImportResult> ImportFromStreamAsync(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var rows = new List<(string Id, string Nombre, string Carrera, string TemaTesis)>();
        var errors = new List<string>();

        if (ext == ".csv")
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine))
                return new ImportResult(0, 0, 0, new() { "El archivo CSV está vacío." });

            var separator = headerLine.Contains(';') ? ';' : ',';
            var headers = SplitCsvLine(headerLine, separator).Select(NormalizeHeader).ToList();

            int idxId = FindColumn(headers, "id", "matricula", "codigo");
            int idxNombre = FindColumn(headers, "nombre", "estudiante", "alumno", "name");
            int idxCarrera = FindColumn(headers, "carrera", "programa", "career");
            int idxTema = FindColumn(headers, "tematesis", "tema", "tesis", "temadetesis");

            if (idxId < 0 || idxNombre < 0 || idxCarrera < 0 || idxTema < 0)
            {
                return new ImportResult(0, 0, 0, new()
                {
                    "El CSV debe contener las columnas: Id, Nombre, Carrera, TemaTesis."
                });
            }

            int lineNum = 1;
            while (!reader.EndOfStream)
            {
                lineNum++;
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = SplitCsvLine(line, separator);
                string id = GetCol(cols, idxId);
                string nombre = GetCol(cols, idxNombre);
                string carrera = GetCol(cols, idxCarrera);
                string tema = GetCol(cols, idxTema);

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nombre))
                {
                    errors.Add($"Fila {lineNum}: Id y Nombre son obligatorios.");
                    continue;
                }

                rows.Add((id.Trim(), nombre.Trim(), carrera.Trim(), tema.Trim()));
            }
        }
        else if (ext == ".xlsx" || ext == ".xls")
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            using var workbook = new XLWorkbook(ms);
            var ws = workbook.Worksheets.FirstOrDefault();
            if (ws == null)
                return new ImportResult(0, 0, 0, new() { "El archivo Excel no contiene hojas." });

            var firstRow = ws.FirstRowUsed();
            if (firstRow == null)
                return new ImportResult(0, 0, 0, new() { "La hoja de Excel está vacía." });

            var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
            var headers = new List<string>();
            for (int c = 1; c <= lastCol; c++)
                headers.Add(NormalizeHeader(firstRow.Cell(c).GetString()));

            int idxId = FindColumn(headers, "id", "matricula", "codigo");
            int idxNombre = FindColumn(headers, "nombre", "estudiante", "alumno", "name");
            int idxCarrera = FindColumn(headers, "carrera", "programa", "career");
            int idxTema = FindColumn(headers, "tematesis", "tema", "tesis", "temadetesis");

            if (idxId < 0 || idxNombre < 0 || idxCarrera < 0 || idxTema < 0)
            {
                return new ImportResult(0, 0, 0, new()
                {
                    "El Excel debe contener las columnas: Id, Nombre, Carrera, TemaTesis."
                });
            }

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? firstRow.RowNumber();
            for (int r = firstRow.RowNumber() + 1; r <= lastRow; r++)
            {
                var row = ws.Row(r);
                string id = row.Cell(idxId + 1).GetString().Trim();
                string nombre = row.Cell(idxNombre + 1).GetString().Trim();
                string carrera = row.Cell(idxCarrera + 1).GetString().Trim();
                string tema = row.Cell(idxTema + 1).GetString().Trim();

                if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(nombre))
                    continue;

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nombre))
                {
                    errors.Add($"Fila {r}: Id y Nombre son obligatorios.");
                    continue;
                }

                rows.Add((id, nombre, carrera, tema));
            }
        }
        else
        {
            return new ImportResult(0, 0, 0, new() { "Formato no soportado. Use .csv o .xlsx" });
        }

        if (rows.Count == 0 && errors.Count == 0)
            errors.Add("No se encontraron registros válidos en el archivo.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        int added = 0, updated = 0, skipped = 0;

        foreach (var item in rows)
        {
            var existing = await db.Students.FindAsync(item.Id);
            if (existing == null)
            {
                db.Students.Add(new Student
                {
                    Id = item.Id,
                    Nombre = item.Nombre,
                    Carrera = string.IsNullOrWhiteSpace(item.Carrera) ? "Sin especificar" : item.Carrera,
                    TemaTesis = string.IsNullOrWhiteSpace(item.TemaTesis) ? "Sin especificar" : item.TemaTesis
                });
                added++;
            }
            else
            {
                existing.Nombre = item.Nombre;
                existing.Carrera = string.IsNullOrWhiteSpace(item.Carrera) ? existing.Carrera : item.Carrera;
                existing.TemaTesis = string.IsNullOrWhiteSpace(item.TemaTesis) ? existing.TemaTesis : item.TemaTesis;
                updated++;
            }
        }

        await db.SaveChangesAsync();
        return new ImportResult(added, updated, skipped, errors);
    }

    public byte[] GenerateTemplateExcel()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Estudiantes");
        ws.Cell(1, 1).Value = "Id";
        ws.Cell(1, 2).Value = "Nombre";
        ws.Cell(1, 3).Value = "Carrera";
        ws.Cell(1, 4).Value = "TemaTesis";

        ws.Cell(2, 1).Value = "20210001";
        ws.Cell(2, 2).Value = "Ana García López";
        ws.Cell(2, 3).Value = "Ingeniería en Sistemas";
        ws.Cell(2, 4).Value = "Sistema de detección de anomalías con Machine Learning";

        ws.Cell(3, 1).Value = "20210002";
        ws.Cell(3, 2).Value = "Carlos Martínez Ruiz";
        ws.Cell(3, 3).Value = "Ingeniería Industrial";
        ws.Cell(3, 4).Value = "Optimización de cadena de suministro en manufactura";

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string NormalizeHeader(string h) =>
        new string(h.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static int FindColumn(List<string> headers, params string[] candidates)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            if (candidates.Contains(headers[i])) return i;
        }
        return -1;
    }

    private static string GetCol(List<string> cols, int idx) =>
        idx >= 0 && idx < cols.Count ? cols[idx].Trim() : string.Empty;

    private static List<string> SplitCsvLine(string line, char separator)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == separator && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }
}
