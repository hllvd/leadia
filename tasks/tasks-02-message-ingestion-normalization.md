# Message Ingestion and Normalization Tasks

## Small Tasks

1. ~~Create the POST /webhook/whatsapp endpoint in the API gateway.~~
2. ~~Parse the raw WhatsApp payload from the request body.~~
3. ~~Extract broker phone number, customer phone number, sender type, message text, and timestamp from the payload.~~
4. ~~Generate conversation_id as <broker_number>-<customer_number>.~~
5. ~~Apply text normalization rules: trim, collapse multiple spaces, normalize newlines, encode as UTF-8.~~
6. ~~Validate sender_type is either "broker" or "customer", return 400 otherwise.~~
7. ~~Convert timestamp to ISO 8601 format.~~
8. ~~Create NormalizedMessage object with all required fields.~~
9. ~~Publish the normalized message to NATS stream 'messages' with subject 'message.received'.~~
10. ~~Handle and log any errors during normalization, return appropriate HTTP status codes.~~
11. ~~Test normalization with various WhatsApp payload formats.~~
12. ~~Ensure the endpoint handles malformed payloads gracefully.~~