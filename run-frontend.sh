#!/bin/bash

# Port to clean
PORT=3000

echo "🔍 Cleaning up frontend ports (3000-3005)..."
for port in {3000..3005}
do
    PID=$(lsof -ti:$port)
    if [ ! -z "$PID" ]; then
        echo "💀 Killing process $PID on port $port..."
        kill -9 $PID
    fi
done

echo "🚀 Launching the Frontend on port $PORT..."
cd client
npm run dev
