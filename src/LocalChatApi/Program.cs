using LocalChatApi.Configuration;
using LocalChatApi.Services;
using LocalChatApi.Agents;
using LocalChatApi.Decisions;
using SemanticKernel.Ollama.Extensions;
using SemanticKernel.Ollama.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using MongoDB.Driver;

#pragma warning disable SKEXP0001 // Suppress experimental API warnings for Ollama integration

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.Configure<OllamaSettings>(
    builder.Configuration.GetSection("OllamaSettings"));

// Register MongoDB
builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var settings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()
        ?? throw new InvalidOperationException("MongoDbSettings not configured");
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var client = serviceProvider.GetRequiredService<IMongoClient>();
    var settings = builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()
        ?? throw new InvalidOperationException("MongoDbSettings not configured");
    return client.GetDatabase(settings.DatabaseName);
});

// Configure Semantic Kernel with Ollama
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
    var ollamaSettings = builder.Configuration.GetSection("OllamaSettings").Get<OllamaSettings>()
        ?? throw new InvalidOperationException("OllamaSettings not configured");

    var ollamaOptions = new OllamaOptions
    {
        Endpoint = ollamaSettings.Endpoint,
        ChatModel = ollamaSettings.ChatModel,
        EmbeddingModel = ollamaSettings.EmbeddingModel,
        TimeoutMinutes = ollamaSettings.TimeoutMinutes
    };

    var kernel = Kernel.CreateBuilder()
        .AddOllamaServices(ollamaOptions)
        .Build();

    return kernel;
});

// Register embedding service separately for injection
builder.Services.AddSingleton<ITextEmbeddingGenerationService>(serviceProvider =>
{
    var kernel = serviceProvider.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<ITextEmbeddingGenerationService>();
});

// Register application services
builder.Services.AddScoped<IChatHistoryService, MongoDbChatHistoryService>();
builder.Services.AddScoped<IFileStorageService, MongoDbFileStorageService>();
builder.Services.AddScoped<ISimpleWorkflowService, SimpleWorkflowService>();
builder.Services.AddScoped<IWorkflowOrchestrationService, WorkflowOrchestrationService>();

// Register agents from FileProcessingAgents.cs
builder.Services.AddScoped<FileUploadAgent>();
builder.Services.AddScoped<FileReaderAgent>();
builder.Services.AddScoped<DataExtractionAgent>();
builder.Services.AddScoped<ChunkingEmbeddingAgent>();
builder.Services.AddScoped<FileChatAgent>();

// Register agents from IntentDetectionAgent.cs
builder.Services.AddScoped<IntentDetectionAgent>();
builder.Services.AddScoped<ChatAgent>();

// Register decisions
builder.Services.AddScoped<IntentRoutingDecision>();
builder.Services.AddScoped<FileAvailabilityDecision>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LocalChatApi v1");
    });
    
    // Enable CORS in development
    app.UseCors("DevelopmentCorsPolicy");
}

// Enable static files to serve demo.html from wwwroot
app.UseStaticFiles();

// Add default file mapping for demo.html
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = { "demo.html" }
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Add a fallback route to serve demo.html for root requests
app.MapFallbackToFile("demo.html");

app.Run();
