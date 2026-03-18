#!/bin/bash

# Port to clean
PORT=5050

CLEAN_SCRIPT="scripts/clean-ports.sh"
if [ -f "$CLEAN_SCRIPT" ]; then
    echo "🔍 Cleaning up port $PORT..."
    bash "$CLEAN_SCRIPT" "$PORT"
fi

echo "🏗️ Starting the API..."
# dotnet run handles incremental build. 
# We disable shared compilation to bypass a known VBCSCompiler hang on this system.
dotnet run --project api/Api/Api.csproj /p:UseSharedCompilation=false "$@"
