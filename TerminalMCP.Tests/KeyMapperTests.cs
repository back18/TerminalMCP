using TerminalMCP.Interop;
using TerminalMCP.Utilities;

namespace TerminalMCP.Tests;

[TestClass]
public sealed class KeyMapperTests
{
    [TestMethod]
    public void KnownKeys_MapToCorrectVk()
    {
        Assert.AreEqual(NativeMethods.VkReturn, KeyMapper.KeyNameToVk("enter"));
        Assert.AreEqual(NativeMethods.VkReturn, KeyMapper.KeyNameToVk("return"));
        Assert.AreEqual(NativeMethods.VkEscape, KeyMapper.KeyNameToVk("escape"));
        Assert.AreEqual(NativeMethods.VkEscape, KeyMapper.KeyNameToVk("esc"));
        Assert.AreEqual(NativeMethods.VkTab, KeyMapper.KeyNameToVk("tab"));
        Assert.AreEqual(NativeMethods.VkSpace, KeyMapper.KeyNameToVk("space"));
        Assert.AreEqual(NativeMethods.VkBack, KeyMapper.KeyNameToVk("backspace"));
        Assert.AreEqual(NativeMethods.VkDelete, KeyMapper.KeyNameToVk("delete"));
        Assert.AreEqual(NativeMethods.VkDelete, KeyMapper.KeyNameToVk("del"));
        Assert.AreEqual(NativeMethods.VkUp, KeyMapper.KeyNameToVk("up"));
        Assert.AreEqual(NativeMethods.VkDown, KeyMapper.KeyNameToVk("down"));
        Assert.AreEqual(NativeMethods.VkLeft, KeyMapper.KeyNameToVk("left"));
        Assert.AreEqual(NativeMethods.VkRight, KeyMapper.KeyNameToVk("right"));
        Assert.AreEqual(NativeMethods.VkHome, KeyMapper.KeyNameToVk("home"));
        Assert.AreEqual(NativeMethods.VkEnd, KeyMapper.KeyNameToVk("end"));
        Assert.AreEqual(NativeMethods.VkY, KeyMapper.KeyNameToVk("y"));
        Assert.AreEqual(NativeMethods.VkN, KeyMapper.KeyNameToVk("n"));
        Assert.AreEqual(NativeMethods.VkA, KeyMapper.KeyNameToVk("a"));
        Assert.AreEqual(NativeMethods.VkC, KeyMapper.KeyNameToVk("c"));
        Assert.AreEqual(NativeMethods.VkV, KeyMapper.KeyNameToVk("v"));
        Assert.AreEqual(NativeMethods.VkD, KeyMapper.KeyNameToVk("d"));
    }

    [TestMethod]
    public void CaseInsensitive()
    {
        Assert.AreEqual(NativeMethods.VkReturn, KeyMapper.KeyNameToVk("ENTER"));
        Assert.AreEqual(NativeMethods.VkEscape, KeyMapper.KeyNameToVk("ESC"));
        Assert.AreEqual(NativeMethods.VkY, KeyMapper.KeyNameToVk("Y"));
    }

    [TestMethod]
    public void UnknownKey_ReturnsNull()
    {
        Assert.IsNull(KeyMapper.KeyNameToVk("f1"));
        Assert.IsNull(KeyMapper.KeyNameToVk("ctrl"));
        Assert.IsNull(KeyMapper.KeyNameToVk(""));
        Assert.IsNull(KeyMapper.KeyNameToVk("shift"));
    }
}
