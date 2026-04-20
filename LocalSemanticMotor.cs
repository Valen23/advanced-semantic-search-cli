using Interfaces;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;

public class LocalSemanticMotor : ISemanticMotor
{
    private readonly IKernelMemory _memory;

    // El constructor inicializa la configuración de Kernel Memory y Ollama
    public LocalSemanticMotor(
        string storageDirectory,
        string ollamaUrl,
        string textModel,
        string embeddingModel
    )
    {
        // storage
        var _storageDirectory = storageDirectory;
        Directory.CreateDirectory(_storageDirectory);
        // ollama
        var _ollamaEndpoint = ollamaUrl;
        var config = new OllamaConfig
        {
            Endpoint = _ollamaEndpoint,
            TextModel = new OllamaModelConfig(textModel),
            EmbeddingModel = new OllamaModelConfig(embeddingModel),
        };
        // memory
        _memory = new KernelMemoryBuilder()
            .WithOllamaTextGeneration(config)
            .WithOllamaTextEmbeddingGeneration(config)
            .WithSimpleFileStorage(
                new SimpleFileStorageConfig
                {
                    Directory = _storageDirectory,
                    StorageType = FileSystemTypes.Disk,
                }
            )
            .WithSimpleVectorDb(
                new SimpleVectorDbConfig
                {
                    Directory = _storageDirectory,
                    StorageType = FileSystemTypes.Disk,
                }
            )
            .Build<MemoryServerless>();
    }

    private string NormalizeDocumentId(string rawName)
    {
        string normalizedDocId = Path.GetFileNameWithoutExtension(rawName)
            .ToLower()
            .Replace(" ", "-");
        return normalizedDocId;
    }

    public async Task IngestAsync(string filePath, string folderPath)
    {
        // validacion
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: Archivo no encontrado en '{filePath}'");
            return;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);

        string documentId = NormalizeDocumentId(filePath);

        Console.WriteLine($"2. Ingiriendo documento: {filePath}...");

        var fileTags = new TagCollection();

        string relativePath = Path.GetRelativePath(folderPath, filePath);
        string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar);

        for (int i = 0; i < pathParts.Length - 1; i++)
        {
            fileTags.Add("category", pathParts[i]);
        }

        fileTags.Add("formato", extension.Replace(".", ""));
        fileTags.Add("fecha_ingesta", DateTime.Now.ToString("yyyy-MM-dd"));

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

    public async Task IngestFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return;

        Console.WriteLine($"2. Escaneando directorio: {folderPath}...");

        // lista todas las rutas de todos los archivos filtrando por su extension
        string[] extensions = { ".pdf", ".txt" };
        var files = Directory
            .EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        // itera por todo el array
        foreach (var filePath in files)
        {
            Console.WriteLine($"   Procesando: {filePath}");

            var fileTags = new TagCollection();
            string fileName = Path.GetFileNameWithoutExtension(filePath);

            string relativePath = Path.GetRelativePath(folderPath, filePath);
            string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar);

            if (pathParts.Length > 1)
            {
                fileTags.Add("department", pathParts[0]);
            }

            for (int i = 1; i < pathParts.Length - 1; i++)
            {
                fileTags.Add("extra_tag", pathParts[i]);
            }

            fileTags.Add("file_name", fileName);
            fileTags.Add("ingest_date", DateTime.Now.ToString("dd-MM-yyyy"));

            await _memory.ImportDocumentAsync(
                filePath,
                documentId: NormalizeDocumentId(fileName),
                tags: fileTags
            );
        }
        Console.WriteLine($"[OK] Ingesta por lotes completada completada.");
    }

    public async Task<string> AskQuestionAsync(string question, string language, string? filterArg)
    {
        var promptFinal =
            $"{question}\n\n[INSTRUCCIÓN ESTRICTA: Redacta tu respuesta final única y exclusivamente en {language}]\n[INSTRUCCIÓN ESTRICTA: Respuestas breves].";

        // 1. Inicializamos el filtro como nulo por defecto
        MemoryFilter? myFilter = null;

        // 2. Verificamos si el usuario envió un cuarto argumento (el filtro)
        if (!string.IsNullOrWhiteSpace(filterArg))
        {
            var parts = filterArg.Split(':');

            if (parts.Length == 2)
            {
                // Construimos el filtro dinámicamente
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

        // 3. Pasamos el filtro (si es nulo, Kernel Memory ignora el filtro y busca en todo)
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

            // Recorremos los "chunks" exactos que extrajo de este documento
            int chunkIndex = 1;
            foreach (var partition in citation.Partitions)
            {
                // partition.Relevance te da el score de similitud vectorial
                // partition.Text te da el extracto original
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
