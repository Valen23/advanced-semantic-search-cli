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

    /// <summary>
    /// Directorio base utilizado para el almacenamiento de la memoria local.
    /// </summary>
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

    /// <summary>
    /// Intenta parsear un string de filtro en el formato "clave:valor".
    /// </summary>
    /// <param name="filterArg">El string de entrada (ej: "category:docs").</param>
    /// <returns>Un MemoryFilter configurado, o null si el formato es inválido o el string es nulo.</returns>
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

            string documentId = NormalizeDocumentId(relativePath);

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

    /// <summary>
    /// Realiza una consulta asíncrona devolviendo un stream de texto y los resultados de la búsqueda (RAG).
    /// </summary>
    /// <param name="question">La pregunta del usuario.</param>
    /// <param name="language">Idioma final de la respuesta (instrucción en el prompt).</param>
    /// <param name="filterArg">Filtro por tag (opcional, formato "clave:valor").</param>
    /// <returns>Un objeto con los resultados de Kernel Memory y el stream de Semantic Kernel.</returns>
    public async Task<SemanticStreamResult> AskQuestionStreamAsync(
        string question,
        string language,
        string? filterArg
    )
    {
        MemoryFilter? myFilter = ParseFilter(filterArg);

        SearchResult searchResult = await _memory.SearchAsync(question, filter: myFilter);

        string contextBuilder = string.Empty;
        foreach (var result in searchResult.Results)
        {
            foreach (var partition in result.Partitions)
            {
                contextBuilder += partition.Text + "\n";
            }
        }

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

        var builder = Kernel.CreateBuilder();
        builder.AddOllamaChatCompletion(_textModel, new Uri(_ollamaUrl));
        var kernel = builder.Build();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var streamingResult = chatService.GetStreamingChatMessageContentsAsync(finalPrompt);

        async IAsyncEnumerable<string> GetTextStream()
        {
            await foreach (var chunk in streamingResult)
            {
                yield return chunk.Content ?? string.Empty;
            }
        }

        return new SemanticStreamResult(searchResult, GetTextStream());
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
