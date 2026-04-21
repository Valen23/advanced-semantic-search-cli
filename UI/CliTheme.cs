namespace UI;

/// <summary>
/// Contiene constantes de colores ANSI 256 para la terminal.
/// </summary>
public static class TerminalColors
{
    public const string Reset = "\u001b[0m";
    public const string Bold = "\u001b[1m";
    public const string Underline = "\u001b[4m";
}

/// <summary>
/// Representa una paleta de colores para la interfaz de línea de comandos.
/// </summary>
/// <param name="Primary">Color principal (títulos, prompt).</param>
/// <param name="Secondary">Color secundario (detalles, paréntesis).</param>
/// <param name="Accent">Color de acento (filtros, ayuda).</param>
/// <param name="Success">Color de éxito.</param>
/// <param name="Error">Color de error o alerta.</param>
public record CliTheme(
    string Primary,
    string Secondary,
    string Accent,
    string Success,
    string Error
);
