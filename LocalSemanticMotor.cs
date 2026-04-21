using Interfaces;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

/// <summary>
/// Provee la lógica central del "Motor Semántico", integrando Microsoft Kernel Memory
/// con Ollama para la ingesta y búsqueda de documentos en lenguaje natural.
/// </summary>
public class LocalSemanticMotor : ISemanticMotor
{
    private readonly IKernelMemory _memory;
    private readonly string _ollamaUrl;
    private readonly string _textModel;

    public string StorageDirectory { get; set; }

    /// <summary>
    /// Inicializa una nueva instancia del motor semántico configurando el almacenamiento y los modelos de IA.
    /// </summary>
    /// <param name="storageDirectory">Directorio local donde se guardarán los vectores y documentos.</param>
    /// <param name="ollamaUrl">URL del endpoint de Ollama (ej. http://localhost:11434).</param>
    /// <param name="textModel">Nombre del modelo para generación de texto (ej. llama3).</param>
    /// <param name="embeddingModel">Nombre del modelo para generación de embeddings (ej. nomic-embed-text).</param>
    public LocalSemanticMotor(
        string storageDirectory,
        string ollamaUrl,
        string textModel,
        string embeddingModel
    )
    {
        this.StorageDirectory = storageDirectory;
        Directory.CreateDirectory(storageDirectory);

        _ollamaUrl = ollamaUrl;
        _textModel = textModel;

        var _ollamaEndpoint = ollamaUrl;
        var config = new OllamaConfig
        {
            Endpoint = _ollamaEndpoint,
            TextModel = new OllamaModelConfig(textModel),
            EmbeddingModel = new OllamaModelConfig(embeddingModel),
        };

        _memory = new KernelMemoryBuilder()
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
    }

    /// <summary>
    /// Normaliza una cadena (como una ruta relativa) para ser usado como DocumentId compatible con Kernel Memory.
    /// Reemplaza espacios y separadores de carpeta por guiones medios.
    /// </summary>
    private string NormalizeDocumentId(string rawPath)
    {
        return rawPath
            .ToLower()
            .Replace(Path.DirectorySeparatorChar, '-')
            .Replace(Path.AltDirectorySeparatorChar, '-')
            .Replace(" ", "-")
            .Trim('-');
    }

    private MemoryFilter? ParseFilter(string? filterArg)
    {
        if (string.IsNullOrWhiteSpace(filterArg))
            return null;
        var parts = filterArg.Split(':');
        if (parts.Length == 2)
        {
            return new MemoryFilter().ByTag(parts[0], parts[1]);
        }
        return null;
    }

    /// <summary>
    /// Ingiere un único documento en la memoria semántica, asignando etiquetas automáticas basadas en la ruta.
    /// </summary>
    /// <param name="filePath">Ruta completa del archivo a procesar.</param>
    /// <param name="folderPath">Ruta raíz del escaneo para determinar categorías jerárquicas.</param>
    public async Task IngestAsync(string filePath, string folderPath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: Archivo no encontrado en '{filePath}'");
            return;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        string relativePath = Path.GetRelativePath(folderPath, filePath);
        string documentId = NormalizeDocumentId(relativePath);

        Console.WriteLine($"2. Ingiriendo documento: {filePath}...");

        var fileTags = new TagCollection();
        string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar);

        // Agregamos cada carpeta del path como una categoría
        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            fileTags.Add("category", pathParts[i]);
        }

        fileTags.Add("format", extension.Replace(".", ""));
        fileTags.Add("ingest_date", DateTime.Now.ToString("yyyy-MM-dd"));

        try
        {
            await _memory.ImportDocumentAsync(filePath, documentId: documentId, tags: fileTags);

            Console.WriteLine($"[OK] Ingesta completada: {documentId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Falló la ingesta de {fileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Escanea un directorio completo e ingiere todos los archivos compatibles (.pdf, .txt).
    /// </summary>
    /// <param name="folderPath">Ruta del directorio a escanear.</param>
    public async Task IngestFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        Console.WriteLine($"2. Escaneando directorio: {folderPath}...");

        string[] extensions = { ".pdf", ".txt" };
        var files = Directory
            .EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        foreach (var filePath in files)
        {
            Console.WriteLine($"   Procesando: {filePath}");

            var fileTags = new TagCollection();
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            string relativePath = Path.GetRelativePath(folderPath, filePath);
            string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar);

            // Evitamos colisiones usando el relativePath como DocumentId
            string documentId = NormalizeDocumentId(relativePath);

            // Tags jerárquicos consistentes
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                fileTags.Add("category", pathParts[i]);
            }

            fileTags.Add("file_name", fileName);
            fileTags.Add("ingest_date", DateTime.Now.ToString("yyyy-MM-dd"));

            await _memory.ImportDocumentAsync(filePath, documentId: documentId, tags: fileTags);
        }
        Console.WriteLine($"[OK] Ingesta por lotes completada.");
    }

    /// <summary>
    /// Realiza una consulta en lenguaje natural sobre los documentos ingeridos.
    /// </summary>
    /// <param name="question">La pregunta o prompt del usuario.</param>
    /// <param name="language">Idioma en el que se espera la respuesta (ej. "español").</param>
    /// <param name="filterArg">Opcional. Filtro en formato "clave:valor" para restringir la búsqueda.</param>
    /// <returns>La respuesta generada por el LLM basada en los fragmentos recuperados.</returns>
    public async Task<string> AskQuestionAsync(string question, string language, string? filterArg)
    {
        var promptFinal =
            $"{question}\n\n[INSTRUCCIÓN ESTRICTA: Redacta tu respuesta final única y exclusivamente en {language}]\n[INSTRUCCIÓN ESTRICTA: Respuestas breves].";

        MemoryFilter? myFilter = null;

        if (!string.IsNullOrWhiteSpace(filterArg))
        {
            var parts = filterArg.Split(':');

            if (parts.Length == 2)
            {
                myFilter = new MemoryFilter().ByTag(parts[0], parts[1]);
                Console.WriteLine(
                    $"2. [Filtro Activo] Restringiendo búsqueda a '{parts[0]}' = '{parts[1]}'"
                );
            }
            else
            {
                Console.WriteLine(
                    "Advertencia: Formato de filtro incorrecto. Usa 'clave:valor'. Buscando en toda la base de datos..."
                );
            }
        }
        else
        {
            Console.WriteLine(
                $"2. Buscando en toda la base de datos local y redactando en {language}..."
            );
        }

        var answer = await _memory.AskAsync(promptFinal, filter: myFilter);

        Console.WriteLine("\n================ RESPUESTA ================");
        Console.WriteLine(answer.Result);
        Console.WriteLine("===========================================\n");

        Console.WriteLine(
            "\n================ [Fuentes utilizadas para redactar esto]: ================"
        );
        foreach (var citation in answer.RelevantSources)
        {
            Console.WriteLine($"\n Documento: {citation.SourceName}");

            int chunkIndex = 1;
            foreach (var partition in citation.Partitions)
            {
                Console.WriteLine(
                    $"  ├─ Fragmento {chunkIndex} (Relevancia: {partition.Relevance:P1}):"
                );
                Console.WriteLine($"  │  \"{partition.Text.Trim()}\"");
                chunkIndex++;
            }
        }
        Console.WriteLine(
            "==========================================================================\n"
        );
        return answer.Result;
    }

    public async Task AskQuestionStreamAsync(string question, string language, string? filterArg)
    {
        // Lógica de parseo de filtro (ya la tienes resuelta)
        MemoryFilter? myFilter = ParseFilter(filterArg);

        Console.WriteLine($"2. Buscando fragmentos relevantes en la base de datos local...");

        // PASO 1: RETRIEVAL (Solo buscar, no generar texto)
        SearchResult searchResult = await _memory.SearchAsync(question, filter: myFilter);

        // PASO 2: CONSTRUCCIÓN DEL CONTEXTO (Tu tarea)
        string contextBuilder = string.Empty;
        // TODO: Iterar sobre searchResult.Results y sus Partitions.
        // Extraer el texto de cada partición y concatenarlo en la variable contextBuilder.
        // Imprimir las fuentes (citas) en este momento, antes de generar la respuesta.
        foreach (var result in searchResult.Results)
        {
            Console.WriteLine($"\n Fuente: {result.SourceName}");
            foreach (var partition in result.Partitions)
            {
                contextBuilder += partition.Text + "\n";
                Console.WriteLine($"  ├─ Relevancia: {partition.Relevance:P1}");
            }
        }

        // PASO 3: EL PROMPT MAESTRO
        string finalPrompt =
            $@"
        Basado en la siguiente información de contexto:
        ---
        {contextBuilder}
        ---
        Responde a la pregunta: {question}
        [INSTRUCCIÓN ESTRICTA: Redacta tu respuesta única y exclusivamente en {language}]
        [INSTRUCCIÓN ESTRICTA: Respuestas breves]
    ";

        Console.WriteLine("\n================ RESPUESTA ================");
        // 1. Configuramos el cliente de streaming
        var builder = Kernel.CreateBuilder();
        builder.AddOllamaChatCompletion(_textModel, new Uri(_ollamaUrl));
        var kernel = builder.Build();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        // 2. Iniciamos el streaming
        var streamingResult = chatService.GetStreamingChatMessageContentsAsync(finalPrompt);

        await foreach (var chunk in streamingResult)
        {
            Console.Write(chunk.Content); // Imprime cada palabra/token en tiempo real
        }

        Console.WriteLine("\n===========================================\n");
    }

    /// <summary>
    /// Elimina físicamente los vectores y fragmentos de un documento guardado en la memoria.
    /// </summary>
    /// <param name="fileName">Nombre del archivo o DocumentId a eliminar.</param>
    public async Task DeleteDocumentAsync(string fileName)
    {
        Console.WriteLine(
            $"2. Buscando y eliminando el documento '{fileName}' de la memoria local..."
        );
        await _memory.DeleteDocumentAsync(documentId: NormalizeDocumentId(fileName));
        Console.WriteLine(
            "¡Operación completada! Los vectores de este documento han sido borrados del disco."
        );
    }
}
