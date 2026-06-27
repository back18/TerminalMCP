using TerminalMCP.Utilities;

namespace TerminalMCP.Tests;

[TestClass]
public sealed class BuildLinesTests
{
    [TestMethod]
    public void AscendingLineNumbers()
    {
        string[] lines = ["alpha", "beta"];

        string result = TextHelper.BuildLines(lines, start: 57);

        Assert.AreEqual("57|alpha\r\n58|beta", result);
    }

    [TestMethod]
    public void Padding_WidthMatchesLargest()
    {
        string[] lines = ["A"];

        string result = TextHelper.BuildLines(lines, start: 100);

        Assert.AreEqual("100|A", result);
    }

    [TestMethod]
    public void EmptyLines_ReturnsEmptyString()
    {
        string[] lines = [];

        string result = TextHelper.BuildLines(lines, start: 1);

        Assert.AreEqual(string.Empty, result);
    }
}
