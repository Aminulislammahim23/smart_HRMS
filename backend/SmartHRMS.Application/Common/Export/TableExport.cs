using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace smartHRMS.Application.Common.Export;

public enum ExportFormat
{
    Csv,
    Xlsx,
}

public enum ExportKind
{
    Text,
    Integer,
    Amount,
    Date,
    DateTime,
}

public sealed record ExportColumn(string Header, ExportKind Kind = ExportKind.Text);

/// <summary>A titled table of rows to export; each row has one value per column (null for an empty cell).</summary>
public sealed class ExportTable
{
    public ExportTable(string title, params ExportColumn[] columns)
    {
        Title = title;
        Columns = columns;
    }

    public string Title { get; }

    public IReadOnlyList<ExportColumn> Columns { get; }

    public List<object?[]> Rows { get; } = new();

    public ExportTable Add(params object?[] values)
    {
        if (values.Length != Columns.Count)
        {
            throw new ArgumentException($"Expected {Columns.Count} values, got {values.Length}.", nameof(values));
        }

        Rows.Add(values);
        return this;
    }
}

/// <summary>A file produced by an export, ready to return from an endpoint.</summary>
public sealed record ExportFile(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Writes an <see cref="ExportTable"/> as CSV (UTF-8 with BOM so Excel detects the encoding) or as a single-sheet
/// .xlsx workbook. No third-party library: the .xlsx is a minimal Office Open XML package built with the BCL zip
/// support. Text that starts like a spreadsheet formula is prefixed with an apostrophe in CSV (formula injection).
/// </summary>
public static class TableExporter
{
    public static ExportFormat ParseFormat(string? value) => (value ?? "csv").Trim().ToLowerInvariant() switch
    {
        "csv" => ExportFormat.Csv,
        "xlsx" or "excel" => ExportFormat.Xlsx,
        _ => throw new Exceptions.BadRequestException($"'{value}' is not a valid export format. Allowed values: csv, xlsx."),
    };

    public static ExportFile Write(ExportTable table, ExportFormat format, string fileNameWithoutExtension)
    {
        return format == ExportFormat.Xlsx
            ? new ExportFile($"{fileNameWithoutExtension}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", WriteXlsx(table))
            : new ExportFile($"{fileNameWithoutExtension}.csv", "text/csv", WriteCsv(table));
    }

    // ---- CSV ----

    public static byte[] WriteCsv(ExportTable table)
    {
        var builder = new StringBuilder();
        builder.AppendJoin(',', table.Columns.Select(c => CsvText(c.Header))).Append("\r\n");
        foreach (var row in table.Rows)
        {
            builder.AppendJoin(',', row.Select((value, i) => CsvCell(value, table.Columns[i].Kind))).Append("\r\n");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static string CsvCell(object? value, ExportKind kind) => value switch
    {
        null => string.Empty,
        decimal d => d.ToString(kind == ExportKind.Integer ? "0" : "0.00", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime time => FormatDateTime(time),
        _ => CsvText(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
    };

    private static string CsvText(string text)
    {
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]))
        {
            text = "'" + text;
        }

        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }

    private static string FormatDateTime(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    // ---- XLSX ----

    public static byte[] WriteXlsx(ExportTable table)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Entry(zip, "[Content_Types].xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Entry(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Entry(zip, "xl/workbook.xml",
                $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{Xml(SheetName(table.Title))}" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Entry(zip, "xl/_rels/workbook.xml.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            // Styles: 0 default, 1 bold header, 2 amount (#,##0.00), 3 integer (#,##0).
            Entry(zip, "xl/styles.xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs></styleSheet>""");
            Entry(zip, "xl/worksheets/sheet1.xml", Sheet(table));
        }

        return stream.ToArray();
    }

    private static string Sheet(ExportTable table)
    {
        var builder = new StringBuilder(
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        builder.Append($"""<cols><col min="1" max="{table.Columns.Count}" width="18" customWidth="1"/></cols><sheetData>""");

        builder.Append("""<row r="1">""");
        for (var c = 0; c < table.Columns.Count; c++)
        {
            builder.Append(InlineString($"{ColumnName(c)}1", table.Columns[c].Header, style: 1));
        }

        builder.Append("</row>");
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var rowNumber = r + 2;
            builder.Append($"""<row r="{rowNumber}">""");
            for (var c = 0; c < table.Columns.Count; c++)
            {
                var reference = $"{ColumnName(c)}{rowNumber}";
                builder.Append(table.Rows[r][c] switch
                {
                    null => string.Empty,
                    decimal d => $"""<c r="{reference}" s="{(table.Columns[c].Kind == ExportKind.Integer ? 3 : 2)}"><v>{d.ToString(CultureInfo.InvariantCulture)}</v></c>""",
                    int i => $"""<c r="{reference}" s="3"><v>{i.ToString(CultureInfo.InvariantCulture)}</v></c>""",
                    DateOnly date => InlineString(reference, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    DateTime time => InlineString(reference, FormatDateTime(time)),
                    var other => InlineString(reference, Convert.ToString(other, CultureInfo.InvariantCulture) ?? string.Empty),
                });
            }

            builder.Append("</row>");
        }

        return builder.Append("</sheetData></worksheet>").ToString();
    }

    private static string InlineString(string reference, string text, int style = 0) =>
        $"""<c r="{reference}" t="inlineStr"{(style == 0 ? string.Empty : $" s=\"{style}\"")}><is><t xml:space="preserve">{Xml(text)}</t></is></c>""";

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + (n - 1) % 26) + name;
        }

        return name;
    }

    private static string SheetName(string title)
    {
        var cleaned = new string(title.Where(ch => "[]:*?/\\".IndexOf(ch) < 0).ToArray()).Trim();
        return cleaned.Length == 0 ? "Report" : cleaned[..Math.Min(31, cleaned.Length)];
    }

    /// <summary>XML-escapes text and drops characters that XML 1.0 can't contain.</summary>
    private static string Xml(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch < 0x20 && ch is not ('\t' or '\n' or '\r'))
            {
                continue;
            }

            builder.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                _ => ch.ToString(),
            });
        }

        return builder.ToString();
    }

    private static void Entry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
