using Elsa.Workflows.Contracts;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Logging;
using ElsaWorkflowAgent.Execution;
using Elsa.Workflows.Activities;

namespace ElsaWorkflowAgent.Services;

/// <summary>
/// Service for managing Elsa workflows with JSON serialization
/// </summary>
public interface IElsaWorkflowService
{
    /// <summary>
    /// Save a workflow to JSON file
    /// </summary>
    Task<bool> SaveWorkflowAsync(string workflowName, Workflow workflow);

    /// <summary>
    /// Load a workflow from JSON file
    /// </summary>
    Task<Workflow?> LoadWorkflowAsync(string workflowName);

    /// <summary>
    /// Execute a workflow by name
    /// </summary>
    Task<WorkflowResult> ExecuteWorkflowAsync(string workflowName, Dictionary<string, object>? input = null);

    /// <summary>
    /// Execute a workflow from JSON
    /// </summary>
    Task<WorkflowResult> ExecuteWorkflowFromJsonAsync(string workflowJson, Dictionary<string, object>? input = null);

    /// <summary>
    /// Get all saved workflow names
    /// </summary>
    Task<List<string>> GetSavedWorkflowsAsync();

    /// <summary>
    /// Delete a saved workflow
    /// </summary>
    Task<bool> DeleteWorkflowAsync(string workflowName);

    /// <summary>
    /// Export a workflow to JSON string
    /// </summary>
    Task<string?> ExportWorkflowAsync(string workflowName);

    /// <summary>
    /// Import a workflow from JSON string
    /// </summary>
    Task<bool> ImportWorkflowAsync(string workflowName, string workflowJson);

    /// <summary>
    /// Create a workflow template
    /// </summary>
    Task<bool> SaveAsTemplateAsync(string templateName, Workflow workflow, string description = "");

    /// <summary>
    /// Load workflow from template
    /// </summary>
    Task<Workflow?> LoadFromTemplateAsync(string templateName, Dictionary<string, object>? parameters = null);

    /// <summary>
    /// Get all workflow templates
    /// </summary>
    Task<List<WorkflowTemplate>> GetTemplatesAsync();
}

/// <summary>
/// File-based Elsa workflow management service
/// </summary>
public class FileElsaWorkflowService : IElsaWorkflowService
{
    private readonly ILogger<FileElsaWorkflowService> _logger;
    private readonly IElsaWorkflowEngine _workflowEngine;
    private readonly string _workflowsDirectory;
    private readonly string _templatesDirectory;

    public FileElsaWorkflowService(
        ILogger<FileElsaWorkflowService> logger,
        IElsaWorkflowEngine workflowEngine,
        string workflowsDirectory = "SavedWorkflows",
        string templatesDirectory = "WorkflowTemplates")
    {
        _logger = logger;
        _workflowEngine = workflowEngine;
        _workflowsDirectory = workflowsDirectory;
        _templatesDirectory = templatesDirectory;

        // Ensure directories exist
        Directory.CreateDirectory(_workflowsDirectory);
        Directory.CreateDirectory(_templatesDirectory);
    }

    public async Task<bool> SaveWorkflowAsync(string workflowName, Workflow workflow)
    {
        try
        {
            var fileName = $"{SanitizeFileName(workflowName)}.json";
            var filePath = Path.Combine(_workflowsDirectory, fileName);
            
            var workflowJson = await _workflowEngine.SaveWorkflowToJsonAsync(workflow);
            await File.WriteAllTextAsync(filePath, workflowJson);
            
            _logger.LogInformation("Saved workflow: {WorkflowName} to {FilePath}", workflowName, filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving workflow: {WorkflowName}", workflowName);
            return false;
        }
    }

    public async Task<Workflow?> LoadWorkflowAsync(string workflowName)
    {
        try
        {
            var fileName = $"{SanitizeFileName(workflowName)}.json";
            var filePath = Path.Combine(_workflowsDirectory, fileName);
            
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Workflow file not found: {FilePath}", filePath);
                return null;
            }

            var workflowJson = await File.ReadAllTextAsync(filePath);
            var workflow = await _workflowEngine.LoadWorkflowFromJsonAsync(workflowJson);
            
            _logger.LogInformation("Loaded workflow: {WorkflowName} from {FilePath}", workflowName, filePath);
            return workflow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workflow: {WorkflowName}", workflowName);
            return null;
        }
    }

    public async Task<WorkflowResult> ExecuteWorkflowAsync(string workflowName, Dictionary<string, object>? input = null)
    {
        var workflow = await LoadWorkflowAsync(workflowName);
        if (workflow == null)
        {
            throw new InvalidOperationException($"Workflow not found: {workflowName}");
        }

        return await _workflowEngine.ExecuteElsaWorkflowAsync(workflow, input);
    }

    public async Task<WorkflowResult> ExecuteWorkflowFromJsonAsync(string workflowJson, Dictionary<string, object>? input = null)
    {
        return await _workflowEngine.ExecuteFromJsonAsync(workflowJson, input);
    }

    public async Task<List<string>> GetSavedWorkflowsAsync()
    {
        try
        {
            if (!Directory.Exists(_workflowsDirectory))
            {
                return new List<string>();
            }

            var files = Directory.GetFiles(_workflowsDirectory, "*.json");
            return files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting saved workflows");
            return new List<string>();
        }
    }

    public async Task<bool> DeleteWorkflowAsync(string workflowName)
    {
        try
        {
            var fileName = $"{SanitizeFileName(workflowName)}.json";
            var filePath = Path.Combine(_workflowsDirectory, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted workflow: {WorkflowName}", workflowName);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow: {WorkflowName}", workflowName);
            return false;
        }
    }

    public async Task<string?> ExportWorkflowAsync(string workflowName)
    {
        try
        {
            var workflow = await LoadWorkflowAsync(workflowName);
            if (workflow == null)
            {
                return null;
            }

            return await _workflowEngine.SaveWorkflowToJsonAsync(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting workflow: {WorkflowName}", workflowName);
            return null;
        }
    }

    public async Task<bool> ImportWorkflowAsync(string workflowName, string workflowJson)
    {
        try
        {
            var workflow = await _workflowEngine.LoadWorkflowFromJsonAsync(workflowJson);
            
            // Update the workflow name
            workflow.Identity.Name = workflowName;
            workflow.Identity.Id = Guid.NewGuid().ToString();
            
            return await SaveWorkflowAsync(workflowName, workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing workflow: {WorkflowName}", workflowName);
            return false;
        }
    }

    public async Task<bool> SaveAsTemplateAsync(string templateName, Workflow workflow, string description = "")
    {
        try
        {
            var template = new WorkflowTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = templateName,
                Description = description,
                CreatedAt = DateTime.UtcNow,
                WorkflowJson = await _workflowEngine.SaveWorkflowToJsonAsync(workflow),
                Parameters = ExtractParametersFromWorkflow(workflow)
            };

            var templatePath = Path.Combine(_templatesDirectory, $"{SanitizeFileName(templateName)}.json");
            var templateJson = System.Text.Json.JsonSerializer.Serialize(template, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(templatePath, templateJson);
            
            _logger.LogInformation("Saved workflow template: {TemplateName}", templateName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving workflow template: {TemplateName}", templateName);
            return false;
        }
    }

    public async Task<Workflow?> LoadFromTemplateAsync(string templateName, Dictionary<string, object>? parameters = null)
    {
        try
        {
            var templatePath = Path.Combine(_templatesDirectory, $"{SanitizeFileName(templateName)}.json");
            
            if (!File.Exists(templatePath))
            {
                _logger.LogWarning("Template not found: {TemplateName}", templateName);
                return null;
            }

            var templateJson = await File.ReadAllTextAsync(templatePath);
            var template = System.Text.Json.JsonSerializer.Deserialize<WorkflowTemplate>(templateJson);
            
            if (template?.WorkflowJson == null)
            {
                return null;
            }

            var workflow = await _workflowEngine.LoadWorkflowFromJsonAsync(template.WorkflowJson);
            
            // Apply parameters if provided
            if (parameters != null)
            {
                ApplyParametersToWorkflow(workflow, parameters);
            }

            // Generate new ID for the workflow instance
            workflow.Identity.Id = Guid.NewGuid().ToString();
            
            return workflow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workflow from template: {TemplateName}", templateName);
            return null;
        }
    }

    public async Task<List<WorkflowTemplate>> GetTemplatesAsync()
    {
        var templates = new List<WorkflowTemplate>();
        
        try
        {
            if (!Directory.Exists(_templatesDirectory))
            {
                return templates;
            }

            var templateFiles = Directory.GetFiles(_templatesDirectory, "*.json");
            
            foreach (var file in templateFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var template = System.Text.Json.JsonSerializer.Deserialize<WorkflowTemplate>(json);
                    
                    if (template != null)
                    {
                        templates.Add(template);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load template from file: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow templates");
        }
        
        return templates;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }

    private Dictionary<string, object> ExtractParametersFromWorkflow(Workflow workflow)
    {
        // Extract parameters that can be customized in templates
        var parameters = new Dictionary<string, object>();
        
        // Add workflow variables as parameters
        foreach (var variable in workflow.Variables)
        {
            parameters[variable.Name] = variable.Value ?? "";
        }
        
        return parameters;
    }

    private void ApplyParametersToWorkflow(Workflow workflow, Dictionary<string, object> parameters)
    {
        // Apply parameters to workflow variables
        foreach (var parameter in parameters)
        {
            var variable = workflow.Variables.FirstOrDefault(v => v.Name == parameter.Key);
            if (variable != null)
            {
                variable.Value = parameter.Value;
            }
        }
    }
}

/// <summary>
/// Workflow template model
/// </summary>
public class WorkflowTemplate
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "System";
    public string WorkflowJson { get; set; } = "";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}