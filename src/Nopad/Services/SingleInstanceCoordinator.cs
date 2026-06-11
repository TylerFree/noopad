using System.IO.Pipes;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace Noopad.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _semaphoreName;
    private readonly string _pipeName;
    private Semaphore? _semaphore;
    private CancellationTokenSource? _serverCancellation;
    private Task? _serverTask;
    private bool _ownsSemaphore;

    public SingleInstanceCoordinator(string instanceName = "noopad")
    {
        var safeName = Regex.Replace(instanceName, "[^A-Za-z0-9_.-]", "-");
        _semaphoreName = $@"Local\{safeName}-single-instance";
        _pipeName = $"{safeName}-single-instance";
    }

    public bool TryBecomePrimary()
    {
        if (_ownsSemaphore)
            return true;

        _semaphore = new Semaphore(initialCount: 1, maximumCount: 1, _semaphoreName);
        if (_semaphore.WaitOne(millisecondsTimeout: 0))
        {
            _ownsSemaphore = true;
            return true;
        }

        _semaphore.Dispose();
        _semaphore = null;
        return false;
    }

    public void StartServer(Func<IReadOnlyList<string>, Task> handleLaunchRequest)
    {
        if (!_ownsSemaphore)
            throw new InvalidOperationException("Only the primary instance can start the single-instance server.");

        if (_serverTask != null)
            return;

        _serverCancellation = new CancellationTokenSource();
        _serverTask = Task.Run(() => RunServerAsync(handleLaunchRequest, _serverCancellation.Token));
    }

    public async Task<bool> TrySendLaunchRequestAsync(IEnumerable<string> arguments, TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTimeOffset.UtcNow < deadline)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
                break;

            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".",
                    _pipeName,
                    PipeDirection.Out,
                    PipeOptions.Asynchronous);

                using var connectCancellation = new CancellationTokenSource(remaining);
                await pipe.ConnectAsync(connectCancellation.Token);

                var request = new SingleInstanceLaunchRequest
                {
                    Arguments = arguments.Where(arg => !string.IsNullOrWhiteSpace(arg)).ToList()
                };
                var json = JsonSerializer.Serialize(request, JsonOptions);

                await using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };
                await writer.WriteLineAsync(json);
                return true;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
            catch (TimeoutException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }

        return false;
    }

    private async Task RunServerAsync(Func<IReadOnlyList<string>, Task> handleLaunchRequest, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(pipe, Encoding.UTF8);
                var json = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                var request = JsonSerializer.Deserialize<SingleInstanceLaunchRequest>(json, JsonOptions);
                if (request != null)
                    await handleLaunchRequest(request.Arguments);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Single-instance pipe request failed: {ex.Message}");
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Single-instance pipe request was invalid JSON: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        _serverCancellation?.Cancel();
        _serverCancellation?.Dispose();
        _serverCancellation = null;
        _serverTask = null;

        if (_ownsSemaphore && _semaphore != null)
            _semaphore.Release();

        _semaphore?.Dispose();
        _semaphore = null;
        _ownsSemaphore = false;
    }
}

public sealed class SingleInstanceLaunchRequest
{
    public List<string> Arguments { get; set; } = new();
}
