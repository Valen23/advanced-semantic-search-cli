using Interfaces;
using UI;

namespace CLI.Routing;

public class DomainCommandRouter
{
    private readonly ISemanticMotor _motor;

    public DomainCommandRouter(ISemanticMotor motor)
    {
        _motor = motor;
    }

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

        // 1. EFECTO DE CARGA (SPINNER)
        var cts = new CancellationTokenSource();
        var spinnerTask = Task.Run(() =>
            ShowSpinner("Buscando en la base de datos semántica...", t, cts.Token)
        );

        try
        {
            // 2. LLAMADA AL MOTOR
            var result = await _motor.AskQuestionStreamAsync(question, language, filter);

            // DETENEMOS SPINNER
            cts.Cancel();
            await spinnerTask;
            Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r"); // Limpia línea del spinner

            // 3. RENDERIZADO DE CITAS (FUENTES)
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

            // 4. HEADER DE RESPUESTA
            Console.WriteLine(
                $"\n{t.Primary}{TerminalColors.Bold}  RESPUESTA:{TerminalColors.Reset}"
            );
            Console.ForegroundColor = GetConsoleColorFromHex(t.Primary);

            // 5. CONSUMO DEL STREAM
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

    private void ShowSpinner(string message, CliTheme t, CancellationToken token)
    {
        string[] frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        int i = 0;
        while (!token.IsCancellationRequested)
        {
            Console.Write(
                $"\r{t.Accent}{frames[i % frames.Length]} {message}{TerminalColors.Reset}"
            );
            i++;
            Thread.Sleep(100);
        }
    }

    private ConsoleColor GetConsoleColorFromHex(string hex)
    {
        // Mapeo simple de colores comunes para la consola basados en los códigos ANSI
        // Esto es un fallback, ya que estamos usando códigos ANSI directamente en las propiedades del tema.
        // Pero para el streaming usaremos el color primario directamente si es posible.
        // Dado que t.Primary contiene el código ANSI (ej: \u001b[38;5;...),
        // lo mejor es imprimir el código ANSI antes del loop.
        return ConsoleColor.Gray; // No se usa si inyectamos el ANSI directamente
    }
}
