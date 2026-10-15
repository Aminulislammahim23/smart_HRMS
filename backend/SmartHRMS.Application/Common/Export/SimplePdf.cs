using System.Globalization;
using System.Text;

namespace smartHRMS.Application.Common.Export;

public enum PdfAlign
{
    Left,
    Right,
}

/// <summary>
/// A minimal single-page PDF 1.4 writer (A4 portrait) for simple documents such as payslips: text in the standard
/// Helvetica / Helvetica-Bold fonts (WinAnsi encoding), lines and filled rectangles. It exists because the project has
/// no PDF library and a payslip needs none of a library's features; no font is embedded, so the file is a few KB.
/// Characters outside WinAnsi are written as "?". Coordinates are points from the top-left corner.
/// </summary>
public sealed class SimplePdf
{
    public const float PageWidth = 595.28f;
    public const float PageHeight = 841.89f;

    private static readonly Encoding WinAnsi = Encoding.Latin1;

    // Glyph widths (1/1000 em) of ASCII 32..126, from the Adobe Helvetica and Helvetica-Bold AFM metrics.
    private static readonly short[] HelveticaWidths =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556,
        556, 556, 278, 278, 584, 584, 584, 556, 1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, 333, 556, 556, 500, 556, 556, 278, 556,
        556, 222, 222, 500, 222, 833, 556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private static readonly short[] HelveticaBoldWidths =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556,
        556, 556, 333, 333, 584, 584, 584, 611, 975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556, 333, 556, 611, 556, 611, 556, 333, 611,
        611, 278, 278, 556, 278, 889, 611, 611, 611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584,
    ];

    private readonly StringBuilder _content = new();
    private readonly string _title;

    public SimplePdf(string title)
    {
        _title = title;
    }

    /// <summary>Writes one line of text; <paramref name="y"/> is the baseline.</summary>
    public SimplePdf Text(float x, float y, string text, float size, bool bold = false, PdfAlign align = PdfAlign.Left, float gray = 0f)
    {
        var bytes = ToWinAnsi(text);
        var left = align == PdfAlign.Right ? x - Width(bytes, size, bold) : x;
        _content.Append(Invariant($"{gray:0.###} g BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {left:0.##} {PageHeight - y:0.##} Td ("))
            .Append(Escape(bytes))
            .Append(") Tj ET\n");
        return this;
    }

    /// <summary>Writes text cut to <paramref name="maxWidth"/> points, ending with "..." when it doesn't fit.</summary>
    public SimplePdf TextFit(float x, float y, string text, float size, float maxWidth, bool bold = false, PdfAlign align = PdfAlign.Left, float gray = 0f)
    {
        if (Width(ToWinAnsi(text), size, bold) > maxWidth)
        {
            while (text.Length > 1 && Width(ToWinAnsi(text + "..."), size, bold) > maxWidth)
            {
                text = text[..^1];
            }

            text = text.TrimEnd() + "...";
        }

        return Text(x, y, text, size, bold, align, gray);
    }

    public SimplePdf Line(float x1, float y1, float x2, float y2, float width = 0.5f, float gray = 0.75f)
    {
        _content.Append(Invariant($"{gray:0.###} G {width:0.##} w {x1:0.##} {PageHeight - y1:0.##} m {x2:0.##} {PageHeight - y2:0.##} l S\n"));
        return this;
    }

    /// <summary>A filled rectangle; (x, y) is its top-left corner.</summary>
    public SimplePdf FillRect(float x, float y, float width, float height, float gray)
    {
        _content.Append(Invariant($"{gray:0.###} g {x:0.##} {PageHeight - y - height:0.##} {width:0.##} {height:0.##} re f\n"));
        return this;
    }

    /// <summary>Text width in points, for alignment.</summary>
    public static float Width(string text, float size, bool bold = false) => Width(ToWinAnsi(text), size, bold);

    public byte[] ToBytes()
    {
        var stream = WinAnsi.GetBytes(_content.ToString());
        var objects = new List<byte[]>
        {
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii(Invariant($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth:0.##} {PageHeight:0.##}] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>")),
            Concat(Ascii($"<< /Length {stream.Length} >>\nstream\n"), stream, Ascii("\nendstream")),
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
            Concat(Ascii("<< /Title ("), Escape(ToWinAnsi(_title)).Select(c => (byte)c).ToArray(), Ascii(") /Producer (SmartHRMS) >>")),
        };

        using var output = new MemoryStream();
        Write(output, Ascii("%PDF-1.4\n%âãÏÓ\n"));
        var offsets = new List<long>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            Write(output, Ascii($"{i + 1} 0 obj\n"));
            Write(output, objects[i]);
            Write(output, Ascii("\nendobj\n"));
        }

        var xref = output.Position;
        var table = new StringBuilder($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        table.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 7 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        Write(output, Ascii(table.ToString()));
        return output.ToArray();
    }

    private static float Width(byte[] text, float size, bool bold)
    {
        var widths = bold ? HelveticaBoldWidths : HelveticaWidths;
        var total = 0;
        foreach (var b in text)
        {
            total += b is >= 32 and <= 126 ? widths[b - 32] : b switch { 0x96 => 556, 0x97 => 1000, _ => 556 };
        }

        return total * size / 1000f;
    }

    /// <summary>WinAnsi bytes: Latin-1 plus en/em dash; anything else becomes "?".</summary>
    private static byte[] ToWinAnsi(string text)
    {
        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            bytes[i] = c switch
            {
                '–' => 0x96,
                '—' => 0x97,
                '→' => (byte)'>',
                _ when c is >= ' ' and <= '~' || c is >= ' ' and <= 'ÿ' => (byte)c,
                _ => (byte)'?',
            };
        }

        return bytes;
    }

    private static string Escape(byte[] text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var b in text)
        {
            if (b is (byte)'(' or (byte)')' or (byte)'\\')
            {
                builder.Append('\\');
            }

            builder.Append((char)b);
        }

        return builder.ToString();
    }

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private static byte[] Ascii(string value) => WinAnsi.GetBytes(value);

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    private static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);
}
