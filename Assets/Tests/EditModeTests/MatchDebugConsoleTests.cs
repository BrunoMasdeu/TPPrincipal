using NUnit.Framework;

public class MatchDebugConsoleTests
{
    [TestCase("match time 30", 30)]
    [TestCase("  MATCH   TIME  5  ", 5)]
    [TestCase("match time 3600", 3600)]
    public void AcceptsValidTimeCommand(string command, int expectedSeconds)
    {
        Assert.IsTrue(MatchDebugConsole.TryParseTimeCommand(command, out int seconds));
        Assert.AreEqual(expectedSeconds, seconds);
    }

    [TestCase("")]
    [TestCase("help")]
    [TestCase("match time")]
    [TestCase("match time 0")]
    [TestCase("match time -1")]
    [TestCase("match time 1.5")]
    [TestCase("match time 3601")]
    [TestCase("match time 30 extra")]
    public void RejectsInvalidCommand(string command)
    {
        Assert.IsFalse(MatchDebugConsole.TryParseTimeCommand(command, out _));
    }
}
