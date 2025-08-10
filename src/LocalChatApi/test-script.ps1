# LocalChat API - Quick Test Script
# Run this script to test all three workflows

Write-Host "🚀 LocalChat API Testing Script" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan

# Configuration
$apiUrl = "https://localhost:7096/api/chat"
$session = "test_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

Write-Host "Session ID: $session" -ForegroundColor Yellow

# Common headers
$headers = @{ "Content-Type" = "application/json" }

Write-Host "`n🔍 Step 1: Testing Intent Detection..." -ForegroundColor Green

# Test 1: Normal Chat
Write-Host "Testing normal chat..."
try {
    $body = @{
        input = "Hello! How are you today?"
        sessionId = $session
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$apiUrl/message" -Method POST -Headers $headers -Body $body
    Write-Host "✅ Chat Response: $($response.data.response.Substring(0, [Math]::Min(100, $response.data.response.Length)))..." -ForegroundColor Green
    Write-Host "✅ Detected Intent: $($response.data.intent)" -ForegroundColor Green
} catch {
    Write-Host "❌ Chat test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: Upload Intent
Write-Host "`nTesting upload intent detection..."
try {
    $body = @{
        input = "I want to upload a document for analysis"
        sessionId = $session
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$apiUrl/message" -Method POST -Headers $headers -Body $body
    Write-Host "✅ Upload Intent Response: $($response.data.response.Substring(0, [Math]::Min(100, $response.data.response.Length)))..." -ForegroundColor Green
    Write-Host "✅ Detected Intent: $($response.data.intent)" -ForegroundColor Green
} catch {
    Write-Host "❌ Upload intent test failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n📁 Step 2: Testing File Upload..." -ForegroundColor Blue

# Create test file
$testContent = @"
# AI and Machine Learning Guide

This document covers important concepts in artificial intelligence and machine learning.

## Key Topics:
- Natural Language Processing (NLP)
- Retrieval Augmented Generation (RAG)
- Vector Embeddings
- Semantic Search

## Summary:
AI systems can understand and generate human language using advanced models.
RAG combines retrieval and generation for more accurate responses.
Vector embeddings enable semantic similarity search.
"@

$testFile = "test_document.txt"
$testContent | Out-File -FilePath $testFile -Encoding UTF8

Write-Host "Created test file: $testFile"

# Note: File upload via PowerShell is complex with multipart/form-data
# This is better tested via the demo.html page or Postman
Write-Host "💡 For file upload testing, please use:" -ForegroundColor Yellow
Write-Host "   - demo.html page (recommended)" -ForegroundColor Yellow
Write-Host "   - Postman with multipart/form-data" -ForegroundColor Yellow
Write-Host "   - curl command with -F parameter" -ForegroundColor Yellow

Write-Host "`n🧠 Step 3: Testing File Chat (will fail without file upload)..." -ForegroundColor Magenta

# Test with dummy file ID (will fail gracefully)
Write-Host "Testing with dummy file ID (expected to fail)..."
try {
    $body = @{
        fileId = "dummy_file_id_12345"
        question = "What is this document about?"
        sessionId = $session
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$apiUrl/file-chat" -Method POST -Headers $headers -Body $body
    Write-Host "✅ File Chat Response: $($response.data)" -ForegroundColor Green
} catch {
    Write-Host "❌ Expected: File not found error: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`n📊 Testing Chat History..." -ForegroundColor Cyan
try {
    $history = Invoke-RestMethod -Uri "$apiUrl/history/$session" -Method GET
    Write-Host "✅ Retrieved $($history.data.Count) messages from session history" -ForegroundColor Green
} catch {
    Write-Host "❌ History test failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n🎯 Testing Complete!" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor White
Write-Host "1. Open demo.html in your browser for full testing" -ForegroundColor White
Write-Host "2. Test file upload and RAG functionality" -ForegroundColor White
Write-Host "3. Try different file types (.txt, .pdf, .docx)" -ForegroundColor White
Write-Host "4. Test with real Ollama model responses" -ForegroundColor White

# Cleanup
Remove-Item $testFile -ErrorAction SilentlyContinue
