using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Repl;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

string? storageDirectory = configuration["SemanticEngine:StorageDirectory"];
string? ollamaUrl = configuration["SemanticEngine:OllamaEndpoint"];
string? textModel = configuration["SemanticEngine:TextModel"];
string? embeddingModel = configuration["SemanticEngine:EmbeddingModel"];

if (string.IsNullOrWhiteSpace(storageDirectory))
    throw new Exception("Falta configuración crítica: 'SemanticEngine:StorageDirectory'");

if (string.IsNullOrWhiteSpace(ollamaUrl))
    throw new Exception("Falta configuración crítica: 'SemanticEngine:OllamaEndpoint'");

if (string.IsNullOrWhiteSpace(textModel))
    throw new Exception("Falta configuración crítica: 'SemanticEngine:TextModel'");

if (string.IsNullOrWhiteSpace(embeddingModel))
    throw new Exception("Falta configuración crítica: 'SemanticEngine:EmbeddingModel'");

var localSemanticMotor = new LocalSemanticMotor(
    storageDirectory,
    ollamaUrl,
    textModel,
    embeddingModel
);

if (args.Length == 0)
{
    var repl = new ReplEnvironment(localSemanticMotor);
    await repl.StartLoopAsync();
    return;
}

if (args.Length < 2)
{
    Console.WriteLine("Uso de la CLI:");
    Console.WriteLine("  Para ingerir archivo:   dotnet run ingest \"<ruta>\"");
    Console.WriteLine("  Para ingerir carpeta:   dotnet run ingest-folder \"<ruta>\"");
    Console.WriteLine(
        "  Para buscar:            dotnet run ask \"<pregunta>\" [idioma] [category:valor]"
    );
    Console.WriteLine("  Para eliminar:          dotnet run delete \"<id_o_ruta>\"");
    return;
}

var command = args[0].ToLower();
var argument = args[1];

Console.WriteLine("1. Iniciando entorno y configurando almacenamiento local...");

try
{
    if (command == "ingest")
    {
        await localSemanticMotor.IngestAsync(argument, "Docs");
    }
    else if (command == "ingest-folder")
    {
        await localSemanticMotor.IngestFolderAsync(argument);
    }
    else if (command == "ask")
    {
        string language = args.Length > 2 ? args[2] : "español";
        var filter = args.Length > 3 ? args[3] : "";
        await localSemanticMotor.AskQuestionAsync(argument, language, filter);
    }
    else if (command == "delete")
    {
        await localSemanticMotor.DeleteDocumentAsync(argument);
    }
    else
    {
        Console.WriteLine(
            $"Comando '{command}' no reconocido. Usa 'ingest', 'ingest-folder', 'ask' o 'delete'."
        );
    }
}
catch (HttpRequestException ex)
{
    Console.WriteLine("\n[ERROR FATAL] No se pudo conectar a Ollama.");
    Console.WriteLine(
        "¿Te aseguraste de que la aplicación de Ollama esté abierta y ejecutándose en segundo plano?"
    );
    Console.WriteLine($"Detalle técnico: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"\n[ERROR INESPERADO] {ex.Message}");
}
