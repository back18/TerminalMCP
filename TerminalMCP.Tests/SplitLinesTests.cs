using TerminalMCP.Utilities;

namespace TerminalMCP.Tests;

[TestClass]
public sealed class SplitLinesTests
{
    [TestMethod]
    public void UnixNewlines()
    {
        string[] result = TextHelper.SplitLines("a\nb\nc");

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result);
    }

    [TestMethod]
    public void WindowsNewlines()
    {
        string[] result = TextHelper.SplitLines("a\r\nb\r\nc");

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result);
    }

    [TestMethod]
    public void TrailingNewline_Stripped()
    {
        string[] result = TextHelper.SplitLines("a\nb\nc\n");

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, result);
    }

    [TestMethod]
    public void MultipleTrailingEmptyLines_AllStripped()
    {
        string[] result = TextHelper.SplitLines("hello\n\n\n");

        CollectionAssert.AreEqual(new[] { "hello" }, result);
    }

    [TestMethod]
    public void SingleLine_NoNewline()
    {
        string[] result = TextHelper.SplitLines("hello");

        CollectionAssert.AreEqual(new[] { "hello" }, result);
    }

    [TestMethod]
    public void EmptyString()
    {
        string[] result = TextHelper.SplitLines("");

        Assert.IsEmpty(result);
    }
}

