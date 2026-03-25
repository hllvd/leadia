using System.Text;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Pure static function that assembles the LLM context payload from API.md §7.
/// Format:
///   SUMMARY\n&lt;rolling_summary&gt;\n\nFACTS\n&lt;key: value…&gt;\n\nRECENT MESSAGES\n&lt;buffer…&gt;\n\nNEW MESSAGE\n&lt;text&gt;
/// </summary>
public static class LlmContextBuilder
{
    /// <summary>
    /// Builds the full prompt context string to send to the LLM.
    /// </summary>
    /// <param name="rollingSummary">Current rolling summary (may be empty for new conversations).</param>
    /// <param name="facts">Current extracted facts.</param>
    /// <param name="buffer">Messages in the current buffer (oldest first).</param>
    /// <param name="incomingMessage">The latest incoming message text.</param>
    /// <param name="currentDateTime">The current datetime to help resolve relative dates.</param>
    public static string Build(
        string rollingSummary,
        IEnumerable<ConversationFact> facts,
        IEnumerable<string> buffer,
        string incomingMessage,
        DateTimeOffset currentDateTime)
    {
        var sb = new StringBuilder();

        sb.AppendLine("CURRENT DATETIME");
        sb.AppendLine(currentDateTime.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        sb.AppendLine();

        sb.AppendLine("SUMMARY");
        sb.AppendLine(rollingSummary);

        sb.AppendLine();
        sb.AppendLine("FACTS");
        if (facts != null)
        {
            foreach (var fact in facts)
                sb.AppendLine($"{fact.FactName}: {fact.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("RECENT MESSAGES");
        if (buffer != null)
        {
            foreach (var msg in buffer)
                sb.AppendLine(msg);
        }

        sb.AppendLine();
        sb.AppendLine("NEW MESSAGE");
        sb.Append(incomingMessage);

        return sb.ToString();
    }
}
