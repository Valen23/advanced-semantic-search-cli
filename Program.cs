using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;

// 1. Enrutador de comandos
if (args.Length < 2)
{
    Console.WriteLine("Uso de la CLI:");
    Console.WriteLine("  Para ingerir:  dotnet run ingest <ruta_al_archivo>");
    Console.WriteLine("  Para buscar:   dotnet run ask \"<tu_pregunta>\" [idioma_opcional]");
    return;
}

var command = args[0].ToLower();
var argument = args[1];

Console.WriteLine("1. Iniciando entorno y configurando almacenamiento local...");

try
{
    // 2. Configuración de Persistencia Local
    var storageDirectory = "MemoriaLocal";
    Directory.CreateDirectory(storageDirectory);

    var ollamaEndpoint = "http://localhost:11434";
    var config = new OllamaConfig
    {
        Endpoint = ollamaEndpoint,
        TextModel = new OllamaModelConfig("llama3"),
        EmbeddingModel = new OllamaModelConfig("nomic-embed-text"),
    };

    // 3. Construimos el motor CON persistencia en disco
    var memory = new KernelMemoryBuilder()
        .WithOllamaTextGeneration(config)
        .WithOllamaTextEmbeddingGeneration(config)
        .WithSimpleFileStorage(
            new SimpleFileStorageConfig
            {
                Directory = storageDirectory,
                StorageType = FileSystemTypes.Disk,
            }
        )
        .WithSimpleVectorDb(
            new SimpleVectorDbConfig
            {
                Directory = storageDirectory,
                StorageType = FileSystemTypes.Disk,
            }
        )
        .Build<MemoryServerless>();

    // 4. Ejecución de la acción solicitada
    if (command == "ingest")
    {
        var filePath = argument;
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: Archivo no encontrado en '{filePath}'");
            return;
        }

        Console.WriteLine($"2. Ingiriendo documento: {filePath}...");
        Console.WriteLine("Procesando vectores... (esto depende enteramente de tu CPU/GPU)");

        // Usamos el nombre del archivo como ID para la base de datos
        await memory.ImportDocumentAsync(filePath, documentId: Path.GetFileName(filePath));

        Console.WriteLine(
            "\n¡Ingesta completada exitosamente! Los vectores están seguros en el disco."
        );
    }
    else if (command == "ask")
    {
        var question = argument;
        var language = args.Length > 2 ? args[2] : "español";
        var promptFinal =
            $"{question}\n\n[INSTRUCCIÓN ESTRICTA: Redacta tu respuesta final única y exclusivamente en {language}]\n[INSTRUCCIÓN ESTRICTA: Respuestas breves].";

        Console.WriteLine(
            $"2. Buscando en la base de datos vectorial y redactando en {language}..."
        );

        var answer = await memory.AskAsync(promptFinal);

        Console.WriteLine("\n================ RESPUESTA ================");
        Console.WriteLine(answer.Result);
        Console.WriteLine("===========================================\n");
    }
    else if (command == "delete")
    {
        var fileName = argument;

        Console.WriteLine(
            $"2. Buscando y eliminando el documento '{fileName}' de la memoria local..."
        );
        await memory.DeleteDocumentAsync(documentId: fileName);
        Console.WriteLine(
            "¡Operación completada! Los vectores de este documento han sido borrados del disco."
        );
    }
    else
    {
        Console.WriteLine($"Comando '{command}' no reconocido. Usa 'ingest' o 'ask'.");
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
