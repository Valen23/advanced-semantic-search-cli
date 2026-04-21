using Interfaces;
using UI;

namespace CLI.Routing;

/// <summary>
/// Enruta y ejecuta los comandos relacionados con el dominio semántico (búsqueda, ingesta, eliminación).
/// Se encarga de la lógica de presentación y flujo de usuario para estos comandos.
/// </summary>
public class DomainCommandRouter
{
    private readonly ISemanticMotor _motor;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="DomainCommandRouter"/>.
    /// </summary>
    /// <param name="motor">El motor semántico a utilizar para las operaciones de dominio.</param>
    public DomainCommandRouter(ISemanticMotor motor)
    {
        _motor = motor;
    }

    /// <summary>
    /// Ejecuta el comando especificado utilizando los argumentos y configuración de sesión provistos.
    /// </summary>
    /// <param name="command">Nombre del comando a ejecutar.</param>
    /// <param name="arguments">Lista de argumentos para el comando.</param>
    /// <param name="language">Idioma actual de la sesión para las respuestas.</param>
    /// <param name="filter">Filtro de búsqueda actual.</param>
    /// <param name="t">Tema visual activo.</param>
    public async Task ExecuteAsync(
        string command,
        string[] arguments,
        string language,
        string filter,
        CliTheme t
    )
    {
        switch (command)
        {
            case "ask":
                await HandleAskCommandAsync(arguments, language, filter, t);
                break;

            case "ingest":
                if (arguments.Length == 0)
                {
                    Console.WriteLine(
                        $"{t.Error}Uso correcto: ingest \"ruta-del-archivo.pdf\"{TerminalColors.Reset}"
                    );
                    return;
                }
                await _motor.IngestAsync(arguments[0], "Docs");
                break;

            case "ingest-folder":
                if (arguments.Length == 0)
                {
                    Console.WriteLine(
                        $"{t.Error}Uso correcto: ingest-folder \"ruta/carpeta\"{TerminalColors.Reset}"
                    );
                    return;
                }
                await _motor.IngestFolderAsync(arguments[0]);
                break;

            case "delete":
                if (arguments.Length == 0)
                {
                    Console.WriteLine(
                        $"{t.Error}Uso correcto: delete \"id-del-documento\"{TerminalColors.Reset}"
                    );
                    return;
                }
                await _motor.DeleteDocumentAsync(arguments[0]);
                break;

            default:
                throw new ArgumentException($"Comando de dominio '{command}' no reconocido.");
        }
    }

    /// <summary>
    /// Maneja el comando 'ask', incluyendo el spinner de carga y el renderizado de la respuesta en streaming.
    /// </summary>
    private async Task HandleAskCommandAsync(
        string[] arguments,
        string language,
        string filter,
        CliTheme t
    )
    {
        if (arguments.Length == 0)
        {
            Console.WriteLine(
                $"{t.Error}Error: Debes proporcionar una pregunta.{TerminalColors.Reset}"
            );
            return;
        }

        string question = arguments[0];

        var cts = new CancellationTokenSource();
        var spinnerTask = ShowSpinner("Buscando en la base de datos semántica...", t, cts.Token);

        try
        {
            Console.WriteLine();
            var result = await _motor.AskQuestionStreamAsync(question, language, filter);

            cts.Cancel();
            await spinnerTask;
            Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");

            Console.WriteLine(
                $"\n{t.Secondary}{TerminalColors.Bold}  FUENTES RELEVANTES:{TerminalColors.Reset}"
            );
            foreach (var cite in result.SearchResult.Results)
            {
                Console.WriteLine($"{t.Accent}  • {cite.SourceName}{TerminalColors.Reset}");
                foreach (var part in cite.Partitions)
                {
                    Console.WriteLine(
                        $"    {t.Secondary}└─ [Relevancia: {part.Relevance:P0}]{TerminalColors.Reset}"
                    );
                }
            }

            Console.WriteLine(
                $"\n{t.Primary}{TerminalColors.Bold}  RESPUESTA:{TerminalColors.Reset}"
            );
            Console.ForegroundColor = GetConsoleColorFromHex(t.Primary);

            await foreach (var token in result.TextStream)
            {
                Console.Write(token);
                await Task.Delay(25);
            }

            Console.ResetColor();
            Console.WriteLine(
                $"\n\n{t.Accent}─────────────────────────────────────────────────────────────────{TerminalColors.Reset}"
            );
        }
        catch (Exception ex)
        {
            cts.Cancel();
            Console.WriteLine(
                $"\n{t.Error}Error durante la consulta: {ex.Message}{TerminalColors.Reset}"
            );
        }
    }

    /// <summary>
    /// Muestra una animación de spinner en la consola mientras se realiza una tarea asíncrona.
    /// </summary>
    private async Task ShowSpinner(string message, CliTheme t, CancellationToken token)
    {
        string[] frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        int i = 0;
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(100);
            Console.Write(
                $"\r{t.Accent}{frames[i % frames.Length]} {message}{TerminalColors.Reset}"
            );
            i++;
        }
    }

    /// <summary>
    /// Intenta mapear un código ANSI o color hexadecimal a un ConsoleColor (Fallback).
    /// </summary>
    private ConsoleColor GetConsoleColorFromHex(string hex)
    {
        return ConsoleColor.Gray;
    }
}
