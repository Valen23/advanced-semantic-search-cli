namespace Interfaces;

public record SemanticStreamResult(
    Microsoft.KernelMemory.SearchResult SearchResult,
    IAsyncEnumerable<string> TextStream
);

public interface ISemanticMotor
{
    string StorageDirectory { get; set; }
    Task IngestAsync(string filePath, string folderPath);
    Task IngestFolderAsync(string folderPath);
    Task<string> AskQuestionAsync(string question, string language, string? filterTag);
    Task<SemanticStreamResult> AskQuestionStreamAsync(
        string question,
        string language,
        string? filterTag
    );
    Task DeleteDocumentAsync(string fileName);
}
