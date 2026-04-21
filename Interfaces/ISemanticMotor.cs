namespace Interfaces;

/// <summary>
/// Representa el resultado de una búsqueda semántica con soporte para streaming.
/// </summary>
/// <param name="SearchResult">Metadatos de la búsqueda y fuentes relevantes encontradas.</param>
/// <param name="TextStream">Flujo asíncrono de tokens de texto generados por el modelo.</param>
public record SemanticStreamResult(
    Microsoft.KernelMemory.SearchResult SearchResult,
    IAsyncEnumerable<string> TextStream
);

/// <summary>
/// Define las operaciones principales de un motor semántico para ingesta y consulta de documentos.
/// </summary>
public interface ISemanticMotor
{
    /// <summary>
    /// Ruta del directorio de almacenamiento de vectores y documentos.
    /// </summary>
    string StorageDirectory { get; set; }

    /// <summary>
    /// Ingiere un archivo individual en la memoria semántica.
    /// </summary>
    /// <param name="filePath">Ruta del archivo físico.</param>
    /// <param name="folderPath">Carpeta base para calcular rutas relativas y metadatos.</param>
    Task IngestAsync(string filePath, string folderPath);

    /// <summary>
    /// Ingiere todos los documentos compatibles de una carpeta de forma recursiva.
    /// </summary>
    /// <param name="folderPath">Ruta de la carpeta a escanear.</param>
    Task IngestFolderAsync(string folderPath);

    /// <summary>
    /// Realiza una consulta síncrona que devuelve la respuesta completa como string.
    /// </summary>
    /// <param name="question">La pregunta del usuario.</param>
    /// <param name="language">Idioma de la respuesta esperada.</param>
    /// <param name="filterTag">Opcional. Filtro para restringir la búsqueda (ej. "category:docs").</param>
    /// <returns>La respuesta generada por el motor.</returns>
    Task<string> AskQuestionAsync(string question, string language, string? filterTag);

    /// <summary>
    /// Realiza una consulta asíncrona que permite consumir la respuesta mediante streaming.
    /// </summary>
    /// <param name="question">La pregunta del usuario.</param>
    /// <param name="language">Idioma de la respuesta esperada.</param>
    /// <param name="filterTag">Opcional. Filtro para restringir la búsqueda.</param>
    /// <returns>Un objeto con los resultados de búsqueda y el flujo de texto.</returns>
    Task<SemanticStreamResult> AskQuestionStreamAsync(
        string question,
        string language,
        string? filterTag
    );

    /// <summary>
    /// Elimina un documento y sus vectores de la memoria semántica.
    /// </summary>
    /// <param name="fileName">Identificador o nombre del archivo a eliminar.</param>
    Task DeleteDocumentAsync(string fileName);
}
