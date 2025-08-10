#!/bin/bash
# LocalChat API - Cross-Platform Test Script (cURL)

echo "🚀 LocalChat API Testing Script (cURL)"
echo "======================================"

# Configuration
API_URL="https://localhost:7096/api/chat"
SESSION="test_$(date +%Y%m%d_%H%M%S)"

echo "Session ID: $SESSION"

# Test 1: Normal Chat
echo ""
echo "🔍 Step 1: Testing Intent Detection..."
echo "Testing normal chat..."

curl -X POST "$API_URL/message" \
  -H "Content-Type: application/json" \
  -d "{
    \"input\": \"Hello! How are you today?\",
    \"sessionId\": \"$SESSION\"
  }" \
  -k -s | jq '.'

# Test 2: Upload Intent
echo ""
echo "Testing upload intent detection..."

curl -X POST "$API_URL/message" \
  -H "Content-Type: application/json" \
  -d "{
    \"input\": \"I want to upload a document for analysis\",
    \"sessionId\": \"$SESSION\"
  }" \
  -k -s | jq '.'

# Test 3: File Chat Intent
echo ""
echo "Testing file chat intent detection..."

curl -X POST "$API_URL/message" \
  -H "Content-Type: application/json" \
  -d "{
    \"input\": \"Can you tell me about file ID abc123?\",
    \"sessionId\": \"$SESSION\"
  }" \
  -k -s | jq '.'

# Test 4: File Upload (create test file first)
echo ""
echo "📁 Step 2: Testing File Upload..."

# Create test file
cat > test_document.txt << EOF
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
EOF

echo "Created test file: test_document.txt"

# Upload file
echo "Uploading file..."
UPLOAD_RESPONSE=$(curl -X POST "$API_URL/upload" \
  -F "file=@test_document.txt" \
  -F "sessionId=$SESSION" \
  -F "description=Test document for AI concepts" \
  -k -s)

echo "$UPLOAD_RESPONSE" | jq '.'

# Extract file ID if upload was successful
FILE_ID=$(echo "$UPLOAD_RESPONSE" | jq -r '.data.fileId // "not_found"')
echo "File ID: $FILE_ID"

# Test 5: File Chat (RAG)
if [ "$FILE_ID" != "not_found" ] && [ "$FILE_ID" != "null" ]; then
    echo ""
    echo "🧠 Step 3: Testing File Chat (RAG)..."
    
    # Test multiple questions
    QUESTIONS=(
        "What are the main topics discussed in this document?"
        "What is RAG and how does it work?"
        "Summarize the key points about AI"
    )
    
    for question in "${QUESTIONS[@]}"; do
        echo ""
        echo "Question: $question"
        curl -X POST "$API_URL/file-chat" \
          -H "Content-Type: application/json" \
          -d "{
            \"fileId\": \"$FILE_ID\",
            \"question\": \"$question\",
            \"sessionId\": \"$SESSION\"
          }" \
          -k -s | jq '.data' | tr -d '"'
    done
else
    echo "❌ File upload failed, skipping RAG tests"
fi

# Test 6: Chat History
echo ""
echo "📊 Testing Chat History..."
curl -X GET "$API_URL/history/$SESSION" \
  -k -s | jq '.data | length' | xargs echo "Messages in session:"

echo ""
echo "🎯 Testing Complete!"
echo "==================="
echo "Next Steps:"
echo "1. Open demo.html in your browser for interactive testing"
echo "2. Check the API logs for detailed workflow execution"
echo "3. Verify MongoDB collections have been populated"

# Cleanup
rm -f test_document.txt
