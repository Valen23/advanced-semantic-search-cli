using System.Linq;
using System.Text.RegularExpressions;
using Interfaces;

namespace Repl;

public class ReplEnvironment
{
    private readonly ISemanticMotor _motor;
    private string _currentLanguage = "español";
    private string _currentFilter = string.Empty;

    public ReplEnvironment(ISemanticMotor motor)
    {
        _motor = motor;
    }

    public async Task StartLoopAsync()
    {
        Console.WriteLine("Iniciando Modo Interactivo (REPL). Escribe 'exit' para salir.");
        bool isRunning = true;

        string command;
        string argument;

        while (isRunning)
        {
            Console.Write($"\nrag-cli> [{_currentLanguage}] [filtro:{_currentFilter}]");
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
                        Console.WriteLine("Uso: set-filter \"category:Contabilidad\"");
                        continue;
                    }
                    else
                    {
                        _currentFilter = argument;
                        Console.WriteLine($"Filtro de la sesión cambiado a: {_currentFilter}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error en el comando]: {ex.Message}");
            }
        }

        Console.WriteLine("Sesión finalizada. Cerrando motor...");
    }
}
