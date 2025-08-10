# ?? File Processing Test Examples

## Quick Testing Guide

You can test the enhanced file processing capabilities using the demo interface at `https://localhost:7096/demo.html` or by using the API directly.

### Sample Files for Testing

Create these sample files to test different processing capabilities:

#### 1. **sample.txt** (Plain Text)
```
This is a sample text file for testing.
It contains multiple lines of text.

Features tested:
- Line counting
- UTF-8 encoding
- File size tracking
```

#### 2. **sample.csv** (CSV Data)
```
Name,Age,Department,Salary
John Doe,30,Engineering,75000
Jane Smith,28,Marketing,65000
Bob Johnson,35,Sales,70000
Alice Brown,32,HR,60000
```

#### 3. **sample.json** (JSON Data)
```json
{
  "company": "TechCorp",
  "employees": [
    {
      "name": "John Doe",
      "position": "Senior Developer",
      "skills": ["C#", "JavaScript", "MongoDB"]
    },
    {
      "name": "Jane Smith",
      "position": "Product Manager",
      "skills": ["Strategy", "Analytics", "Leadership"]
    }
  ],
  "founded": 2020,
  "headquarters": "San Francisco"
}
```

#### 4. **sample.md** (Markdown)
```markdown
# Project Documentation

## Overview
This is a sample markdown file to test markdown processing.

### Features
- **Bold text** support
- *Italic text* support
- `Code snippets`
- Lists and tables

### Code Example
```javascript
function hello() {
    console.log("Hello World!");
}
```

## Conclusion
Markdown processing maintains structure and formatting.
```

#### 5. **sample.cs** (C# Code)
```csharp
using System;
using System.Collections.Generic;

namespace SampleNamespace
{
    /// <summary>
    /// Sample class for testing code file processing
    /// </summary>
    public class SampleClass
    {
        private readonly List<string> _items;

        public SampleClass()
        {
            _items = new List<string>();
        }

        public void AddItem(string item)
        {
            if (!string.IsNullOrEmpty(item))
            {
                _items.Add(item);
                Console.WriteLine($"Added: {item}");
            }
        }

        public int Count => _items.Count;
    }
}
```

## API Testing Examples

### Upload and Process Files

```bash
# Test PDF Upload
curl -X POST "https://localhost:7096/api/chat/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@sample.pdf" \
  -F "sessionId=test_session_123"

# Test Excel Upload
curl -X POST "https://localhost:7096/api/chat/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@sample.xlsx" \
  -F "sessionId=test_session_123"

# Test Word Document Upload
curl -X POST "https://localhost:7096/api/chat/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@sample.docx" \
  -F "sessionId=test_session_123"

# Test Code File Upload
curl -X POST "https://localhost:7096/api/chat/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@sample.cs" \
  -F "sessionId=test_session_123"
```

### Query Processed Content

```bash
# Ask about uploaded file content
curl -X POST "https://localhost:7096/api/chat/file-chat" \
  -H "Content-Type: application/json" \
  -d '{
    "fileId": "ABC12345",
    "question": "What is the main purpose of this document?",
    "sessionId": "test_session_123"
  }'

# Ask about code structure
curl -X POST "https://localhost:7096/api/chat/file-chat" \
  -H "Content-Type: application/json" \
  -d '{
    "fileId": "XYZ67890",
    "question": "What classes and methods are defined in this code?",
    "sessionId": "test_session_123"
  }'

# Ask about Excel data
curl -X POST "https://localhost:7096/api/chat/file-chat" \
  -H "Content-Type: application/json" \
  -d '{
    "fileId": "DEF54321",
    "question": "What is the average salary in the spreadsheet?",
    "sessionId": "test_session_123"
  }'
```

## Expected Processing Results

### PDF Processing
```
=== Page 1 ===
[Extracted text from page 1]

=== Page 2 ===
[Extracted text from page 2]

Metadata:
- page_count: 2
- pdf_version: 1.4
- ExtractedBy: PdfTextExtractor (iTextSharp)
```

### Excel Processing
```
=== Worksheet: Sheet1 ===
Headers: Name | Age | Department | Salary
--------------------------------------------------
John Doe | 30 | Engineering | 75000
Jane Smith | 28 | Marketing | 65000
Bob Johnson | 35 | Sales | 70000

Metadata:
- worksheet_count: 1
- worksheet_Sheet1_rows: 4
- worksheet_Sheet1_columns: 4
- ExtractedBy: ExcelPackageProcessor (EPPlus)
```

### Word Document Processing
```
Project Overview
This document outlines the project requirements.

Key Features
- Feature 1: User authentication
- Feature 2: Data processing
- Feature 3: Report generation

Metadata:
- paragraph_count: 6
- title: Project Requirements
- author: John Doe
- ExtractedBy: WordDocumentProcessor (OpenXml)
```

### Code File Processing
```
=== sample.cs (.cs) ===
using System;
using System.Collections.Generic;

namespace SampleNamespace
{
    public class SampleClass
    {
        // ... code content ...
    }
}

Metadata:
- file_extension: .cs
- line_count: 25
- language: C#
- ExtractedBy: CodeFileReader (.cs)
```

## Performance Testing

### File Size Recommendations
| File Type | Recommended Max Size | Processing Time |
|-----------|---------------------|-----------------|
| **Text files** | 5MB | < 1 second |
| **PDF files** | 10MB | 2-10 seconds |
| **Word docs** | 5MB | 1-5 seconds |
| **Excel files** | 2MB | 1-3 seconds |
| **Code files** | 1MB | < 1 second |

### Batch Testing Script

Create this PowerShell script for batch testing:

```powershell
# batch-test.ps1
$baseUrl = "https://localhost:7096/api/chat"
$sessionId = "batch_test_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

$testFiles = @(
    "sample.txt",
    "sample.pdf", 
    "sample.docx",
    "sample.xlsx",
    "sample.cs",
    "sample.json"
)

foreach ($file in $testFiles) {
    Write-Host "Testing file: $file"
    
    # Upload file
    $uploadResult = Invoke-RestMethod -Uri "$baseUrl/upload" -Method Post -Form @{
        file = Get-Item $file
        sessionId = $sessionId
    }
    
    if ($uploadResult.success) {
        Write-Host "? Upload successful - File ID: $($uploadResult.data.fileId)"
        
        # Test file chat
        $chatResult = Invoke-RestMethod -Uri "$baseUrl/file-chat" -Method Post -ContentType "application/json" -Body (@{
            fileId = $uploadResult.data.fileId
            question = "What is the main content of this file?"
            sessionId = $sessionId
        } | ConvertTo-Json)
        
        Write-Host "?? Chat response: $($chatResult.data.response)"
    } else {
        Write-Host "? Upload failed: $($uploadResult.message)"
    }
    
    Write-Host "---"
}
```

## Troubleshooting

### Common Issues

1. **File Type Not Supported**
   - Check the file extension against supported types
   - Verify MIME type is correct

2. **Large File Processing Timeout**
   - Increase timeout in configuration
   - Consider file size limits

3. **PDF Extraction Issues**
   - Some PDFs may have security restrictions
   - Scanned PDFs won't extract text (would need OCR)

4. **Excel Memory Issues**
   - Large spreadsheets are limited to 100 rows per sheet
   - Consider splitting large files

### Debug Logging

Enable detailed logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "LocalChatApi.Services.MongoDbFileStorageService": "Debug"
    }
  }
}
```

This will show detailed information about file processing steps and any issues encountered.

## Success Indicators

? **File Upload**: Returns 200 with file ID  
? **Content Extraction**: File content visible in processing logs  
? **Embedding Generation**: Chunks created and stored  
? **RAG Queries**: Relevant responses based on file content  
? **Multiple File Types**: All supported formats process correctly  

The enhanced file processing system is now ready for production use with comprehensive support for document analysis and AI-powered content extraction!