#!/bin/bash

# Use Node 20 via nvm
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && . "$NVM_DIR/nvm.sh"
nvm use 20 --silent 2>/dev/null || export PATH="/Users/hudson/.nvm/versions/node/v20.20.1/bin:$PATH"

# Port to clean
PORT=3000

kill -9 $(lsof -t -i:3000)


echo "🚀 Launching the Frontend on port $PORT..."
cd client
npm run dev
