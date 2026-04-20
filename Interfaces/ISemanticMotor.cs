namespace Interfaces;

public interface ISemanticMotor
{
    Task IngestAsync(string filePath, string folderPath);
    Task IngestFolderAsync(string folderPath);
    Task<string> AskQuestionAsync(string question, string language, string? filterTag);
    Task DeleteDocumentAsync(string fileName);
}
