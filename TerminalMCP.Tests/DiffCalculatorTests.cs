using TerminalMCP.Models;
using TerminalMCP.Utilities;

namespace TerminalMCP.Tests;

[TestClass]
public sealed class DiffCalculatorTests
{
    [TestMethod]
    public void AppendOnly_ReturnsAppendedLines()
    {
        string[] previous = ["L1", "L2", "L3"];
        string[] current = ["L1", "L2", "L3", "L4", "L5"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "L4", "L5" }, result.NewLines);
        Assert.AreEqual(4, result.StartLine);
    }

    [TestMethod]
    public void NoChange_ReturnsNoChange()
    {
        string[] previous = ["L1", "L2", "L3"];
        string[] current = ["L1", "L2", "L3"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.NoChange, result.Status);
        Assert.IsEmpty(result.NewLines);
    }

    [TestMethod]
    public void Divergence_ReturnsFromDivergence()
    {
        string[] previous = ["L1", "L2", "L3"];
        string[] current = ["L1", "CHANGED", "L3", "L4"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "CHANGED", "L3", "L4" }, result.NewLines);
        Assert.AreEqual(2, result.StartLine);
    }

    [TestMethod]
    public void EmptyPrevious_ReturnsAllCurrent()
    {
        string[] previous = [];
        string[] current = ["L1", "L2"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "L1", "L2" }, result.NewLines);
        Assert.AreEqual(1, result.StartLine);
    }

    [TestMethod]
    public void EmptyCurrent_ReturnsNewWithNoLines()
    {
        string[] previous = ["L1", "L2"];
        string[] current = [];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        Assert.IsEmpty(result.NewLines);
        Assert.AreEqual(1, result.StartLine);
    }

    [TestMethod]
    public void CompletelyDifferent_ReturnsAllCurrent()
    {
        string[] previous = ["A", "B", "C"];
        string[] current = ["X", "Y", "Z"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "X", "Y", "Z" }, result.NewLines);
        Assert.AreEqual(1, result.StartLine);
    }

    [TestMethod]
    public void DivergenceAtSecondLine()
    {
        string[] previous = ["A", "B", "C", "D"];
        string[] current = ["A", "B", "X", "Y"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "X", "Y" }, result.NewLines);
        Assert.AreEqual(3, result.StartLine);
    }

    [TestMethod]
    public void TailShorter_SamePrefix_ReturnsEmpty()
    {
        string[] previous = ["A", "B", "C", "D", "E", "F", "G", "H"];
        string[] current = ["A", "B", "C", "D"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        // Prefix matches but current ends earlier — no new content to return
        Assert.AreEqual(DiffStatus.NoChange, result.Status);
        Assert.IsEmpty(result.NewLines);
    }

    [TestMethod]
    public void BufferRollover_MostCommon_HeadTrimmed_TailAppended()
    {
        // ABC trimmed from head, LMN appended at tail
        string[] previous = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K"];
        string[] current = ["D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "L", "M", "N" }, result.NewLines);
        Assert.AreEqual(9, result.StartLine);
    }

    [TestMethod]
    public void BufferRollover_HeadTrimmed_MoreNewLines()
    {
        // ABC trimmed, 6 new lines appended
        string[] previous = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K"];
        string[] current = ["D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "L", "M", "N", "O", "P", "Q" }, result.NewLines);
        Assert.AreEqual(9, result.StartLine);
    }

    // ── Buffer rollover scenarios (head trimmed, tail overlap) ──

    [TestMethod]
    public void BufferRollover_HeadTrimmed_TailOverlap_NewLines()
    {
        // A-C were trimmed from buffer, X-Y are new output
        string[] previous = ["A", "B", "C", "D", "E", "F"];
        string[] current = ["D", "E", "F", "X", "Y"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "X", "Y" }, result.NewLines);
    }

    [TestMethod]
    public void BufferRollover_HeadTrimmed_NoNewContent()
    {
        // A-C trimmed, nothing new
        string[] previous = ["A", "B", "C", "D", "E"];
        string[] current = ["D", "E"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.NoChange, result.Status);
        Assert.IsEmpty(result.NewLines);
    }

    [TestMethod]
    public void BufferRollover_SmallOverlap_OneNewLine()
    {
        // Only last 2 lines overlap
        string[] previous = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J"];
        string[] current = ["I", "J", "X"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "X" }, result.NewLines);
    }

    [TestMethod]
    public void BufferRollover_LargeBuffer_PartialOverlap()
    {
        // 9000-line buffer, 500 trimmed from head, 500 new at tail
        string[] previous = new string[9000];
        for (int i = 0; i < 9000; i++)
            previous[i] = $"L{i + 1}";

        string[] current = new string[9000];
        for (int i = 0; i < 9000; i++)
            current[i] = $"L{i + 501}";

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        Assert.HasCount(500, result.NewLines);
        Assert.AreEqual("L9001", result.NewLines[0]);
        Assert.AreEqual("L9500", result.NewLines[499]);
    }

    [TestMethod]
    public void BufferRollover_FirstAnchorShort_RetryFindsFullMatch()
    {
        // Prefix fails at line 0 (X≠A). First "A" at idx=3 only matches 3 lines
        // (ABC then A≠D). Retry at idx=6 finds full 5-line overlap (ABCDE).
        string[] previous = ["X", "Y", "Z", "A", "B", "C", "A", "B", "C", "D", "E"];
        string[] current = ["A", "B", "C", "D", "E", "A", "B", "C", "NEW"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "NEW" }, result.NewLines);
        Assert.AreEqual(6, result.StartLine);
    }

    [TestMethod]
    public void BufferRollover_RepeatedPrefix_RetryPastShortMatch()
    {
        // "A" at idx=0 matches 3 lines (ABC then A≠D). Retry at idx=3
        // matches 5 lines (ABCDE). New content is "A", "B", "C", "NEW".
        string[] previous = ["A", "B", "C", "A", "B", "C", "D", "E"];
        string[] current = ["A", "B", "C", "D", "E", "A", "B", "C", "NEW"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "NEW" }, result.NewLines);
        Assert.AreEqual(6, result.StartLine);
    }

    [TestMethod]
    public void BufferRollover_NoisyMiddle_RetryPastShortMatch()
    {
        // "A" at idx=0 matches 3 lines (ABC then X≠D). "A" at idx=6 matches
        // full 5-line overlap (ABCDE). Noise in between (XYZ) is skipped.
        string[] previous = ["A", "B", "C", "X", "Y", "Z", "A", "B", "C", "D", "E"];
        string[] current = ["A", "B", "C", "D", "E", "A", "B", "C", "NEW"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "NEW" }, result.NewLines);
        Assert.AreEqual(6, result.StartLine);
    }

    [TestMethod]
    public void BufferRollover_DensePrefixes_FourRetriesBeforeMatch()
    {
        // "A" at idx=0→1 match, idx=1→2, idx=3→3, idx=6→4, idx=10→5 ✓
        string[] previous = ["A", "A", "B", "A", "B", "C", "A", "B", "C", "D", "A", "B", "C", "D", "E"];
        string[] current = ["A", "B", "C", "D", "E", "A", "B", "C", "NEW"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "A", "B", "C", "NEW" }, result.NewLines);
        Assert.AreEqual(6, result.StartLine);
    }

    [TestMethod]
    public void BufferRollover_TwoMatchesOnly_NotQualified_FallsBack()
    {
        // AB matches 2 lines (<3, not at end) → skipped, no qualified → fallback.
        string[] previous = ["X", "A", "B", "Z", "Z", "Z"];
        string[] current = ["A", "B", "E", "NEW"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "A", "B", "E", "NEW" }, result.NewLines);
        Assert.AreEqual(1, result.StartLine);
    }

    [TestMethod]
    public void BufferRollover_ShortMatchAtStart_LongerMatchInMiddle()
    {
        // idx=0 has 5 matches (ABCDE), idx=6 has 10 matches (ABCDEFGHIJ).
        // Best-match picks idx=6 — only "NEW" is truly new.
        string[] previous = ["A", "B", "C", "D", "E", "X", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J"];
        string[] current = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "NEW"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "NEW" }, result.NewLines);
        Assert.AreEqual(11, result.StartLine);
    }

    [TestMethod]
    public void BufferRollover_BestIsFour_UsesItAnyway()
    {
        // Candidates: 1, 2, 3, 4 matches. Picks best (4 at idx=6).
        string[] previous = ["A", "A", "B", "A", "B", "C", "A", "B", "C", "D", "X", "Y", "Z"];
        string[] current = ["A", "B", "C", "D", "E", "A", "B", "C", "NEW"];

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        CollectionAssert.AreEqual(new[] { "E", "A", "B", "C", "NEW" }, result.NewLines);
        Assert.AreEqual(5, result.StartLine);
    }


    [TestMethod]
    public void LargeAppend_StartLineCorrect()
    {
        string[] previous = new string[100];
        Array.Fill(previous, "same");
        string[] current = new string[150];
        Array.Fill(current, "same");

        DiffOutput result = DiffCalculator.Compute(previous, current);

        Assert.AreEqual(DiffStatus.New, result.Status);
        Assert.HasCount(50, result.NewLines);
        Assert.AreEqual(101, result.StartLine);
    }
}
