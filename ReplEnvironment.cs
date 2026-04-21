using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Interfaces;
using UI;

namespace Repl;

public class ReplEnvironment
{
    private readonly ISemanticMotor _motor;
    private string _currentLanguage = "español";
    private string _currentFilter = string.Empty;
    private string _themeName;

    private CliTheme CurrentTheme => ThemeLibrary.GetTheme(_themeName);

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
{t.Primary}{TerminalColors.Bold}  _____ ______ __  __          _   _ _______ _____ _____ 
 / ____|  ____|  \/  |   /\   | \ | |__   __|_   _/ ____|
| (___ | |__  | \  / |  /  \  |  \| |  | |    | || |     
 \___ \|  __| | |\/| | / /\ \ | . ` |  | |    | || |     
 ____) | |____| |  | |/ ____ \| |\  |  | |   _| || |____ 
|_____/|______|_|  |_/_/    \_\_| \_|  |_|  |_____\_____|
{t.Secondary}           SEARCH ENGINE - SEMANTIC CLI v0.5{TerminalColors.Reset}
";
        Console.WriteLine(banner);
    }

    public async Task StartLoopAsync()
    {
        PrintBanner();
        Console.WriteLine(
            $"{CurrentTheme.Secondary}Iniciando Modo Interactivo. Escribe 'help' para ver comandos.{TerminalColors.Reset}"
        );
        bool isRunning = true;

        string command;
        string argument;

        while (isRunning)
        {
            // 1. READ (Prompt Dinámico y con Colores)
            var t = CurrentTheme;
            string filterText = string.IsNullOrEmpty(_currentFilter) ? "ninguno" : _currentFilter;

            Console.Write($"\n{t.Primary}rag-cli{TerminalColors.Reset} ");
            Console.Write($"{t.Secondary}({_currentLanguage}){TerminalColors.Reset} ");
            Console.Write(
                $"{t.Accent}[filtro:{filterText}]{TerminalColors.Reset} {t.Primary}>{TerminalColors.Reset} "
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
                            $"{CurrentTheme.Error}Uso: set-filter \"category:Contabilidad\"{TerminalColors.Reset}"
                        );
                        continue;
                    }
                    else
                    {
                        _currentFilter = argument;
                        Console.WriteLine(
                            $"{CurrentTheme.Success}Filtro de la sesión cambiado a: {_currentFilter}{TerminalColors.Reset}"
                        );
                    }
                }
                else if (command == "set-theme")
                {
                    if (parts.Length < 2 || !ThemeLibrary.IsValidTheme(argument))
                    {
                        Console.WriteLine(
                            $"{CurrentTheme.Error}Temas disponibles: {string.Join(", ", ThemeLibrary.GetAvailableThemes())}{TerminalColors.Reset}"
                        );
                        continue;
                    }

                    _themeName = argument;
                    await SaveThemeToConfigAsync(_themeName);
                    Console.WriteLine(
                        $"{CurrentTheme.Success}Tema cambiado a: {_themeName}{TerminalColors.Reset}"
                    );
                }
                else if (command == "help")
                {
                    PrintHelp();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"{CurrentTheme.Error}[Error]: {ex.Message}{TerminalColors.Reset}"
                );
            }
        }

        Console.WriteLine(
            $"{CurrentTheme.Secondary}Sesión finalizada. Cerrando motor...{TerminalColors.Reset}"
        );
    }

    private void PrintHelp()
    {
        var t = CurrentTheme;
        Console.WriteLine(
            $"\n{t.Primary}{TerminalColors.Bold}COMANDOS DISPONIBLES:{TerminalColors.Reset}"
        );
        Console.WriteLine(
            $"{t.Accent}  ingest \"<ruta>\"{TerminalColors.Reset}       - Ingiere un archivo"
        );
        Console.WriteLine(
            $"{t.Accent}  ingest-folder \"<ruta>\"{TerminalColors.Reset}- Ingiere una carpeta completa"
        );
        Console.WriteLine(
            $"{t.Accent}  ask \"<pregunta>\"{TerminalColors.Reset}       - Realiza una consulta"
        );
        Console.WriteLine(
            $"{t.Accent}  delete \"<id>\"{TerminalColors.Reset}           - Borra un documento"
        );
        Console.WriteLine(
            $"{t.Accent}  set-lang <idioma>{TerminalColors.Reset}     - Cambia el idioma de respuesta"
        );
        Console.WriteLine(
            $"{t.Accent}  set-filter <tag:val>{TerminalColors.Reset}  - Filtra las búsquedas"
        );
        Console.WriteLine(
            $"{t.Accent}  set-theme <nombre>{TerminalColors.Reset}    - Cambia el tema (Gotham, Rust, Neon-Vapor, Forest, Glacier)"
        );
        Console.WriteLine(
            $"{t.Accent}  help{TerminalColors.Reset}                   - Muestra esta ayuda"
        );
        Console.WriteLine(
            $"{t.Accent}  exit{TerminalColors.Reset}                   - Sale del programa\n"
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
