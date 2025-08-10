using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ElsaWorkflowAgent.Decisions;

namespace ElsaWorkflowAgent.Tests;

public class DecisionTests
{
    [Fact]
    public async Task BaseDecision_ExecuteAsync_ShouldCallExecuteInternalAsync()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TestDecision>>();
        var decision = new TestDecision(mockLogger.Object);
        var input = "test input";
        var context = new DecisionContext();

        // Act
        var result = await decision.ExecuteAsync(input, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("success", result.NextStep);
    }

    [Fact]
    public void BaseDecision_CanHandle_ShouldReturnTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TestDecision>>();
        var decision = new TestDecision(mockLogger.Object);

        // Act
        var result = decision.CanHandle("test input");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DecisionResult_Success_ShouldCreateSuccessResult()
    {
        // Act
        var result = DecisionResult.CreateSuccess("next-step", "test data");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("next-step", result.NextStep);
        Assert.Equal("test data", result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void DecisionResult_Failure_ShouldCreateFailureResult()
    {
        // Act
        var result = DecisionResult.Failure("error message");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("error message", result.ErrorMessage);
    }

    private class TestDecision : BaseDecision
    {
        public TestDecision(ILogger<TestDecision> logger) : base(logger)
        {
        }

        public override string Id => "test-decision";
        public override string Name => "Test Decision";
        public override string Description => "A test decision";
        public override DecisionMetadata Metadata => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            PossibleOutcomes = new List<string> { "success", "failure" }
        };

        protected override Task<DecisionResult> ExecuteInternalAsync(object input, DecisionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(DecisionResult.CreateSuccess("success"));
        }
    }
}
