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
        if (args.Length == 4 && args[0] == "--worker" && int.TryParse(args[2], out var parentPid)) { RunWorker(args[1], parentPid, args[3]); return; }
        if (args.Length > 0 && args[0].StartsWith("--ipc-", StringComparison.Ordinal)) { IpcSmoke.Run(args); return; }
        var localizationSmoke = args.Contains("--localization-smoke");
        var smoke = args.Contains("--smoke") || localizationSmoke;
        var testFolder = smoke ? Path.Combine(Path.GetTempPath(), "ViVeUI-smoke-" + Guid.NewGuid().ToString("N")) : null;
        var app = new Application();
        app.DispatcherUnhandledException += (_, e) => { if (smoke) { File.WriteAllText("smoke-error.txt", e.Exception.ToString()); e.Handled = true; app.Shutdown(1); return; }
            var locale = (app.MainWindow as MainWindow)?.L ?? new Locale(); LocalizedDialog.Show(app.MainWindow, locale, locale["Error"], locale.ErrorSummary(e.Exception), technical: e.Exception.ToString()); e.Handled = true; };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("Theme.xaml", UriKind.Relative) });
        MainWindow window;
        try { window = new MainWindow(args.Contains("--demo") || smoke, testFolder); }
        catch (Exception e) { File.WriteAllText(smoke ? "smoke-error.txt" : Path.Combine(Path.GetTempPath(), "ViVeUI-startup-error.txt"), e.ToString()); if (!smoke) { var locale = new Locale(); LocalizedDialog.Show(null, locale, locale["Error"], locale.ErrorSummary(e), technical: e.ToString()); } Environment.ExitCode = 1; return; }
        var smokeStarted = false;
        if (smoke)
            window.ContentRendered += async (_, _) =>
            {
                if (smokeStarted) return; smokeStarted = true;
                try { if (localizationSmoke) await window.LocalizationSmokeAsync(); else await window.SmokeAsync(); app.Shutdown(0); }
                catch (Exception e) { File.WriteAllText("smoke-error.txt", e.ToString()); app.Shutdown(1); }
            };
        try { app.Run(window); } finally { if (testFolder is not null) { try { Directory.Delete(testFolder, true); } catch (IOException) { } } }
    }
    static void RunWorker(string pipeName, int parentPid, string secret)
    {
        if (!pipeName.StartsWith("ViVeUI-", StringComparison.Ordinal) || !Guid.TryParseExact(pipeName[7..], "N", out _)) return;
        var locale = new Locale();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var pipe = WorkerChannel.Client(pipeName);
            pipe.Connect(30000);
            WorkerChannel.AuthenticateClient(pipe, parentPid, secret, timeout.Token).GetAwaiter().GetResult();
            var line = WorkerChannel.Read(pipe, timeout.Token).GetAwaiter().GetResult();
            var request = JsonSerializer.Deserialize<WorkerRequest>(line) ?? throw new InvalidDataException("Empty request.");
            var changes = request.Changes;
            locale.Set(request.Language);
            ChangeEngine.Validate(changes);
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18963)) throw new PlatformNotSupportedException("Windows build 18963 or newer is required.");
            // The elevated process also displays the exact scope. IPC never accepts file paths,
            // registry paths, commands, executables or download locations.
            var details = string.Join("\n", changes.Select(x => $"{Localization.FeatureId(x.Id)}: {locale.State(x.Before)} → {locale.State(x.After)}"));
            if (!LocalizedDialog.Show(null, locale, "ViVeUI · " + locale["Review"], locale["UserOverride"] + "\n\n" + locale["DefaultHelp"], confirm: true, scope: details))
            { WorkerChannel.Write(pipe, JsonSerializer.Serialize(new WorkerResponse(null, locale["Canceled"])), timeout.Token).GetAwaiter().GetResult(); return; }
            List<ChangeResult>? result = null; string? error = null;
            try { result = ChangeEngine.Apply(new WindowsStore(), changes); } catch (Exception e) { error = locale.ErrorSummary(e); }
            WorkerChannel.Write(pipe, JsonSerializer.Serialize(new WorkerResponse(result, error)), timeout.Token).GetAwaiter().GetResult();
        }
        catch (Exception e) { LocalizedDialog.Show(null, locale, "ViVeUI · " + locale["Error"], locale.ErrorSummary(e), technical: e.ToString()); }
    }
    public static async Task<WorkerResponse> ElevateAsync(List<Change> changes, string language)
    {
        var name = WorkerChannel.NewName(); var secret = WorkerChannel.NewSecret();
        using var pipe = WorkerChannel.Server(name);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", Arguments = $"--worker {name} {Environment.ProcessId} {secret}" }) ?? throw new IOException("Could not start elevated worker.");
        var connected = pipe.WaitForConnectionAsync(timeout.Token);
        var exited = process.WaitForExitAsync(timeout.Token);
        if (await Task.WhenAny(connected, exited) == exited && !pipe.IsConnected) throw new IOException("Worker exited before connecting.");
        await connected;
        await WorkerChannel.AuthenticateServer(pipe, process.Id, secret, timeout.Token);
        await WorkerChannel.Write(pipe, JsonSerializer.Serialize(new WorkerRequest(changes, language)), timeout.Token);
        var response = await WorkerChannel.Read(pipe, timeout.Token);
        return JsonSerializer.Deserialize<WorkerResponse>(response) ?? throw new IOException("Invalid worker response.");
    }
}
public sealed record WorkerResponse(List<ChangeResult>? Results, string? Error);

public sealed record WorkerRequest(List<Change> Changes, string Language);
