# 🧪 LocalChat API Testing Guide

## Overview
This guide provides comprehensive testing instructions for the **Intent-Driven Workflow** implemented in LocalChat API using **SemanticKernel.Ollama**, **ElsaWorkflowAgent**, and **MongoDB**.

## Prerequisites

### 1. Environment Setup
- ✅ **MongoDB** running (local or cloud)
- ✅ **Ollama** running with a model (e.g., `llama2`, `mistral`, `codellama`)
- ✅ **LocalChatApi** built and running
- ✅ **Dependencies** installed (SemanticKernel.Ollama, ElsaWorkflowAgent)

### 2. Start Required Services

```powershell
# 1. Start MongoDB (if local)
mongod --dbpath "C:\data\db"

# 2. Start Ollama
ollama serve

# 3. Pull a model (if not already done)
ollama pull llama2

# 4. Start the API
cd "C:\Users\abhis\OneDrive\Documents\PersnolAI\learn\net\LocalChat\LocalChatApi"
dotnet run
```

### 3. Configuration Check
Verify `appsettings.json` has correct settings:
```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "LocalChatDb"
  },
  "OllamaSettings": {
    "BaseUrl": "http://localhost:11434",
    "ModelName": "llama2"
  }
}
```

## Testing Methods

### Method 1: Demo HTML Page (Recommended)
1. Open `demo.html` in your browser
2. Update the API URL if needed (default: `https://localhost:7096`)
3. Follow the 3-step workflow testing

### Method 2: PowerShell/curl Commands
### Method 3: Postman Collection
### Method 4: Swagger UI

---

## 🚀 Step-by-Step Testing Workflows

### Step 1: Intent Detection & Chat Agent

#### Test Case 1.1: Normal Chat
**Purpose:** Verify basic chat functionality and intent detection

**PowerShell:**
```powershell
$session = "test_session_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
$headers = @{ "Content-Type" = "application/json" }

# Normal chat message
$body = @{
    input = "Hello! How are you today?"
    sessionId = $session
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/message" -Method POST -Headers $headers -Body $body
$response | ConvertTo-Json -Depth 10
```

**Expected Response:**
```json
{
  "success": true,
  "message": "Message processed successfully",
  "data": {
    "response": "Hello! I'm doing well, thank you for asking...",
    "intent": "chat",
    "sessionId": "test_session_20250811_143022",
    "timestamp": "2025-08-11T14:30:22.123Z",
    "metadata": {
      "intent": "chat",
      "confidence": 0.85
    }
  }
}
```

#### Test Case 1.2: Upload Intent Detection
**Purpose:** Verify intent detection routes to upload workflow

**PowerShell:**
```powershell
$body = @{
    input = "I want to upload a document for analysis"
    sessionId = $session
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/message" -Method POST -Headers $headers -Body $body
$response | ConvertTo-Json -Depth 10
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "intent": "file_upload",
    "response": "I understand you want to upload a file. Please use the upload endpoint...",
    "metadata": {
      "intent": "file_upload",
      "confidence": 0.75
    }
  }
}
```

#### Test Case 1.3: File Chat Intent Detection
**Purpose:** Verify intent detection for RAG queries

**PowerShell:**
```powershell
$body = @{
    input = "Can you tell me about file ID abc123?"
    sessionId = $session
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/message" -Method POST -Headers $headers -Body $body
```

### Step 2: File Upload & Processing Flow

#### Test Case 2.1: Upload Text File
**Purpose:** Test complete file processing pipeline

**PowerShell:**
```powershell
# Create a test file
$testContent = @"
# Sample Document for Testing

This is a test document for the LocalChat API.
It contains various information about artificial intelligence,
machine learning, and natural language processing.

Key points:
- AI can understand and generate human language
- Machine learning models learn from data
- NLP helps computers process text and speech
- RAG (Retrieval Augmented Generation) combines retrieval and generation
"@

$testFile = "C:\temp\test_document.txt"
$testContent | Out-File -FilePath $testFile -Encoding UTF8

# Upload the file
$boundary = [System.Guid]::NewGuid().ToString()
$headers = @{
    "Content-Type" = "multipart/form-data; boundary=$boundary"
}

$bodyLines = @()
$bodyLines += "--$boundary"
$bodyLines += "Content-Disposition: form-data; name=`"sessionId`""
$bodyLines += ""
$bodyLines += $session
$bodyLines += "--$boundary"
$bodyLines += "Content-Disposition: form-data; name=`"description`""
$bodyLines += ""
$bodyLines += "Test document for AI and ML concepts"
$bodyLines += "--$boundary"
$bodyLines += "Content-Disposition: form-data; name=`"file`"; filename=`"test_document.txt`""
$bodyLines += "Content-Type: text/plain"
$bodyLines += ""
$bodyLines += $testContent
$bodyLines += "--$boundary--"

$body = $bodyLines -join "`r`n"

$response = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/upload" -Method POST -Headers $headers -Body $body
$fileId = $response.data.fileId
Write-Host "File ID: $fileId"
$response | ConvertTo-Json -Depth 10
```

**Expected Response:**
```json
{
  "success": true,
  "message": "File uploaded and processed successfully",
  "data": {
    "fileId": "60f7b1c8e4b0c8d4f8e9a2b1",
    "success": true,
    "message": "File processed successfully",
    "chunkCount": 3,
    "metadata": {
      "filename": "test_document.txt",
      "size": 425,
      "processedAt": "2025-08-11T14:30:22.123Z"
    }
  }
}
```

### Step 3: File Chat Flow (RAG)

#### Test Case 3.1: Ask About Uploaded File
**Purpose:** Test RAG functionality with vector similarity search

**PowerShell:**
```powershell
# Use the fileId from Step 2
$body = @{
    fileId = $fileId
    question = "What are the main topics discussed in this document?"
    sessionId = $session
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/file-chat" -Method POST -Headers $headers -Body $body
$response | ConvertTo-Json -Depth 10
```

#### Test Case 3.2: Specific Questions
```powershell
# Test specific information retrieval
$questions = @(
    "What is RAG and how does it work?",
    "List the key points mentioned about AI",
    "What does NLP help computers do?",
    "Summarize the content in 2 sentences"
)

foreach ($question in $questions) {
    Write-Host "`n--- Question: $question ---"
    $body = @{
        fileId = $fileId
        question = $question
        sessionId = $session
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/file-chat" -Method POST -Headers $headers -Body $body
    Write-Host "Answer: $($response.data)"
}
```

#### Test Case 3.3: Invalid File ID
**Purpose:** Test error handling for missing files

```powershell
$body = @{
    fileId = "invalid_file_id_12345"
    question = "What is this document about?"
    sessionId = $session
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/file-chat" -Method POST -Headers $headers -Body $body
} catch {
    Write-Host "Expected Error: $($_.Exception.Message)"
}
```

---

## 📊 Advanced Testing Scenarios

### Scenario 1: Complete User Journey
```powershell
# 1. Create session and chat
$session = "journey_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

# 2. Normal conversation
$chatResponse = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/message" -Method POST -Headers @{"Content-Type"="application/json"} -Body (@{input="Hello, I need help with document analysis"; sessionId=$session} | ConvertTo-Json)

# 3. Upload document
# [Upload file code here]

# 4. Ask multiple questions about the document
# [File chat code here]

# 5. Check chat history
$history = Invoke-RestMethod -Uri "https://localhost:7096/api/chat/history/$session" -Method GET
$history.data | ConvertTo-Json -Depth 5
```

### Scenario 2: Multiple File Handling
Test uploading multiple files and switching between them in conversation.

### Scenario 3: Session Management
Test creating, switching, and managing multiple chat sessions.

### Scenario 4: Large File Processing
Test with larger files (>1MB) to verify chunking and embedding performance.

---

## 🔍 Debugging & Troubleshooting

### Common Issues & Solutions

#### 1. **API Not Responding**
```powershell
# Check if API is running
Test-NetConnection -ComputerName localhost -Port 7096
```

#### 2. **MongoDB Connection Issues**
```powershell
# Test MongoDB connection
mongo "mongodb://localhost:27017/LocalChatDb" --eval "db.runCommand({ping: 1})"
```

#### 3. **Ollama Model Issues**
```powershell
# Check available models
ollama list

# Test model directly
ollama run llama2 "Hello, how are you?"
```

#### 4. **Check Application Logs**
Monitor the console output of `dotnet run` for detailed error messages and workflow execution logs.

### Performance Monitoring

#### Monitor Key Metrics:
- **Response Times:** Intent detection, file processing, RAG queries
- **Memory Usage:** During file chunking and embedding
- **Database Operations:** MongoDB read/write performance
- **Model Performance:** Ollama response times

#### Useful Queries:
```javascript
// MongoDB - Check uploaded files
db.files.find().limit(5)

// MongoDB - Check chat messages
db.chatMessages.find({sessionId: "your_session_id"}).sort({timestamp: -1})

// MongoDB - Check embeddings
db.files.findOne({}, {chunks: {$slice: 1}})
```

---

## 📈 Expected Performance Benchmarks

| Operation | Expected Time | Notes |
|-----------|---------------|-------|
| Intent Detection | < 2 seconds | Depends on model size |
| File Upload (< 1MB) | < 5 seconds | Includes chunking & embedding |
| RAG Query | < 3 seconds | Includes similarity search |
| Chat History Retrieval | < 500ms | MongoDB query |

---

## ✅ Testing Checklist

### Basic Functionality
- [ ] API starts without errors
- [ ] MongoDB connection established
- [ ] Ollama model responding
- [ ] Intent detection working for all 3 types
- [ ] File upload and processing complete
- [ ] RAG queries returning relevant answers
- [ ] Chat history persistence working
- [ ] Error handling for invalid inputs
- [ ] Session management working

### Advanced Features
- [ ] Multiple file handling
- [ ] Large file processing (>1MB)
- [ ] Concurrent user sessions
- [ ] Vector similarity search accuracy
- [ ] Context preservation across conversation
- [ ] File processing background tasks

### Error Scenarios
- [ ] Invalid file formats handled gracefully
- [ ] Network connectivity issues handled
- [ ] Database unavailable scenarios
- [ ] Ollama model unavailable scenarios
- [ ] Malformed requests handled properly

---

## 🎯 Success Criteria

**Intent Detection (Step 1):**
- ✅ Correctly identifies chat, upload, and file_chat intents
- ✅ Routes to appropriate workflow
- ✅ Maintains conversation context

**File Processing (Step 2):**
- ✅ Successfully uploads and processes files
- ✅ Creates vector embeddings
- ✅ Stores in MongoDB with proper indexing
- ✅ Returns valid File ID

**RAG Queries (Step 3):**
- ✅ Retrieves relevant chunks based on similarity
- ✅ Generates contextual answers
- ✅ Maintains conversation history
- ✅ Handles file not found gracefully

The system successfully demonstrates a complete **intent-driven workflow** with **AI orchestration**, **file processing**, and **RAG capabilities** using modern .NET technologies!
