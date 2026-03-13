#!/bin/bash

# Port to clean
PORT=5050

echo "🔍 Cleaning up port $PORT..."
PID=$(lsof -ti:$PORT)
if [ -z "$PID" ]; then
    echo "✅ Port $PORT is already free."
else
    echo "💀 Killing process(es) $PID on port $PORT..."
    echo "$PID" | xargs kill -9
fi

echo "🏗️ Building the application..."
dotnet build second-brain-ia.sln

if [ $? -ne 0 ]; then
    echo "❌ Build failed. Stopping."
    exit 1
fi

echo "🧪 Running unit tests..."
dotnet test tests/Unit/Unit.csproj

if [ $? -ne 0 ]; then
    echo "❌ Unit tests failed. Stopping."
    exit 1
fi

echo "🚀 Launching the API on port $PORT..."
dotnet run --project api/Api/Api.csproj
