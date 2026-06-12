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

    private readonly string _lockFilePath;
    private readonly string _pipeName;
    private FileStream? _lockFile;
    private CancellationTokenSource? _serverCancellation;
    private Task? _serverTask;

    public SingleInstanceCoordinator(string instanceName = "noopad")
    {
        var safeName = Regex.Replace(instanceName, "[^A-Za-z0-9_.-]", "-");
        var lockDirectory = Path.Combine(Path.GetTempPath(), "noopad");
        _lockFilePath = Path.Combine(lockDirectory, $"{safeName}-single-instance.lock");
        _pipeName = $"{safeName}-single-instance";
    }

    public bool TryBecomePrimary()
    {
        if (_lockFile != null)
            return true;

        Directory.CreateDirectory(Path.GetDirectoryName(_lockFilePath)!);

        try
        {
            _lockFile = new FileStream(
                _lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void StartServer(Func<IReadOnlyList<string>, Task> handleLaunchRequest)
    {
        if (_lockFile == null)
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

        _lockFile?.Dispose();
        _lockFile = null;
    }
}

public sealed class SingleInstanceLaunchRequest
{
    public List<string> Arguments { get; set; } = new();
}
