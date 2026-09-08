using Xunit;

namespace SharpDispatch.Tests;

public class CommandDispatchResultTests
{
    [Fact]
    public void Ok_WithoutMessage_ReturnsSuccessfulResult()
    {
        var result = CommandDispatchResult.Ok();

        Assert.True(result.Success);
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Ok_WithMessage_PreservesMessage()
    {
        var result = CommandDispatchResult.Ok("done");

        Assert.True(result.Success);
        Assert.Equal("done", result.Message);
    }

    [Fact]
    public void Fail_SetsSuccessFalseAndMessage()
    {
        var result = CommandDispatchResult.Fail("boom");

        Assert.False(result.Success);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("boom", result.Message);
    }

    [Fact]
    public void FromException_SetsFailureWithExceptionMessage()
    {
        var result = CommandDispatchResult.FromException(new InvalidOperationException("it broke"));

        Assert.False(result.Success);
        Assert.True(result.IsFailure);
        Assert.Equal("it broke", result.Message);
    }

    [Fact]
    public void FromException_Null_ThrowsArgumentNullException()
        => Assert.Throws<ArgumentNullException>(() => CommandDispatchResult.FromException(null!));

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(CommandDispatchResult.Ok("x"), CommandDispatchResult.Ok("x"));
        Assert.NotEqual(CommandDispatchResult.Ok("x"), CommandDispatchResult.Ok("y"));
    }
}
