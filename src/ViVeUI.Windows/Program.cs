using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using ViVeUI.Core;

namespace ViVeUI.Windows;
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--worker") { RunWorker(args[1]); return; }
        var smoke = args.Contains("--smoke");
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) => { if (smoke) { File.WriteAllText("smoke-error.txt", e.Exception.ToString()); e.Handled = true; app.Shutdown(1); return; }
            MessageBox.Show(e.Exception.Message, "ViVeUI", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true; };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("Theme.xaml", UriKind.Relative) });
        MainWindow window;
        try { window = new MainWindow(args.Contains("--demo") || smoke); }
        catch (Exception e) { File.WriteAllText(smoke ? "smoke-error.txt" : Path.Combine(Path.GetTempPath(), "ViVeUI-startup-error.txt"), e.ToString()); if (!smoke) MessageBox.Show(e.Message, "ViVeUI"); Environment.ExitCode = 1; return; }
        var smokeStarted = false;
        if (args.Contains("--smoke"))
            window.ContentRendered += async (_, _) =>
            {
                if (smokeStarted) return; smokeStarted = true;
                try { await window.SmokeAsync(); app.Shutdown(0); }
                catch (Exception e) { File.WriteAllText("smoke-error.txt", e.ToString()); app.Shutdown(1); }
            };
        app.Run(window);
    }
    static void RunWorker(string pipeName)
    {
        if (!pipeName.StartsWith("ViVeUI-", StringComparison.Ordinal) || !Guid.TryParseExact(pipeName[7..], "N", out _)) return;
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(30000);
            using var reader = new StreamReader(pipe, leaveOpen: true);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            var line = reader.ReadLine();
            if (line is null || line.Length > 100_000) throw new InvalidDataException("Invalid worker request.");
            var request = JsonSerializer.Deserialize<WorkerRequest>(line) ?? throw new InvalidDataException("Empty request.");
            var changes = request.Changes;
            var locale = new Locale(); locale.Set(request.Language);
            ChangeEngine.Validate(changes);
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18963)) throw new PlatformNotSupportedException("Windows build 18963 or newer is required.");
            // The elevated process also displays the exact scope. IPC never accepts file paths,
            // registry paths, commands, executables or download locations.
            var details = string.Join("\n", changes.Select(x => $"{x.Id}: {locale.State(x.Before)} → {locale.State(x.After)}"));
            if (MessageBox.Show(locale["UserOverride"] + "\n\n" + details + "\n\n" + locale["DefaultHelp"], "ViVeUI — " + locale["Review"], MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            { writer.WriteLine(JsonSerializer.Serialize(new WorkerResponse(null, "Change canceled."))); return; }
            List<ChangeResult>? result = null; string? error = null;
            try { result = ChangeEngine.Apply(new WindowsStore(), changes); } catch (Exception e) { error = e.Message; }
            writer.WriteLine(JsonSerializer.Serialize(new WorkerResponse(result, error)));
        }
        catch (Exception e) { MessageBox.Show(e.Message, "ViVeUI worker", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    public static async Task<WorkerResponse> ElevateAsync(List<Change> changes, string language)
    {
        var name = "ViVeUI-" + Guid.NewGuid().ToString("N");
        using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", Arguments = "--worker " + name }) ?? throw new IOException("Could not start elevated worker.");
        var connected = pipe.WaitForConnectionAsync(timeout.Token);
        var exited = process.WaitForExitAsync(timeout.Token);
        if (await Task.WhenAny(connected, exited) == exited && !pipe.IsConnected) throw new IOException("Worker exited before connecting.");
        await connected;
        using var reader = new StreamReader(pipe, leaveOpen: true);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync(JsonSerializer.Serialize(new WorkerRequest(changes, language)));
        var response = await reader.ReadLineAsync(timeout.Token) ?? throw new IOException("Worker disconnected. Review pending history before retrying.");
        return JsonSerializer.Deserialize<WorkerResponse>(response) ?? throw new IOException("Invalid worker response.");
    }
}
public sealed record WorkerResponse(List<ChangeResult>? Results, string? Error);

public sealed record WorkerRequest(List<Change> Changes, string Language);
