/**
 * Builds a deterministic conversation ID from the broker and customer phone numbers.
 * Matches the backend implementation in MessageNormalizer.cs.
 * 
 * @param {string} brokerNumber 
 * @param {string} customerNumber 
 * @returns {string}
 */
export function buildConversationId(brokerNumber, customerNumber) {
  if (!brokerNumber || !customerNumber) return '';
  return `${brokerNumber.trim()}-${customerNumber.trim()}`;
}
