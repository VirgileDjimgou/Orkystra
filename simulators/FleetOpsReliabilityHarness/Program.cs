using FleetOpsReliabilityHarness;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var options = ReliabilityOptions.Parse(args);
    using var runner = new ReliabilityRunner(options);
    return await runner.RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Reliability harness cancelled.");
    return 130;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Reliability harness failed: {exception.Message}");
    return 3;
}
