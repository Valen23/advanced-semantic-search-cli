namespace Interfaces;

public interface ISemanticMotor
{
    Task IngestAsync(string filePath, string folderPath);
    Task IngestFolderAsync(string folderPath);
    Task<string> AskQuestionAsync(string question, string language, string filterTag = null);
    Task DeleteDocumentAsync(string fileName);
}
