# LocalChatApi - Intent-Driven Workflow Processing

A modular C# ASP.NET Core Web API that processes user input through an intent-driven workflow using Semantic Kernel, Ollama integration, and Elsa Workflow orchestration with MongoDB for chat history and memory management.

## 🏗️ Architecture Overview

```
[User Input] → [Intent Recognition Agent] → [Workflow Orchestrator]
    ↓
Step 1: Intent Detection
    • Normal query → Chat Agent (SemanticKernel.Ollama)
    • "upload file" → File Upload Flow
    • "question about file" → File Chat Flow (RAG)

Step 2: File Upload Flow
    • File Upload Agent → Store file + generate File ID
    • File Reader Agent → Parse content
    • Data Extraction Agent → Clean/validate
    • Chunking & Embedding Agent → Vector embeddings
    • Store in MongoDB → Return File ID

Step 3: File Chat Flow (RAG)
    • Check file availability in MongoDB
    • Retrieve relevant chunks via similarity search
    • File Chat Agent → RAG with SemanticKernel.Ollama
    • Return contextual response
```

## 🚀 Features

- **Intent-Driven Workflow**: Automatically detects user intent and routes to appropriate workflow
- **MongoDB Integration**: Persistent chat history, file storage, and vector embeddings
- **RAG Implementation**: Question-answering over uploaded files using vector similarity
- **Elsa Workflow Orchestration**: Complex workflow management with agent coordination
- **Semantic Kernel Integration**: LLM interactions via Ollama
- **Clean Architecture**: Separated concerns with agents, services, and orchestration layers

## 📋 Prerequisites

### Required Services
1. **Ollama**: AI model serving
   ```bash
   # Install Ollama: https://ollama.ai/
   ollama pull llama3.1:latest
   ollama pull mxbai-embed-large:latest
   ollama serve
   ```

2. **MongoDB**: Database for chat history and file storage
   ```bash
   # Using Docker
   docker run -d -p 27017:27017 --name mongodb mongo:latest
   
   # Or install MongoDB locally
   # https://www.mongodb.com/docs/manual/installation/
   ```

### Dependencies
- .NET 9.0
- ASP.NET Core
- Semantic Kernel
- Ollama integration
- MongoDB Driver
- Elsa Workflow

## ⚙️ Configuration

### appsettings.json
```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "LocalChatDb",
    "ChatMessagesCollection": "ChatMessages",
    "ChatSessionsCollection": "ChatSessions",
    "FileDocumentsCollection": "FileDocuments"
  },
  "OllamaSettings": {
    "Endpoint": "http://localhost:11434",
    "ChatModel": "llama3.1:latest",
    "EmbeddingModel": "mxbai-embed-large:latest"
  }
}
```

## 🛠️ API Endpoints

### Chat Operations
- `POST /api/chat/message` - Send message (intent detection + routing)
- `POST /api/chat/upload` - Upload file for processing
- `POST /api/chat/file-chat` - Ask questions about uploaded files
- `GET /api/chat/history/{sessionId}` - Get chat history
- `GET /api/chat/sessions/{userId}` - Get user sessions
- `POST /api/chat/sessions` - Create new session

### Example Usage

#### 1. Normal Chat
```bash
curl -X POST "https://localhost:5001/api/chat/message" \
  -H "Content-Type: application/json" \
  -d '{
    "input": "Hello, how are you?",
    "sessionId": "session-123"
  }'
```

#### 2. File Upload
```bash
curl -X POST "https://localhost:5001/api/chat/upload" \
  -F "file=@document.txt" \
  -F "sessionId=session-123"
```

#### 3. File Chat (RAG)
```bash
curl -X POST "https://localhost:5001/api/chat/file-chat" \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What are the main points in this document?",
    "fileId": "ABC12345",
    "sessionId": "session-123"
  }'
```

## 🔧 Component Details

### Agents
- **IntentDetectionAgent**: Analyzes user input to determine workflow route
- **ChatAgent**: Handles normal conversation with chat history context
- **FileUploadAgent**: Processes file uploads and generates unique File IDs
- **FileReaderAgent**: Parses and cleans file content using LLM
- **DataExtractionAgent**: Validates and structures cleaned content
- **ChunkingEmbeddingAgent**: Creates text chunks and vector embeddings
- **FileChatAgent**: Handles RAG-based queries over uploaded files

### Decisions
- **IntentRoutingDecision**: Routes requests based on detected intent
- **FileAvailabilityDecision**: Checks file availability for chat operations

### Services
- **WorkflowOrchestrationService**: Main orchestrator coordinating all workflows
- **ChatHistoryService**: MongoDB-based chat history management
- **FileStorageService**: MongoDB-based file and embedding storage

### Workflows
1. **Intent Detection Pipeline**: User input → Intent detection → Routing
2. **File Upload Pipeline**: Upload → Parse → Clean → Chunk → Embed → Store
3. **File Chat Pipeline**: Availability check → Similarity search → RAG response

## 🗄️ MongoDB Collections

### ChatMessages
```json
{
  "_id": "ObjectId",
  "sessionId": "string",
  "role": "user|assistant",
  "content": "string",
  "intent": "string",
  "timestamp": "DateTime",
  "metadata": {}
}
```

### ChatSessions
```json
{
  "_id": "ObjectId",
  "sessionId": "string",
  "userId": "string",
  "title": "string",
  "createdAt": "DateTime",
  "lastMessageAt": "DateTime",
  "isActive": "boolean",
  "metadata": {}
}
```

### FileDocuments
```json
{
  "_id": "ObjectId",
  "fileId": "string",
  "sessionId": "string",
  "fileName": "string",
  "contentType": "string",
  "originalContent": "string",
  "cleanedContent": "string",
  "chunks": [
    {
      "id": "string",
      "content": "string",
      "embedding": [float],
      "chunkIndex": "int",
      "metadata": {}
    }
  ],
  "createdAt": "DateTime",
  "metadata": {}
}
```

## 🚀 Running the Application

1. **Start Prerequisites**:
   ```bash
   # Start Ollama
   ollama serve
   
   # Start MongoDB (if using Docker)
   docker start mongodb
   ```

2. **Run the API**:
   ```bash
   cd LocalChatApi
   dotnet run
   ```

3. **Access the API**:
   - API: `https://localhost:5001`
   - OpenAPI/Swagger: `https://localhost:5001/swagger`

## 🔄 Workflow Examples

### Intent Detection Flow
```
User: "I want to upload a document"
↓
IntentDetectionAgent → Intent: "file_upload"
↓
IntentRoutingDecision → Route: "file_upload_workflow"
↓
Response: "Please use the file upload endpoint to upload your file."
```

### File Upload Flow
```
File Upload Request
↓
FileUploadAgent → Store file, generate File ID
↓
Background Processing:
  FileReaderAgent → Clean content
  DataExtractionAgent → Validate
  ChunkingEmbeddingAgent → Create embeddings
↓
Response: "File processed, File ID: ABC12345"
```

### File Chat Flow (RAG)
```
User: "What does file ABC12345 say about pricing?"
↓
IntentDetectionAgent → Intent: "file_chat"
↓
FileAvailabilityDecision → Check file exists
↓
FileChatAgent → 
  Generate question embedding
  Search similar chunks
  Create RAG prompt with context
  Generate response with LLM
↓
Response: "Based on the document, the pricing section mentions..."
```

## 🧪 Testing

### Manual Testing
Use the provided curl examples or test through Swagger UI at `/swagger`.

### Sample Workflow Test
1. Start a chat session
2. Upload a text file
3. Ask questions about the uploaded file
4. Verify responses use file content
5. Check chat history is maintained

## 📝 Notes

- File processing happens asynchronously after upload
- Vector similarity search uses cosine similarity
- Chat history provides context for better responses
- All workflows are orchestrated through Elsa Workflow engine
- MongoDB indexes are automatically created for performance

## 🔧 Troubleshooting

1. **Ollama Connection Issues**: Ensure Ollama is running on `http://localhost:11434`
2. **MongoDB Issues**: Check MongoDB is accessible on `mongodb://localhost:27017`
3. **File Processing**: Large files may take time to process in background
4. **Embedding Errors**: Ensure the embedding model is pulled in Ollama

## 📚 Technology Stack

- **Backend**: ASP.NET Core 9.0, C#
- **AI/ML**: Semantic Kernel, Ollama
- **Workflow**: Elsa Workflow Agent
- **Database**: MongoDB
- **Architecture**: Clean Architecture, Dependency Injection
