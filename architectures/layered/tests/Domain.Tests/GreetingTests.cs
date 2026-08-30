using LayeredArchitecture.Domain.Entities;
using Xunit;

namespace LayeredArchitecture.Domain.Tests;

public class GreetingTests
{
    [Fact]
    public void Constructor_WithNonEmptyMessage_SetsMessage()
    {
        var greeting = new Greeting("Hello, World!");

        Assert.Equal("Hello, World!", greeting.Message);
    }

    [Fact]
    public void Constructor_WithNullMessage_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Greeting(null!));
    }

    // Deliberately no test for new Greeting("") (message.Length == 0, the right-hand
    // side of the `||`): these two tests already report 100% branch/condition coverage
    // for the constructor's `if`, yet neither exercises that operand's true outcome —
    // confirmed by mutating `== 0` to `== 999` in Greeting.cs, which no test here catches.
    // Coverlet has no condition (MC/DC) coverage mode that would flag this gap.
}
