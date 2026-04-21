using Interfaces;

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
        string filter
    )
    {
        switch (command)
        {
            case "ask":
                if (arguments.Length == 0)
                {
                    Console.WriteLine("Uso correcto: ask \"¿Cuál es la capital de Francia?\"");
                    return;
                }
                else if (arguments.Length > 1)
                {
                    Console.WriteLine(
                        "[Aviso]: Detectamos varias palabras sin comillas. Solo se procesará la primera."
                    );
                    Console.WriteLine("Uso correcto: ask \"¿Cuál es la capital de Francia?\"");
                }

                await _motor.AskQuestionAsync(arguments[0], language, filter);
                break;

            case "ingest":
                if (arguments.Length == 0)
                {
                    Console.WriteLine("Uso correcto: ingest \"ruta-del-archivo.pdf\"");
                    return;
                }

                if (arguments.Length > 1)
                {
                    Console.WriteLine(
                        "[Aviso]: Ruta con espacios detectada sin comillas. ¿Olvidaste las comillas?"
                    );
                }

                await _motor.IngestAsync(arguments[0], "Docs");
                break;

            case "ingest-folder":
                if (arguments.Length == 0)
                {
                    Console.WriteLine("Uso correcto: ingest-folder \"ruta/carpeta\"");
                    return;
                }
                await _motor.IngestFolderAsync(arguments[0]);
                break;

            case "delete":
                if (arguments.Length == 0)
                {
                    Console.WriteLine("Uso correcto: delete \"id-del-documento\"");
                    return;
                }
                await _motor.DeleteDocumentAsync(arguments[0]);
                break;

            default:
                throw new ArgumentException($"Comando de dominio '{command}' no reconocido.");
        }
    }
}
