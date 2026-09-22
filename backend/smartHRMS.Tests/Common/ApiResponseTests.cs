using smartHRMS.Application.Common.Models;
using Xunit;

namespace smartHRMS.Tests.Common;

public class ApiResponseTests
{
    [Fact]
    public void Ok_WithData_SetsSuccessAndDataAndNoErrors()
    {
        var response = ApiResponse<string>.Ok("payload", "done");

        Assert.True(response.Success);
        Assert.Equal("done", response.Message);
        Assert.Equal("payload", response.Data);
        Assert.Null(response.Errors);
    }

    [Fact]
    public void Ok_WithoutData_HasNullDataAndNoErrors()
    {
        var response = ApiResponse.Ok("done");

        Assert.True(response.Success);
        Assert.Null(response.Data);
        Assert.Null(response.Errors);
    }

    [Fact]
    public void Fail_WithErrors_SetsFailureAndListsErrors()
    {
        var response = ApiResponse.Fail("Bad request.", new[] { "first", "second" });

        Assert.False(response.Success);
        Assert.Equal("Bad request.", response.Message);
        Assert.Null(response.Data);
        Assert.Equal(new[] { "first", "second" }, response.Errors);
    }

    [Fact]
    public void Fail_WithoutErrors_ReturnsEmptyErrorListNotNull()
    {
        var response = ApiResponse.Fail("Boom.");

        Assert.False(response.Success);
        Assert.NotNull(response.Errors);
        Assert.Empty(response.Errors!);
    }
}
