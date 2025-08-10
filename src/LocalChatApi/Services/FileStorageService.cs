using LocalChatApi.Models;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using OfficeOpenXml;
using System.Text.RegularExpressions;

namespace LocalChatApi.Services;

/// <summary>
/// Interface for file storage service
/// </summary>
public interface IFileStorageService
{
    Task<string> SaveFileAsync(FileDocument fileDoc);
    Task<string> SavePhysicalFileAsync(IFormFile file, string fileId);
    Task<string?> ReadFileContentAsync(string fileId);
    Task<FileDocument?> GetFileAsync(string fileId);
    Task<List<FileDocument>> GetSessionFilesAsync(string sessionId);
    Task<bool> DeleteFileAsync(string fileId);
    Task<List<DocumentChunk>> SearchSimilarChunksAsync(string fileId, float[] queryEmbedding, int topK = 5);
}

/// <summary>
/// Result of file content extraction
/// </summary>
public class FileContentExtractionResult
{
    public string Content { get; set; } = string.Empty;
    public string ExtractedBy { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// MongoDB implementation of file storage service with file system storage and comprehensive file type support
/// </summary>
public class MongoDbFileStorageService : IFileStorageService
{
    private readonly IMongoCollection<FileDocument> _filesCollection;
    private readonly ILogger<MongoDbFileStorageService> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly string _uploadPath;

    // Supported file types mapping
    private static readonly Dictionary<string, string[]> SupportedFileTypes = new()
    {
        { "text", new[] { ".txt", ".md", ".csv", ".json", ".xml", ".html", ".htm", ".log" } },
        { "pdf", new[] { ".pdf" } },
        { "word", new[] { ".docx", ".doc" } },
        { "excel", new[] { ".xlsx", ".xls" } },
        { "powerpoint", new[] { ".pptx", ".ppt" } },
        { "rtf", new[] { ".rtf" } },
        { "code", new[] { ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".css", ".sql" } }
    };

    public MongoDbFileStorageService(
        IMongoDatabase database,
        ILogger<MongoDbFileStorageService> logger,
        IWebHostEnvironment environment)
    {
        _filesCollection = database.GetCollection<FileDocument>("FileDocuments");
        _logger = logger;
        _environment = environment;

        // Create uploads folder if it doesn't exist
        _uploadPath = System.IO.Path.Combine(_environment.ContentRootPath, "uploads");
        if (!Directory.Exists(_uploadPath))
        {
            Directory.CreateDirectory(_uploadPath);
            _logger.LogInformation("Created uploads directory: {UploadPath}", _uploadPath);
        }

        // Set EPPlus license context for non-commercial use
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        // Create indexes
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        // Index on fileId
        _filesCollection.Indexes.CreateOne(
            new CreateIndexModel<FileDocument>(
                Builders<FileDocument>.IndexKeys.Ascending(x => x.FileId)));

        // Index on sessionId
        _filesCollection.Indexes.CreateOne(
            new CreateIndexModel<FileDocument>(
                Builders<FileDocument>.IndexKeys.Ascending(x => x.SessionId)));

        // Index on createdAt
        _filesCollection.Indexes.CreateOne(
            new CreateIndexModel<FileDocument>(
                Builders<FileDocument>.IndexKeys.Descending(x => x.CreatedAt)));
    }

    public async Task<string> SavePhysicalFileAsync(IFormFile file, string fileId)
    {
        try
        {
            // Validate file type
            var fileExtension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!IsFileTypeSupported(fileExtension))
            {
                throw new NotSupportedException($"File type '{fileExtension}' is not supported. Supported types: {GetSupportedFileTypesString()}");
            }

            // Create a safe filename using the fileId and original extension
            var safeFileName = $"{fileId}{fileExtension}";
            var filePath = System.IO.Path.Combine(_uploadPath, safeFileName);

            // Save the file to disk
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            _logger.LogInformation("File saved to disk: {FilePath} (Type: {FileType})", filePath, GetFileTypeCategory(fileExtension));
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file to disk with ID: {FileId}", fileId);
            throw;
        }
    }

    public async Task<string?> ReadFileContentAsync(string fileId)
    {
        try
        {
            var fileDoc = await GetFileAsync(fileId);
            if (fileDoc == null || string.IsNullOrEmpty(fileDoc.FilePath))
            {
                _logger.LogWarning("File not found or file path is empty for ID: {FileId}", fileId);
                return null;
            }

            if (!File.Exists(fileDoc.FilePath))
            {
                _logger.LogWarning("Physical file not found: {FilePath}", fileDoc.FilePath);
                return null;
            }

            // Extract content based on file type
            var extractionResult = await ExtractFileContentAsync(fileDoc.FilePath, fileDoc.ContentType);
            
            if (!extractionResult.Success)
            {
                _logger.LogError("Failed to extract content from file: {FilePath}. Error: {Error}", 
                    fileDoc.FilePath, extractionResult.ErrorMessage);
                return null;
            }

            _logger.LogInformation("File content extracted successfully for ID: {FileId} using {ExtractedBy} (Length: {ContentLength})", 
                fileId, extractionResult.ExtractedBy, extractionResult.Content.Length);
            
            return extractionResult.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file content for ID: {FileId}", fileId);
            return null;
        }
    }

    private async Task<FileContentExtractionResult> ExtractFileContentAsync(string filePath, string contentType)
    {
        var result = new FileContentExtractionResult();
        var fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            switch (GetFileTypeCategory(fileExtension))
            {
                case "text":
                    result = await ExtractTextFileContentAsync(filePath);
                    break;
                case "pdf":
                    result = await ExtractPdfContentAsync(filePath);
                    break;
                case "word":
                    result = await ExtractWordContentAsync(filePath);
                    break;
                case "excel":
                    result = await ExtractExcelContentAsync(filePath);
                    break;
                case "rtf":
                    result = await ExtractRtfContentAsync(filePath);
                    break;
                case "code":
                    result = await ExtractCodeFileContentAsync(filePath);
                    break;
                default:
                    result = new FileContentExtractionResult
                    {
                        Success = false,
                        ErrorMessage = $"Unsupported file type: {fileExtension}",
                        ExtractedBy = "UnsupportedHandler"
                    };
                    break;
            }
        }
        catch (Exception ex)
        {
            result = new FileContentExtractionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ExtractedBy = "ErrorHandler"
            };
        }

        return result;
    }

    private async Task<FileContentExtractionResult> ExtractTextFileContentAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        
        return new FileContentExtractionResult
        {
            Content = content,
            Success = true,
            ExtractedBy = $"TextFileReader ({fileExtension})",
            Metadata = new Dictionary<string, object>
            {
                ["file_size"] = new FileInfo(filePath).Length,
                ["encoding"] = "UTF-8",
                ["line_count"] = content.Split('\n').Length
            }
        };
    }

    private async Task<FileContentExtractionResult> ExtractPdfContentAsync(string filePath)
    {
        var content = new StringBuilder();
        var metadata = new Dictionary<string, object>();

        using var reader = new PdfReader(filePath);
        metadata["page_count"] = reader.NumberOfPages;
        metadata["pdf_version"] = reader.PdfVersion.ToString();

        for (int page = 1; page <= reader.NumberOfPages; page++)
        {
            var pageText = PdfTextExtractor.GetTextFromPage(reader, page);
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                content.AppendLine($"=== Page {page} ===");
                content.AppendLine(pageText);
                content.AppendLine();
            }
        }

        return new FileContentExtractionResult
        {
            Content = content.ToString(),
            Success = true,
            ExtractedBy = "PdfTextExtractor (iTextSharp)",
            Metadata = metadata
        };
    }

    private async Task<FileContentExtractionResult> ExtractWordContentAsync(string filePath)
    {
        var content = new StringBuilder();
        var metadata = new Dictionary<string, object>();

        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document.Body;
        
        if (body != null)
        {
            var paragraphs = body.Elements<Paragraph>();
            var paragraphCount = 0;

            foreach (var paragraph in paragraphs)
            {
                var paragraphText = paragraph.InnerText;
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    content.AppendLine(paragraphText);
                    paragraphCount++;
                }
            }

            metadata["paragraph_count"] = paragraphCount;
        }

        // Extract document properties
        var docProps = doc.PackageProperties;
        metadata["title"] = docProps.Title ?? "";
        metadata["author"] = docProps.Creator ?? "";
        metadata["created"] = docProps.Created?.ToString() ?? "";

        return new FileContentExtractionResult
        {
            Content = content.ToString(),
            Success = true,
            ExtractedBy = "WordDocumentProcessor (OpenXml)",
            Metadata = metadata
        };
    }

    private async Task<FileContentExtractionResult> ExtractExcelContentAsync(string filePath)
    {
        var content = new StringBuilder();
        var metadata = new Dictionary<string, object>();

        using var package = new ExcelPackage(new FileInfo(filePath));
        metadata["worksheet_count"] = package.Workbook.Worksheets.Count;

        foreach (var worksheet in package.Workbook.Worksheets)
        {
            content.AppendLine($"=== Worksheet: {worksheet.Name} ===");
            
            var start = worksheet.Dimension?.Start;
            var end = worksheet.Dimension?.End;
            
            if (start != null && end != null)
            {
                // Extract headers (first row)
                var headers = new List<string>();
                for (int col = start.Column; col <= end.Column; col++)
                {
                    var headerValue = worksheet.Cells[start.Row, col].Value?.ToString() ?? "";
                    headers.Add(headerValue);
                }
                
                if (headers.Any(h => !string.IsNullOrWhiteSpace(h)))
                {
                    content.AppendLine($"Headers: {string.Join(" | ", headers)}");
                    content.AppendLine(new string('-', 50));
                }

                // Extract data rows (limit to first 100 rows for performance)
                var maxRows = Math.Min(end.Row, start.Row + 99);
                for (int row = start.Row + 1; row <= maxRows; row++)
                {
                    var rowData = new List<string>();
                    for (int col = start.Column; col <= end.Column; col++)
                    {
                        var cellValue = worksheet.Cells[row, col].Value?.ToString() ?? "";
                        rowData.Add(cellValue);
                    }
                    
                    if (rowData.Any(d => !string.IsNullOrWhiteSpace(d)))
                    {
                        content.AppendLine(string.Join(" | ", rowData));
                    }
                }

                metadata[$"worksheet_{worksheet.Name}_rows"] = end.Row - start.Row;
                metadata[$"worksheet_{worksheet.Name}_columns"] = end.Column - start.Column + 1;
            }
            
            content.AppendLine();
        }

        return new FileContentExtractionResult
        {
            Content = content.ToString(),
            Success = true,
            ExtractedBy = "ExcelPackageProcessor (EPPlus)",
            Metadata = metadata
        };
    }

    private async Task<FileContentExtractionResult> ExtractRtfContentAsync(string filePath)
    {
        // For RTF files, we'll read as text and clean up RTF formatting
        var rawContent = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        
        // Basic RTF cleanup - remove RTF control sequences
        var cleanContent = Regex.Replace(rawContent, @"\\[a-z]+\d*\s?", " ");
        cleanContent = Regex.Replace(cleanContent, @"[{}]", "");
        cleanContent = Regex.Replace(cleanContent, @"\s+", " ");
        cleanContent = cleanContent.Trim();

        return new FileContentExtractionResult
        {
            Content = cleanContent,
            Success = true,
            ExtractedBy = "RtfTextCleaner",
            Metadata = new Dictionary<string, object>
            {
                ["original_size"] = rawContent.Length,
                ["cleaned_size"] = cleanContent.Length
            }
        };
    }

    private async Task<FileContentExtractionResult> ExtractCodeFileContentAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = System.IO.Path.GetFileName(filePath);
        
        // Add context for code files
        var contentWithContext = new StringBuilder();
        contentWithContext.AppendLine($"=== {fileName} ({fileExtension}) ===");
        contentWithContext.AppendLine(content);

        return new FileContentExtractionResult
        {
            Content = contentWithContext.ToString(),
            Success = true,
            ExtractedBy = $"CodeFileReader ({fileExtension})",
            Metadata = new Dictionary<string, object>
            {
                ["file_extension"] = fileExtension,
                ["line_count"] = content.Split('\n').Length,
                ["language"] = GetProgrammingLanguage(fileExtension)
            }
        };
    }

    private static bool IsFileTypeSupported(string fileExtension)
    {
        return SupportedFileTypes.Values.Any(extensions => extensions.Contains(fileExtension));
    }

    private static string GetFileTypeCategory(string fileExtension)
    {
        foreach (var category in SupportedFileTypes)
        {
            if (category.Value.Contains(fileExtension))
                return category.Key;
        }
        return "unknown";
    }

    private static string GetSupportedFileTypesString()
    {
        return string.Join(", ", SupportedFileTypes.Values.SelectMany(x => x));
    }

    private static string GetProgrammingLanguage(string fileExtension)
    {
        return fileExtension switch
        {
            ".cs" => "C#",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            ".py" => "Python",
            ".java" => "Java",
            ".cpp" => "C++",
            ".c" => "C",
            ".h" => "C/C++ Header",
            ".css" => "CSS",
            ".sql" => "SQL",
            _ => "Unknown"
        };
    }

    public async Task<string> SaveFileAsync(FileDocument fileDoc)
    {
        try
        {
            // For upsert operations, we need to handle the _id field properly
            // If it's a new document (null, empty or invalid Id), let MongoDB generate the _id
            if (string.IsNullOrEmpty(fileDoc.Id) || (fileDoc.Id.Length != 24 || !ObjectId.TryParse(fileDoc.Id, out _)))
            {
                // For new documents, set Id to null to let MongoDB generate it
                fileDoc.Id = null;
            }

            // Use ReplaceOneAsync with upsert=true to handle both insert and update scenarios
            var filter = Builders<FileDocument>.Filter.Eq(x => x.FileId, fileDoc.FileId);
            var result = await _filesCollection.ReplaceOneAsync(
                filter, 
                fileDoc, 
                new ReplaceOptions { IsUpsert = true });
            
            _logger.LogInformation("File metadata saved successfully with ID: {FileId} (IsAcknowledged: {IsAcknowledged}, ModifiedCount: {ModifiedCount}, UpsertedId: {UpsertedId})", 
                fileDoc.FileId, result.IsAcknowledged, result.ModifiedCount, result.UpsertedId);
            return fileDoc.FileId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file metadata with ID: {FileId}", fileDoc.FileId);
            throw;
        }
    }

    public async Task<FileDocument?> GetFileAsync(string fileId)
    {
        try
        {
            return await _filesCollection
                .Find(x => x.FileId == fileId)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file with ID: {FileId}", fileId);
            return null;
        }
    }

    public async Task<List<FileDocument>> GetSessionFilesAsync(string sessionId)
    {
        try
        {
            return await _filesCollection
                .Find(x => x.SessionId == sessionId)
                .SortByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving files for session: {SessionId}", sessionId);
            return new List<FileDocument>();
        }
    }

    public async Task<bool> DeleteFileAsync(string fileId)
    {
        try
        {
            // First get the file to get the physical path
            var fileDoc = await GetFileAsync(fileId);

            // Delete from MongoDB
            var result = await _filesCollection.DeleteOneAsync(x => x.FileId == fileId);

            // Delete physical file if it exists
            if (fileDoc != null && !string.IsNullOrEmpty(fileDoc.FilePath) && File.Exists(fileDoc.FilePath))
            {
                File.Delete(fileDoc.FilePath);
                _logger.LogInformation("Physical file deleted: {FilePath}", fileDoc.FilePath);
            }

            _logger.LogInformation("File deleted successfully with ID: {FileId}", fileId);
            return result.DeletedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file with ID: {FileId}", fileId);
            return false;
        }
    }

    public async Task<List<DocumentChunk>> SearchSimilarChunksAsync(string fileId, float[] queryEmbedding, int topK = 5)
    {
        try
        {
            var file = await GetFileAsync(fileId);
            if (file == null || !file.Chunks.Any())
            {
                return new List<DocumentChunk>();
            }

            // Simple cosine similarity calculation (in a real scenario, you might want to use a vector database)
            var rankedChunks = file.Chunks
                .Where(chunk => chunk.Embedding.Length > 0)
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Similarity = CalculateCosineSimilarity(queryEmbedding, chunk.Embedding)
                })
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Select(x => x.Chunk)
                .ToList();

            return rankedChunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching similar chunks for file: {FileId}", fileId);
            return new List<DocumentChunk>();
        }
    }

    private static double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            return 0;

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA == 0 || normB == 0)
            return 0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
