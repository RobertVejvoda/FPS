[Collection("DaprTests")]
public class DaprWorkflowTests : IAsyncLifetime
{
    private readonly DaprClient _daprClient;
    private readonly TestDaprHost _daprHost;
    
    public DaprWorkflowTests()
    {
        _daprHost = new TestDaprHost();
        _daprClient = new DaprClientBuilder()
            .UseHttpEndpoint($"http://localhost:{_daprHost.HttpPort}")
            .Build();
    }
    
    public async Task InitializeAsync()
    {
        await _daprHost.StartAsync();
    }
    
    public async Task DisposeAsync()
    {
        await _daprHost.StopAsync();
        _daprClient.Dispose();
    }
    
    [Fact]
    public async Task SlotAllocationWorkflow_EndToEnd_CompletesSuccessfully()
    {
        // Arrange
        var workflowId = Guid.NewGuid().ToString();
        var request = new SlotAllocationRequest(DateTime.Today.AddDays(1));
        
        // Act
        await _daprClient.StartWorkflowAsync(
            "SlotAllocationWorkflow",
            workflowId,
            request);
            
        // Wait for workflow completion with timeout
        var result = await WaitForWorkflowCompletion<SlotAllocationResult>(workflowId, TimeSpan.FromSeconds(30));
        
        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }
    
    private async Task<T> WaitForWorkflowCompletion<T>(string instanceId, TimeSpan timeout)
    {
        // Implementation to poll workflow status until completion or timeout
    }
}