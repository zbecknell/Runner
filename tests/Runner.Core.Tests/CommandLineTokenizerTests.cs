using Runner.Core.Runners;

namespace Runner.Core.Tests;

public sealed class CommandLineTokenizerTests
{
    [Fact]
    public void Split_HandlesQuotedArguments()
    {
        var args = CommandLineTokenizer.Split("--urls \"http://localhost:5050\" --name \"my app\"");

        Assert.Equal(
            ["--urls", "http://localhost:5050", "--name", "my app"],
            args);
    }

    [Fact]
    public void Split_ReturnsEmptyListForBlankInput()
    {
        Assert.Empty(CommandLineTokenizer.Split("   "));
    }
}
