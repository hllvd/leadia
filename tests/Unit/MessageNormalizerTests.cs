using Application.Services;

namespace Unit;

public class MessageNormalizerTests
{
    // ── BuildConversationId ───────────────────────────────────────────────────

    [Fact]
    public void BuildConversationId_Concatenates_BrokerAndCustomer()
    {
        var result = MessageNormalizer.BuildConversationId("4798913312", "47839948");
        Assert.Equal("4798913312-47839948", result);
    }

    [Fact]
    public void BuildConversationId_Trims_InputWhitespace()
    {
        var result = MessageNormalizer.BuildConversationId(" 123 ", " 456 ");
        Assert.Equal("123-456", result);
    }

    [Theory]
    [InlineData("", "456")]
    [InlineData("   ", "456")]
    [InlineData("123", "")]
    [InlineData("123", "   ")]
    public void BuildConversationId_Throws_OnNullOrWhitespace(string broker, string customer)
    {
        Assert.Throws<ArgumentException>(() =>
            MessageNormalizer.BuildConversationId(broker, customer));
    }

    [Theory]
    [InlineData("persist.message.123-456", "123-456")]
    [InlineData("message.received.abc", "abc")]
    [InlineData("abc", "abc")]
    [InlineData("", "")]
    public void ExtractConversationId_ReturnsLastPart(string subject, string expected)
    {
        Assert.Equal(expected, MessageNormalizer.ExtractConversationId(subject));
    }

    // ── Normalize ─────────────────────────────────────────────────────────────

    [Fact]
    public void Normalize_TrimsLeadingAndTrailingWhitespace()
    {
        var result = MessageNormalizer.Normalize("  hello  ");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Normalize_CollapsesMultipleSpaces()
    {
        var result = MessageNormalizer.Normalize("hello   world");
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Normalize_NormalizesCRLFToLF()
    {
        var result = MessageNormalizer.Normalize("line1\r\nline2");
        Assert.Equal("line1\nline2", result);
    }

    [Fact]
    public void Normalize_NormalizesCRToLF()
    {
        var result = MessageNormalizer.Normalize("line1\rline2");
        Assert.Equal("line1\nline2", result);
    }

    [Fact]
    public void Normalize_ReturnsEmpty_ForNullOrEmpty()
    {
        Assert.Equal(string.Empty, MessageNormalizer.Normalize(""));
        Assert.Equal(string.Empty, MessageNormalizer.Normalize(null!));
    }

    // ── ComputeHash ──────────────────────────────────────────────────────────

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        var h1 = MessageNormalizer.ComputeHash("ts", "broker1", "cust1", "hello");
        var h2 = MessageNormalizer.ComputeHash("ts", "broker1", "cust1", "hello");
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ComputeHash_ProducesLowercaseHex()
    {
        var hash = MessageNormalizer.ComputeHash("ts", "b", "c", "msg");
        Assert.Matches(@"^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeHash_DifferentText_ProducesDifferentHash()
    {
        var h1 = MessageNormalizer.ComputeHash("ts", "b", "c", "hello");
        var h2 = MessageNormalizer.ComputeHash("ts", "b", "c", "world");
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void ComputeHash_DifferentTimestamp_ProducesDifferentHash()
    {
        var h1 = MessageNormalizer.ComputeHash("ts1", "b", "c", "msg");
        var h2 = MessageNormalizer.ComputeHash("ts2", "b", "c", "msg");
        Assert.NotEqual(h1, h2);
    }

    // ── GetS3FileName ────────────────────────────────────────────────────────
    
    [Theory]
    [InlineData("123-456", 1, "123-456-part-1.json")]
    [InlineData("abc-def", 10, "abc-def-part-10.json")]
    [InlineData("  trim-me  ", 5, "trim-me-part-5.json")]
    public void GetS3FileName_ReturnsCorrectPattern(string convId, int part, string expected)
    {
        Assert.Equal(expected, MessageNormalizer.GetS3FileName(convId, part));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void GetS3FileName_Throws_OnNullOrWhitespace(string convId)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            MessageNormalizer.GetS3FileName(convId, 1));
    }
}
