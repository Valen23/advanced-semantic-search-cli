using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Interfaces;

namespace Repl;

public class ReplEnvironment
{
    private readonly ISemanticMotor _motor;
    private string _currentLanguage = "español";
    private string _currentFilter = string.Empty;
    private string _themeName;

    // Colores ANSI 256 para mayor profundidad visual
    private static class Colors
    {
        public const string Reset = "\u001b[0m";
        public const string Bold = "\u001b[1m";
        public const string Cyan = "\u001b[38;5;103m"; // Steel Blue-Gray (More visible)
        public const string Magenta = "\u001b[38;5;67m"; // Shadow Blue (More visible)
        public const string Yellow = "\u001b[38;5;214m"; // Gold / Utility Belt Yellow
        public const string Green = "\u001b[38;5;108m"; // Sage / Muted Success
        public const string Blue = "\u001b[38;5;110m"; // Cold Steel Blue
        public const string Orange = "\u001b[38;5;172m"; // Amber
        public const string Gray = "\u001b[38;5;244m"; // Medium Gray
        public const string Red = "\u001b[38;5;167m"; // Muted Crimson
    }

    private record CliTheme(
        string Primary,
        string Secondary,
        string Accent,
        string Success,
        string Error
    );

    private static readonly Dictionary<string, CliTheme> Themes = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["Cyberpunk"] = new CliTheme(
            Colors.Magenta,
            Colors.Cyan,
            Colors.Yellow,
            Colors.Green,
            Colors.Red
        ),
        ["Midnight"] = new CliTheme(Colors.Blue, Colors.Gray, Colors.Bold, Colors.Cyan, Colors.Red),
        ["Emerald"] = new CliTheme(
            Colors.Green,
            Colors.Bold,
            Colors.Cyan,
            Colors.Yellow,
            Colors.Red
        ),
        ["Sunset"] = new CliTheme(
            Colors.Orange,
            Colors.Magenta,
            Colors.Yellow,
            Colors.Cyan,
            Colors.Red
        ),
    };

    private CliTheme CurrentTheme =>
        Themes.TryGetValue(_themeName, out var t) ? t : Themes["Cyberpunk"];

    public ReplEnvironment(ISemanticMotor motor, string themeName)
    {
        _motor = motor;
        _themeName = themeName;
    }

    private void PrintBanner()
    {
        var t = CurrentTheme;
        Console.ForegroundColor = ConsoleColor.Black; // Hack para limpiar si hay bordes
        Console.ResetColor();

        string banner =
            $@"
{t.Primary}{Colors.Bold}  _____ ______ __  __          _   _ _______ _____ _____ 
 / ____|  ____|  \/  |   /\   | \ | |__   __|_   _/ ____|
| (___ | |__  | \  / |  /  \  |  \| |  | |    | || |     
 \___ \|  __| | |\/| | / /\ \ | . ` |  | |    | || |     
 ____) | |____| |  | |/ ____ \| |\  |  | |   _| || |____ 
|_____/|______|_|  |_/_/    \_\_| \_|  |_|  |_____\_____|
{t.Secondary}           SEARCH ENGINE - SEMANTIC CLI v0.5{Colors.Reset}
";
        Console.WriteLine(banner);
    }

    public async Task StartLoopAsync()
    {
        PrintBanner();
        Console.WriteLine(
            $"{CurrentTheme.Secondary}Iniciando Modo Interactivo. Escribe 'help' para ver comandos.{Colors.Reset}"
        );
        bool isRunning = true;

        string command;
        string argument;

        while (isRunning)
        {
            // 1. READ (Prompt Dinámico y con Colores)
            var t = CurrentTheme;
            string filterText = string.IsNullOrEmpty(_currentFilter) ? "ninguno" : _currentFilter;

            Console.Write($"\n{t.Primary}rag-cli{Colors.Reset} ");
            Console.Write($"{t.Secondary}({_currentLanguage}){Colors.Reset} ");
            Console.Write(
                $"{t.Accent}[filtro:{filterText}]{Colors.Reset} {t.Primary}>{Colors.Reset} "
            );

            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            var matches = Regex.Matches(input, @"[^\s""]+|""([^""]*)""");

            string clean = input.Trim();
            var parts = matches
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Value)
                .ToArray();
            command = parts[0].ToLower();
            argument = parts.Length > 1 ? parts[1] : string.Empty;

            if (command == "exit" || command == "quit")
            {
                isRunning = false;
                continue;
            }

            try
            {
                if (command == "ingest")
                {
                    if (parts.Length > 2)
                    {
                        Console.WriteLine(
                            "[Aviso]: Ruta con espacios detectada. ¿Olvidaste las comillas?"
                        );
                        Console.WriteLine(
                            "Uso correcto: ingest \"C:\\Mis Documentos\\archivo.pdf\""
                        );
                    }
                    await _motor.IngestAsync(argument, "Docs");
                }
                else if (command == "ingest-folder")
                {
                    await _motor.IngestFolderAsync(argument);
                }
                else if (command == "ask")
                {
                    if (parts.Length == 1)
                    {
                        Console.WriteLine("Uso correcto: ask \"¿Cuál es la capital de Francia?\"");
                        continue;
                    }
                    else if (parts.Length > 2)
                    {
                        Console.WriteLine(
                            "[Aviso]: Detectamos varias palabras sin comillas. Solo se procesará la primera."
                        );
                        Console.WriteLine("Uso correcto: ask \"¿Cuál es la capital de Francia?\"");
                        continue;
                    }
                    else
                    {
                        await _motor.AskQuestionAsync(argument, _currentLanguage, _currentFilter);
                    }
                }
                else if (command == "delete")
                {
                    await _motor.DeleteDocumentAsync(argument);
                }
                else if (command == "set-lang")
                {
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("Uso: set-lang \"<idioma>\"");
                        continue;
                    }
                    else
                    {
                        _currentLanguage = argument;
                        Console.WriteLine($"Idioma de la sesión cambiado a: {_currentLanguage}");
                    }
                }
                else if (command == "set-filter")
                {
                    if (parts.Length < 2)
                    {
                        Console.WriteLine(
                            $"{CurrentTheme.Error}Uso: set-filter \"category:Contabilidad\"{Colors.Reset}"
                        );
                        continue;
                    }
                    else
                    {
                        _currentFilter = argument;
                        Console.WriteLine(
                            $"{CurrentTheme.Success}Filtro de la sesión cambiado a: {_currentFilter}{Colors.Reset}"
                        );
                    }
                }
                else if (command == "set-theme")
                {
                    if (parts.Length < 2 || !Themes.ContainsKey(argument))
                    {
                        Console.WriteLine(
                            $"{CurrentTheme.Error}Temas disponibles: {string.Join(", ", Themes.Keys)}{Colors.Reset}"
                        );
                        continue;
                    }

                    _themeName = argument;
                    await SaveThemeToConfigAsync(_themeName);
                    Console.WriteLine(
                        $"{CurrentTheme.Success}Tema cambiado a: {_themeName}{Colors.Reset}"
                    );
                }
                else if (command == "help")
                {
                    PrintHelp();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{CurrentTheme.Error}[Error]: {ex.Message}{Colors.Reset}");
            }
        }

        Console.WriteLine(
            $"{CurrentTheme.Secondary}Sesión finalizada. Cerrando motor...{Colors.Reset}"
        );
    }

    private void PrintHelp()
    {
        var t = CurrentTheme;
        Console.WriteLine($"\n{t.Primary}{Colors.Bold}COMANDOS DISPONIBLES:{Colors.Reset}");
        Console.WriteLine(
            $"{t.Accent}  ingest \"<ruta>\"{Colors.Reset}       - Ingiere un archivo"
        );
        Console.WriteLine(
            $"{t.Accent}  ingest-folder \"<ruta>\"{Colors.Reset}- Ingiere una carpeta completa"
        );
        Console.WriteLine(
            $"{t.Accent}  ask \"<pregunta>\"{Colors.Reset}       - Realiza una consulta"
        );
        Console.WriteLine(
            $"{t.Accent}  delete \"<id>\"{Colors.Reset}           - Borra un documento"
        );
        Console.WriteLine(
            $"{t.Accent}  set-lang <idioma>{Colors.Reset}     - Cambia el idioma de respuesta"
        );
        Console.WriteLine(
            $"{t.Accent}  set-filter <tag:val>{Colors.Reset}  - Filtra las búsquedas"
        );
        Console.WriteLine(
            $"{t.Accent}  set-theme <nombre>{Colors.Reset}    - Cambia el tema (Cyberpunk, Emerald, Midnight, Sunset)"
        );
        Console.WriteLine($"{t.Accent}  help{Colors.Reset}                   - Muestra esta ayuda");
        Console.WriteLine(
            $"{t.Accent}  exit{Colors.Reset}                   - Sale del programa\n"
        );
    }

    private async Task SaveThemeToConfigAsync(string themeName)
    {
        try
        {
            string configPath = "appsettings.json";
            var jsonText = await File.ReadAllTextAsync(configPath);
            var jsonNode = JsonNode.Parse(jsonText);

            if (jsonNode?["SemanticEngine"] != null)
            {
                jsonNode["SemanticEngine"]!["Theme"] = themeName;
                await File.WriteAllTextAsync(configPath, jsonNode.ToString());
            }
        }
        catch
        { /* Fallo silencioso si no se puede escribir */
        }
    }
}
