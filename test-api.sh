curl -X POST "http://localhost:8080/api/chat" -H "Content-Type: application/json" -d '{"brokerNumber":"AA","customerNumber":"BB","message":"Hello from curl","type":"customer"}'
echo ""
sleep 2
curl "http://localhost:8080/api/chat/AA-BB/history"
