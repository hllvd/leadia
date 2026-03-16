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

echo "🏗️ Starting the API..."
# dotnet run handles incremental build. 
# We disable shared compilation to bypass a known VBCSCompiler hang on this system.
dotnet run --project api/Api/Api.csproj /p:UseSharedCompilation=false "$@"
