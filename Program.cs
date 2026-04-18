using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;

// 1. Validación de entrada (Argumentos dinámicos)
if (args.Length < 2)
{
    Console.WriteLine("Uso incorrecto de la CLI.");
    Console.WriteLine("Ejecutar como: dotnet run <ruta_al_archivo> \"<tu_pregunta>\"");
    return;
}

var filePath = args[0];
var question = args[1];

if (!File.Exists(filePath))
{
    Console.WriteLine($"Error: No se encontró el archivo en la ruta '{filePath}'.");
    return;
}

Console.WriteLine("1. Iniciando entorno y conectando con Ollama...");

try
{
    // 2. Configuración del Motor (Serverless + In-Memory)
    var ollamaEndpoint = "http://localhost:11434";
    var config = new OllamaConfig
    {
        Endpoint = ollamaEndpoint,
        TextModel = new OllamaModelConfig("llama3"),
        EmbeddingModel = new OllamaModelConfig("nomic-embed-text"),
    };

    var memory = new KernelMemoryBuilder()
        .WithOllamaTextGeneration(config)
        .WithOllamaTextEmbeddingGeneration(config)
        .WithSimpleVectorDb()
        .Build<MemoryServerless>();

    Console.WriteLine($"2. Ingiriendo documento: {filePath}...");

    // Usamos el nombre del archivo como ID interno para Kernel Memory
    await memory.ImportDocumentAsync(filePath, documentId: Path.GetFileName(filePath));

    Console.WriteLine($"3. Documento vectorizado. Buscando respuesta a: '{question}'...");

    // 3. Ejecución de la consulta
    var answer = await memory.AskAsync(question);

    Console.WriteLine("\n================ RESPUESTA ================");
    Console.WriteLine(answer.Result);
    Console.WriteLine("===========================================\n");
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
