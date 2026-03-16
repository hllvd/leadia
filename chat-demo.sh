#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# chat-demo.sh — Simulates a WhatsApp conversation with the AI broker bot.
#
# Usage:
#   1. Start the API:  cd api/Api && dotnet run --local
#   2. Run this script: ./chat-demo.sh
# ─────────────────────────────────────────────────────────────────────────────

BASE_URL="http://localhost:5050"
FROM="customer-demo-$$"   # unique per run so state is fresh each time

GREEN="\033[0;32m"
BLUE="\033[0;34m"
YELLOW="\033[1;33m"
RESET="\033[0m"

say() {
  local role="$1"
  local text="$2"
  if [ "$role" = "customer" ]; then
    echo -e "${BLUE}👤 Customer:${RESET} $text"
  else
    echo -e "${GREEN}🤖 Broker:${RESET}   $text"
  fi
}

chat() {
  local message="$1"
  say "customer" "$message"

  local response
  response=$(curl -s -X POST "$BASE_URL/chat" \
    -H "Content-Type: application/json" \
    -d "{\"from\": \"$FROM\", \"message\": \"$message\"}")

  local reply
  reply=$(echo "$response" | grep -o '"reply":"[^"]*"' | sed 's/"reply":"//;s/"$//')

  if [ -z "$reply" ]; then
    echo -e "${YELLOW}⚠️  No reply received. Is the API running? Response: $response${RESET}"
    exit 1
  fi

  say "broker" "$reply"
  echo ""
  sleep 1   # small pause so timestamps differ (dedup safety)
}

# ─────────────────────────────────────────────────────────────────────────────

echo ""
echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
echo -e "${YELLOW}  AI Broker Demo — Real Estate Lead Qualification${RESET}"
echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
echo ""

chat "Oi! Vi um anúncio de vocês e fiquei interessado."
chat "Estou procurando um apartamento para comprar no Rio de Janeiro."
chat "Quero 3 quartos, de preferência no Leblon ou Ipanema."
chat "Meu orçamento é de até R\$ 2 milhões."
chat "Precisa ter vaga de garagem. Andar alto seria ótimo."
chat "Tenho financiamento pré-aprovado pela Caixa Econômica."
chat "Gostaria de visitar algum imóvel ainda essa semana."
chat "Vocês têm algo com vista para o mar?"
chat "Pode me enviar as fotos e o valor do condomínio?"
chat "Perfeito. Qual o próximo passo para agendar a visita?"

echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
echo -e "${YELLOW}  End of demo conversation${RESET}"
echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${RESET}"
echo ""
