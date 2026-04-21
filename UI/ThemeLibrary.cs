using System;
using System.Collections.Generic;

namespace UI;

/// <summary>
/// Biblioteca central de temas para el CLI semántico.
/// </summary>
public static class ThemeLibrary
{
    /// <summary>
    /// Nombre del tema por defecto si no se encuentra el especificado.
    /// </summary>
    public const string DefaultThemeName = "Gotham";

    /// <summary>
    /// Almacén interno de temas disponibles indexados por nombre.
    /// </summary>
    private static readonly Dictionary<string, CliTheme> Themes = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["Gotham"] = new CliTheme(
            "\u001b[38;5;103m", // Steel Blue-Gray
            "\u001b[38;5;67m", // Shadow Blue
            "\u001b[38;5;214m", // Gold
            "\u001b[38;5;108m", // Sage
            "\u001b[38;5;167m" // Muted Crimson
        ),
        ["Rust"] = new CliTheme(
            "\u001b[38;5;131m", // Rust
            "\u001b[38;5;244m", // Iron Gray
            "\u001b[38;5;136m", // Ochre
            "\u001b[38;5;58m", // Moss Green
            "\u001b[38;5;124m" // Deep Blood Red
        ),
        ["Neon-Vapor"] = new CliTheme(
            "\u001b[38;5;205m", // Pink
            "\u001b[38;5;93m", // Deep Purple
            "\u001b[38;5;51m", // Cyan
            "\u001b[38;5;121m", // Mint
            "\u001b[38;5;197m" // Vivid Red
        ),
        ["Forest"] = new CliTheme(
            "\u001b[38;5;65m", // Olive
            "\u001b[38;5;94m", // Brown
            "\u001b[38;5;180m", // Tan
            "\u001b[38;5;150m", // Salvia Green
            "\u001b[38;5;174m" // Dusty Rose
        ),
        ["Glacier"] = new CliTheme(
            "\u001b[38;5;153m", // Ice Blue
            "\u001b[38;5;67m", // Steel Blue
            "\u001b[38;5;195m", // Arctic White
            "\u001b[38;5;159m", // Crystal
            "\u001b[38;5;203m" // Soft Red
        ),
    };

    /// <summary>
    /// Obtiene un tema por su nombre. Devuelve Gotham si el tema no existe.
    /// </summary>
    public static CliTheme GetTheme(string name)
    {
        return Themes.TryGetValue(name, out var theme) ? theme : Themes[DefaultThemeName];
    }

    /// <summary>
    /// Obtiene la lista de nombres de temas disponibles.
    /// </summary>
    public static IEnumerable<string> GetAvailableThemes() => Themes.Keys;

    /// <summary>
    /// Verifica si un nombre de tema es válido.
    /// </summary>
    public static bool IsValidTheme(string name) => Themes.ContainsKey(name);
}
