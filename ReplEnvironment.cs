using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CLI.Routing;
using Interfaces;
using UI;

namespace Repl;

/// <summary>
/// Provee el entorno de ejecución REPL (Read-Eval-Print Loop) para la consola interactiva.
/// Gestiona el estado de la sesión, la configuración visual y el enrutamiento de comandos básicos.
/// </summary>
public class ReplEnvironment
{
    private readonly ISemanticMotor _motor;
    private readonly DomainCommandRouter _domainRouter;
    private string _currentLanguage = "español";
    private string _currentFilter = string.Empty;
    private string _themeName;

    /// <summary>
    /// Obtiene el objeto de tema actual basado en el nombre configurado.
    /// </summary>
    private CliTheme CurrentTheme => ThemeLibrary.GetTheme(_themeName);

    public ReplEnvironment(ISemanticMotor motor, string themeName)
    {
        _motor = motor;
        _themeName = themeName;
        _domainRouter = new DomainCommandRouter(motor);
    }

    /// <summary>
    /// Imprime el banner ASCII del programa utilizando los colores del tema actual.
    /// </summary>
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
{t.Secondary}           SEARCH ENGINE - SEMANTIC CLI v1.0{TerminalColors.Reset}
";
        Console.WriteLine(banner);
    }

    /// <summary>
    /// Inicia el bucle interactivo de la consola, procesando la entrada del usuario hasta que se solicite salir.
    /// </summary>
    public async Task StartLoopAsync()
    {
        bool isRunning = true;

        Console.Clear();
        PrintBanner();

        while (isRunning)
        {
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
            var parts = matches
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Value)
                .ToArray();

            string command = parts[0].ToLower();
            string[] commandArgs = parts.Skip(1).ToArray();

            if (command == "exit" || command == "quit")
            {
                isRunning = false;
                continue;
            }

            try
            {
                if (command == "clear" || command == "cls")
                {
                    Console.Clear();
                    PrintBanner();
                    Console.WriteLine(
                        $"{t.Secondary}Consola limpiada. Modo Interactivo Activo.{TerminalColors.Reset}"
                    );
                }
                else if (command == "set-lang")
                {
                    if (commandArgs.Length == 0)
                    {
                        Console.WriteLine(
                            $"{t.Error}Uso: set-lang \"<idioma>\"{TerminalColors.Reset}"
                        );
                        continue;
                    }
                    _currentLanguage = commandArgs[0];
                    Console.WriteLine(
                        $"{t.Success}Idioma de la sesión cambiado a: {_currentLanguage}{TerminalColors.Reset}"
                    );
                }
                else if (command == "set-filter")
                {
                    if (commandArgs.Length == 0)
                    {
                        Console.WriteLine(
                            $"{t.Error}Uso: set-filter \"category:Contabilidad\"{TerminalColors.Reset}"
                        );
                        continue;
                    }
                    _currentFilter = commandArgs[0];
                    Console.WriteLine(
                        $"{t.Success}Filtro de la sesión cambiado a: {_currentFilter}{TerminalColors.Reset}"
                    );
                }
                else if (command == "set-theme")
                {
                    if (commandArgs.Length == 0 || !ThemeLibrary.IsValidTheme(commandArgs[0]))
                    {
                        Console.WriteLine(
                            $"{t.Error}Temas disponibles: {string.Join(", ", ThemeLibrary.GetAvailableThemes())}{TerminalColors.Reset}"
                        );
                        continue;
                    }

                    _themeName = commandArgs[0];
                    await SaveThemeToConfigAsync(_themeName);
                    Console.WriteLine(
                        $"{t.Success}Tema cambiado a: {_themeName}{TerminalColors.Reset}"
                    );
                }
                else if (command == "help")
                {
                    PrintHelp();
                }
                else
                {
                    await _domainRouter.ExecuteAsync(
                        command,
                        commandArgs,
                        _currentLanguage,
                        _currentFilter,
                        t // Tema actual
                    );
                }
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(
                    $"{CurrentTheme.Error}[Comando Inválido]: {ex.Message}{TerminalColors.Reset}"
                );
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

    /// <summary>
    /// Muestra la lista de comandos disponibles y su descripción.
    /// </summary>
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
            $"{t.Accent}  clear / cls{TerminalColors.Reset}            - Limpia la pantalla y muestra el banner"
        );
        Console.WriteLine(
            $"{t.Accent}  help{TerminalColors.Reset}                   - Muestra esta ayuda"
        );
        Console.WriteLine(
            $"{t.Accent}  exit{TerminalColors.Reset}                   - Sale del programa\n"
        );
    }

    /// <summary>
    /// Persiste la selección del tema en el archivo appsettings.json.
    /// </summary>
    /// <param name="themeName">Nombre del tema a guardar.</param>
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
