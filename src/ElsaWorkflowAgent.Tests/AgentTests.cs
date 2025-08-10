using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;
using ElsaWorkflowAgent.Agents;

namespace ElsaWorkflowAgent.Tests;

public class AgentTests
{
    [Fact]
    public async Task BaseAgent_ExecuteAsync_ShouldCallExecuteInternalAsync()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TestAgent>>();
        var mockKernel = new Mock<Microsoft.SemanticKernel.Kernel>();
        var agent = new TestAgent(mockLogger.Object, mockKernel.Object);
        var input = "test input";
        var context = new AgentContext();

        // Act
        var result = await agent.ExecuteAsync(input, context);

        // Assert
        Assert.Equal("test response", result);
    }

    [Fact]
    public void BaseAgent_CanHandle_ShouldReturnTrue()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<TestAgent>>();
        var mockKernel = new Mock<Microsoft.SemanticKernel.Kernel>();
        var agent = new TestAgent(mockLogger.Object, mockKernel.Object);

        // Act
        var result = agent.CanHandle("test input");

        // Assert
        Assert.True(result);
    }

    private class TestAgent : BaseAgent<string>
    {
        public TestAgent(ILogger<TestAgent> logger, Microsoft.SemanticKernel.Kernel kernel) : base(logger, kernel)
        {
        }

        public override string Id => "test-agent";
        public override string Name => "Test Agent";
        public override string Description => "A test agent";
        public override AgentMetadata Metadata => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            InputType = typeof(string),
            OutputType = typeof(string)
        };

        protected override Task<string> ExecuteInternalAsync(object input, AgentContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult("test response");
        }
    }
}
