public class TestDaprHost
{
    private Process _daprProcess;
    public int HttpPort { get; } = 3500;
    public int GrpcPort { get; } = 50001;
    
    public async Task StartAsync()
    {
        // Start Dapr in test mode with required components
        _daprProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dapr",
            Arguments = $"run --app-id test-app --app-port 0 --dapr-http-port {HttpPort} --dapr-grpc-port {GrpcPort}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });
        
        // Wait for Dapr to start up
        await Task.Delay(TimeSpan.FromSeconds(2));
    }
    
    public async Task StopAsync()
    {
        if (_daprProcess != null && !_daprProcess.HasExited)
        {
            _daprProcess.Kill();
            await _daprProcess.WaitForExitAsync();
        }
    }
}