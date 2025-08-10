using ElsaWorkflowAgent.Agents;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using LocalChatApi.Models;
using LocalChatApi.Services;

namespace LocalChatApi.Agents;

/// <summary>
/// File upload agent that processes uploaded files and generates File IDs
/// </summary>
public class FileUploadAgent : BaseAgent<FileProcessingResult>
{
    private readonly IFileStorageService _fileStorageService;

    public FileUploadAgent(ILogger<FileUploadAgent> logger, Kernel kernel, IFileStorageService fileStorageService)
        : base(logger, kernel)
    {
        _fileStorageService = fileStorageService;
    }

    public override string Id => "file-upload-agent";
    public override string Name => "File Upload Agent";
    public override string Description => "Processes file uploads and stores file metadata";

    public override AgentMetadata Metadata => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        InputType = typeof(FileUploadRequest),
        OutputType = typeof(FileProcessingResult)
    };

    protected override async Task<FileProcessingResult> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
    {
        var fileRequest = input as FileUploadRequest ?? throw new ArgumentException("Input must be FileUploadRequest");
        
        Logger.LogInformation("Processing file upload: {FileName} ({FileSize} bytes)", fileRequest.File.FileName, fileRequest.File.Length);

        try
        {
            // Generate unique file ID
            var fileId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            // Save the physical file to disk
            var filePath = await _fileStorageService.SavePhysicalFileAsync(fileRequest.File, fileId);

            // Create file document with metadata only (no content stored in DB)
            var fileDoc = new FileDocument
            {
                FileId = fileId,
                SessionId = fileRequest.SessionId,
                FileName = fileRequest.File.FileName,
                ContentType = fileRequest.File.ContentType,
                FilePath = filePath,
                FileSize = fileRequest.File.Length,
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["file_size"] = fileRequest.File.Length,
                    ["uploaded_at"] = DateTime.UtcNow,
                    ["original_filename"] = fileRequest.File.FileName
                }
            };

            // Store file metadata in MongoDB
            await _fileStorageService.SaveFileAsync(fileDoc);

            Logger.LogInformation("File uploaded successfully with ID: {FileId}, saved to: {FilePath}", fileId, filePath);

            return new FileProcessingResult
            {
                FileId = fileId,
                Success = true,
                Message = $"File '{fileRequest.File.FileName}' uploaded successfully",
                ChunkCount = 0, // Will be updated by chunking agent
                Metadata = new Dictionary<string, object>
                {
                    ["file_name"] = fileRequest.File.FileName,
                    ["file_size"] = fileRequest.File.Length,
                    ["content_type"] = fileRequest.File.ContentType,
                    ["file_path"] = filePath
                }
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing file upload: {FileName}", fileRequest.File.FileName);
            
            return new FileProcessingResult
            {
                FileId = "",
                Success = false,
                Message = $"Failed to upload file: {ex.Message}",
                ChunkCount = 0,
                Metadata = new Dictionary<string, object>()
            };
        }
    }
}
