# 🚀 LocalChat API - Complete Testing Setup

## Quick Start Testing

### Prerequisites
1. **Ollama** running with a model (e.g., `ollama pull llama2`)
2. **MongoDB** running (local or cloud)
3. **LocalChatApi** built successfully

### 🎯 Three Ways to Test

## Method 1: Interactive Demo (Recommended)
```bash
# Start the API
dotnet run

# Browser will automatically open to demo.html
# OR manually go to: https://localhost:7096/demo.html
```

The demo page provides:
- ✅ **Step 1**: Intent Detection Testing (chat, upload, file_chat)
- ✅ **Step 2**: File Upload & Processing Pipeline  
- ✅ **Step 3**: RAG File Chat with Vector Search
- ✅ Real-time response display
- ✅ Session management
- ✅ Example queries for each workflow

## Method 2: PowerShell Script
```powershell
# Run automated tests
.\test-script.ps1
```

## Method 3: Cross-Platform cURL
```bash
# Make executable and run
chmod +x test-script.sh
./test-script.sh
```

---

## 🔄 Complete Workflow Testing

### Step 1: Intent Detection
**Tests the AI's ability to understand user intent and route appropriately**

**Try these inputs:**
- `"Hello! How are you today?"` → Should detect `chat` intent
- `"I want to upload a document"` → Should detect `file_upload` intent  
- `"Tell me about file ID abc123"` → Should detect `file_chat` intent

### Step 2: File Upload & Processing
**Tests the complete file processing pipeline**

1. **Upload a file** (.txt, .pdf, .docx, .md)
2. **File Reader Agent** → Parses content
3. **Data Extraction Agent** → Cleans and validates
4. **Chunking & Embedding Agent** → Creates vector embeddings
5. **MongoDB Storage** → Saves chunks with embeddings
6. **Returns File ID** → For later RAG queries

### Step 3: RAG File Chat
**Tests Retrieval Augmented Generation with uploaded files**

1. **Use File ID** from Step 2
2. **Ask questions** about the file content
3. **Vector Search** → Finds relevant chunks
4. **LLM Generation** → Creates contextual answers
5. **Chat History** → Maintains conversation context

---

## 🧪 Testing Scenarios

### Scenario A: Complete User Journey
1. Start conversation: `"Hello, I need help analyzing a document"`
2. Upload a file: Use demo.html file upload
3. Get File ID: Note the returned ID (e.g., `60f7b1c8e4b0c8d4f8e9a2b1`)
4. Ask questions: `"What are the main topics in this document?"`
5. Follow-up: `"Can you summarize the key points?"`

### Scenario B: Intent Detection Accuracy
Test different phrasings:
- Upload: `"upload file"`, `"attach document"`, `"send file"`
- File Chat: `"file ID 123"`, `"about the uploaded file"`, `"question about document"`
- Regular Chat: `"how are you"`, `"what's the weather"`, `"tell me a joke"`

### Scenario C: RAG Quality Test
1. Upload a document with specific information
2. Ask targeted questions:
   - `"What is mentioned about [specific topic]?"`
   - `"List the key points discussed"`
   - `"What recommendations are made?"`
3. Verify answers are relevant and accurate

---

## 🔍 Monitoring & Debugging

### Check API Logs
The console output shows detailed workflow execution:
```
[INFO] Intent detected: chat with confidence 0.85
[INFO] Routing to chat workflow
[INFO] File processing completed: 3 chunks created
[INFO] RAG query: Found 2 relevant chunks
```

### MongoDB Verification
```javascript
// Check collections
use LocalChatDb
db.chatMessages.find().limit(5)
db.files.find().limit(5)  
db.chatSessions.find().limit(5)

// Check file processing
db.files.findOne({}, {chunks: {$slice: 1}})
```

### Test Endpoints Directly
```bash
# Health check
curl https://localhost:7096/api/chat/sessions/test_user

# Swagger UI
https://localhost:7096/scalar/v1
```

---

## ✅ Success Indicators

### Intent Detection Working ✅
- Different input types route to correct workflows
- Intent confidence scores are reasonable (>0.6)
- Fallback to 'chat' intent for unclear inputs

### File Processing Working ✅  
- Files upload successfully
- Unique File IDs generated
- Vector embeddings created
- MongoDB storage confirmed

### RAG Working ✅
- Relevant answers to file-specific questions
- Context preserved across conversation
- Handles "file not found" gracefully

### System Integration Working ✅
- All agents communicate properly
- ElsaWorkflow orchestration functional
- SemanticKernel.Ollama responses generated
- MongoDB persistence operational

---

## 🎉 Expected Results

When everything is working correctly:

1. **Demo Page**: All three steps complete successfully
2. **PowerShell Script**: Shows ✅ for most tests (except file upload)
3. **cURL Script**: Complete workflow including file upload
4. **MongoDB**: Collections populated with data
5. **Logs**: Show successful agent execution and decision routing

The system demonstrates a fully functional **intent-driven AI workflow** with **file processing**, **vector search**, and **contextual chat** capabilities!
