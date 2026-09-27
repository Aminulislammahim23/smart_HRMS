namespace smartHRMS.Application.Common.Text;

/// <summary>Small helpers for cleaning free-text input before it is compared or stored.</summary>
internal static class InputText
{
    /// <summary>Trims the value; blank or whitespace-only becomes null.</summary>
    public static string? Optional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Trims a value that validation has already confirmed is present.</summary>
    public static string Required(string value)
    {
        return value.Trim();
    }
}
