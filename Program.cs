using System.IO;
using System.Linq;
using CLI.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Repl;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

Console.OutputEncoding = System.Text.Encoding.UTF8;

string? storageDirectory = configuration["SemanticEngine:StorageDirectory"];
string? ollamaUrl = configuration["SemanticEngine:OllamaEndpoint"];
string? textModel = configuration["SemanticEngine:TextModel"];
string? embeddingModel = configuration["SemanticEngine:EmbeddingModel"];
string? initialTheme = configuration["SemanticEngine:Theme"] ?? "Cyberpunk";

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

var router = new DomainCommandRouter(localSemanticMotor);

if (args.Length == 0)
{
    var repl = new ReplEnvironment(localSemanticMotor, initialTheme);
    await repl.StartLoopAsync();
    return;
}

if (args.Length < 2)
{
    Console.WriteLine("Uso de la CLI:");
    Console.WriteLine("  Para iniciar el programa: dotnet run");
    Console.WriteLine("  Una vez iniciado podes ver la lista de comandos usando: help");
    return;
}

var command = args[0].ToLower();
var argument = args[1];

var theme = UI.ThemeLibrary.GetTheme(initialTheme);
await router.ExecuteAsync(command, args.Skip(1).ToArray(), "español", "", theme);
