using TerminalMCP.Utilities;

namespace TerminalMCP.Tests;

[TestClass]
public sealed class SliceLinesTests
{
    [TestMethod]
    public void Offset1_Limit20_StandardCase()
    {
        string[] lines = ["L1", "L2", "L3", "L4", "L5"];

        string[] result = TextHelper.SliceLines(lines, offset: 1, limit: 20);

        CollectionAssert.AreEqual(new[] { "L1", "L2", "L3", "L4", "L5" }, result);
    }

    [TestMethod]
    public void Offset1_Limit2_ReturnsLast2()
    {
        string[] lines = ["L1", "L2", "L3", "L4", "L5"];

        string[] result = TextHelper.SliceLines(lines, offset: 1, limit: 2);

        CollectionAssert.AreEqual(new[] { "L4", "L5" }, result);
    }

    [TestMethod]
    public void Offset2_Limit2_MiddleRange()
    {
        string[] lines = ["L1", "L2", "L3", "L4", "L5"];

        string[] result = TextHelper.SliceLines(lines, offset: 2, limit: 2);

        CollectionAssert.AreEqual(new[] { "L3", "L4" }, result);
    }

    [TestMethod]
    public void OffsetExceedsTotal_ReturnsEmpty()
    {
        string[] lines = ["L1", "L2"];

        string[] result = TextHelper.SliceLines(lines, offset: 10, limit: 5);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void EmptyArray_ReturnsEmpty()
    {
        string[] lines = [];

        string[] result = TextHelper.SliceLines(lines, offset: 1, limit: 20);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Offset1_Limit1_SingleLine()
    {
        string[] lines = ["first", "middle", "last"];

        string[] result = TextHelper.SliceLines(lines, offset: 1, limit: 1);

        CollectionAssert.AreEqual(new[] { "last" }, result);
    }

    [TestMethod]
    public void Offset3_Limit10_ClampsStart()
    {
        string[] lines = ["A", "B", "C"];

        string[] result = TextHelper.SliceLines(lines, offset: 3, limit: 10);

        CollectionAssert.AreEqual(new[] { "A" }, result);
    }
}

