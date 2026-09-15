namespace GraphQLGatewayService.UnitTests.GraphQL;

using GraphQLGatewayService.Api.GraphQL.Errors;
using GraphQLGatewayService.Domain.Common;
using HotChocolate;
using DomainError = GraphQLGatewayService.Domain.Common.Error;

/// <summary>
/// The mapping from <see cref="ErrorCode"/> to <c>extensions.code</c>. A GraphQL response is 200
/// whatever happens, so that string is the only machine-readable signal a client gets.
/// </summary>
public sealed class ResultExtensionsTests
{
    [Fact]
    public void ValueOrThrow_SuccessfulResult_ReturnsValue() =>
        Assert.Equal(42, Result.Success(42).ValueOrThrow());

    [Theory]
    [InlineData(ErrorCode.Validation, "BAD_USER_INPUT")]
    [InlineData(ErrorCode.NotFound, "NOT_FOUND")]
    [InlineData(ErrorCode.Transient, "SERVICE_UNAVAILABLE")]
    [InlineData(ErrorCode.Permanent, "INTERNAL_SERVER_ERROR")]
    public void ValueOrThrow_FailedResult_ThrowsWithMappedCode(ErrorCode code, string expected)
    {
        var result = Result.Failure<int>(Create(code, "something went wrong"));

        var exception = Assert.Throws<GraphQLException>(() => result.ValueOrThrow());

        var error = Assert.Single(exception.Errors);
        Assert.Equal(expected, error.Code);
    }

    [Fact]
    public void ValueOrThrow_FailedResult_KeepsTheActionableMessage()
    {
        var result = Result.Failure<int>(DomainError.CreateValidation("from must be earlier than to."));

        var exception = Assert.Throws<GraphQLException>(() => result.ValueOrThrow());

        Assert.Equal("from must be earlier than to.", Assert.Single(exception.Errors).Message);
    }

    [Fact]
    public void ValueOrThrow_NullResult_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ((Result<int>)null!).ValueOrThrow());

    private static DomainError Create(ErrorCode code, string description) =>
        code switch
        {
            ErrorCode.Validation => DomainError.CreateValidation(description),
            ErrorCode.NotFound => DomainError.CreateNotFound(description),
            ErrorCode.Transient => DomainError.CreateTransient(description),
            ErrorCode.Permanent => DomainError.CreatePermanent(description),
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unhandled code."),
        };
}
