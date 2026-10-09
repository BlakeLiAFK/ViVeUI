using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ViVeUI.Core;
using Microsoft.Win32.SafeHandles;

namespace ViVeUI.Windows;

/// <summary>Fixed-scope self update helper; all paths are derived locally, never supplied over IPC.</summary>
public static class SelfUpdateWorker
{
    public static string Architecture => RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";
    public static bool TryHandle(string[] args)
    {
        if (args.Length == 4 && args[0] == "--update-worker" && int.TryParse(args[2], out var parent))
        { RunWorkerAsync(args[1], parent, args[3]).GetAwaiter().GetResult(); return true; }
        if (args.Length >= 1 && args[0] == "--update-probe")
        {
            if (args.Length is not (2 or 3) || !Guid.TryParseExact(args[1], "N", out _) || args.Length == 3 && args[2] != "--wait")
            { Environment.ExitCode = 2; return true; }
            Console.WriteLine(args[1]); Console.Out.Flush();
            if (args.Length == 3) Console.ReadLine();
            return true;
        }
        if (args.Length == 1 && args[0] == "--update-smoke")
        {
            try { SelfUpdateSmoke.RunAsync().GetAwaiter().GetResult(); }
            catch (Exception error) { File.WriteAllText("self-update-error.txt", error.ToString()); Environment.ExitCode = 1; }
            return true;
        }
        return false;
    }

    public static async Task<SelfUpdateSession> PrepareAsync(AppRelease release, CancellationToken cancellationToken = default)
    {
        SelfUpdateInstaller.ValidateRelease(release, BuildInfo.Version, Architecture);
        try { return await StartAsync(release, false, cancellationToken); }
        catch (SelfUpdateAccessException) { return await StartAsync(release, true, cancellationToken); }
    }
    static async Task<SelfUpdateSession> StartAsync(AppRelease release, bool elevate, CancellationToken cancellationToken)
    {
        var name = WorkerChannel.NewName(); var secret = WorkerChannel.NewSecret(); var pipe = WorkerChannel.Server(name);
        Process? process = null;
        try
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = elevate };
            if (elevate) info.Verb = "runas";
            info.ArgumentList.Add("--update-worker"); info.ArgumentList.Add(name); info.ArgumentList.Add(Environment.ProcessId.ToString()); info.ArgumentList.Add(secret);
            process = Process.Start(info) ?? throw new IOException("Could not start update helper.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var connected = pipe.WaitForConnectionAsync(timeout.Token);
            if (await Task.WhenAny(connected, process.WaitForExitAsync(timeout.Token)) != connected && !pipe.IsConnected)
                throw new IOException("Update helper exited before authentication.");
            await connected;
            await WorkerChannel.AuthenticateServer(pipe, process.Id, secret, timeout.Token);
            await WorkerChannel.Write(pipe, JsonSerializer.Serialize(release), timeout.Token);
            var response = JsonSerializer.Deserialize<UpdateWorkerResponse>(await WorkerChannel.Read(pipe, timeout.Token)) ?? throw new IOException("Empty update response.");
            if (response.Error is not null)
            {
                if (response.AccessDenied) throw new SelfUpdateAccessException(response.Error);
                throw new IOException(response.Error);
            }
            if (response.Prepared is null) throw new IOException("Missing prepared update receipt.");
            var expected = Path.GetFullPath(Environment.ProcessPath!);
            if (!string.Equals(response.Prepared.TargetPath, expected, StringComparison.OrdinalIgnoreCase) ||
                !response.Prepared.Sha256.Equals(release.Asset.Sha256, StringComparison.OrdinalIgnoreCase) || response.Prepared.Version != release.Version)
                throw new InvalidDataException("Update helper returned an unexpected installation scope.");
            return new(pipe, process, response.Prepared);
        }
        catch { pipe.Dispose(); process?.Dispose(); throw; }
    }
    static async Task RunWorkerAsync(string name, int parent, string secret)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
            using var pipe = WorkerChannel.Client(name);
            await pipe.ConnectAsync(30000, timeout.Token);
            await WorkerChannel.AuthenticateClient(pipe, parent, secret, timeout.Token);
            var release = JsonSerializer.Deserialize<AppRelease>(await WorkerChannel.Read(pipe, timeout.Token)) ?? throw new InvalidDataException("Missing release.");
            var target = Path.GetFullPath(Environment.ProcessPath!);
            var package = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ViVeUI", "downloads", $"ViVeUI-win-{Architecture}.exe");
            var installer = new SelfUpdateInstaller(new PhysicalUpdateFiles());
            PreparedSelfUpdate prepared;
            try
            {
                // Re-resolve repository metadata inside the helper; IPC cannot invent a trusted digest.
                using var service = new UpdateService();
                var trusted = await service.CheckAsync(BuildInfo.Version, Architecture, timeout.Token);
                if (trusted is null || trusted != release) throw new InvalidDataException("Release metadata changed. Check for updates again.");
                prepared = installer.Prepare(target, package, trusted, BuildInfo.Version, Architecture);
            }
            catch (Exception failure)
            {
                await WorkerChannel.Write(pipe, JsonSerializer.Serialize(new UpdateWorkerResponse(null, failure.Message, failure is UnauthorizedAccessException)), timeout.Token);
                return;
            }
            await WorkerChannel.Write(pipe, JsonSerializer.Serialize(new UpdateWorkerResponse(prepared, null, false)), timeout.Token);
            // No automatic launch, including on broken pipes or timeouts. The replacement remains recoverable.
            var command = await WorkerChannel.Read(pipe, timeout.Token);
            try
            {
                if (command == "rollback") installer.Rollback(target);
                else if (command is not ("later" or "launched")) throw new InvalidDataException("Invalid update helper command.");
                await WorkerChannel.Write(pipe, "ok", timeout.Token);
            }
            catch (Exception failure) { await WorkerChannel.Write(pipe, "error:" + failure.Message, timeout.Token); }
        }
        catch { Environment.ExitCode = 1; } // Caller reports transport failure; never open an unattended elevated dialog.
    }

    /// <summary>Recover interrupted swaps and clean a verified new installation. Never launches an executable.</summary>
    public static string? RecoverStartup()
    {
        var path = Environment.ProcessPath;
        if (path is null) return null;
        try
        {
            var installer = new SelfUpdateInstaller(new PhysicalUpdateFiles());
            if (path.EndsWith(SelfUpdateInstaller.BackupSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var original = path[..^SelfUpdateInstaller.BackupSuffix.Length];
                installer.Rollback(original);
                return "The original executable was restored. Close this recovery copy and reopen: " + original;
            }
            if (!File.Exists(path + SelfUpdateInstaller.JournalSuffix)) return null;
            var prepared = installer.Recover(path);
            if (prepared is not null) installer.Complete(path, BuildInfo.Version);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        { return "Update recovery remains pending: " + error.Message; }
    }
    public static void CompleteStartup() => RecoverStartup();
    sealed class SelfUpdateAccessException(string message) : IOException(message);
    sealed record UpdateWorkerResponse(PreparedSelfUpdate? Prepared, string? Error, bool AccessDenied);
}

/// <summary>Keep this session until the user answers the restart question. Only RestartAsync launches a process.</summary>
public sealed class SelfUpdateSession : IAsyncDisposable
{
    readonly NamedPipeServerStream pipe;
    readonly Process helper;
    readonly SemaphoreSlim gate = new(1, 1);
    bool finished;
    public PreparedSelfUpdate Prepared { get; }
    internal SelfUpdateSession(NamedPipeServerStream pipe, Process helper, PreparedSelfUpdate prepared)
        => (this.pipe, this.helper, Prepared) = (pipe, helper, prepared);
    public async Task RestartAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (finished) throw new InvalidOperationException("Update session already completed.");
            try
            {
                // This runs in the original unelevated UI process, never in the UAC helper.
                using var process = Process.Start(new ProcessStartInfo(Prepared.TargetPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Prepared.TargetPath)! })
                    ?? throw new IOException("Could not start the updated application.");
                await Task.Delay(1000);
                if (process.HasExited) throw new IOException("Updated application exited during startup; the old executable will be restored.");
            }
            catch (Exception launch)
            {
                try { await Command("rollback"); }
                catch (Exception rollback) { throw new AggregateException("Restart failed and rollback needs recovery. Keep the original application open.", launch, rollback); }
                throw;
            }
            // If acknowledgement is lost, do not roll back a successfully launched executable.
            try { await Command("launched"); }
            catch (IOException) { finished = true; }
            catch (OperationCanceledException) { finished = true; }
        }
        finally { gate.Release(); }
    }
    async Task Command(string command)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await WorkerChannel.Write(pipe, command, timeout.Token);
        var response = await WorkerChannel.Read(pipe, timeout.Token);
        finished = true;
        if (response != "ok") throw new IOException(response);
    }
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (!finished) { try { await Command("later"); } catch (IOException) { } catch (OperationCanceledException) { } }
            finished = true; pipe.Dispose(); helper.Dispose();
        }
        finally { gate.Release(); }
    }
}

internal class PhysicalUpdateFiles : IUpdateFileSystem
{
    static void Check(string path)
    {
        var full = Path.GetFullPath(path);
        for (var cursor = full; cursor is not null; cursor = Path.GetDirectoryName(cursor))
            if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Updates through symbolic links or junctions are not supported.");
    }
    public bool Exists(string path) { Check(path); return File.Exists(path); }
    public Stream OpenRead(string path) { Check(path); return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
    public virtual Version VersionOf(string path)
    {
        Check(path); var info = FileVersionInfo.GetVersionInfo(path);
        return new(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
    }
    public void CopyNew(string source, string target)
    {
        Check(source); Check(target);
        using var input = OpenRead(source);
        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough);
        input.CopyTo(output); output.Flush(true);
    }
    public void Move(string source, string target) { Check(source); Check(target); File.Move(source, target); }
    public void Delete(string path) { Check(path); File.Delete(path); }
    public string ReadText(string path) { Check(path); if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Update journal too large."); return File.ReadAllText(path); }
    public void WriteTextAtomic(string path, string text)
    {
        Check(path); var temporary = path + ".tmp"; Check(temporary);
        if (File.Exists(temporary)) File.Delete(temporary);
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { using var writer = new StreamWriter(stream, leaveOpen: true); writer.Write(text); writer.Flush(); stream.Flush(true); }
        File.Move(temporary, path, true);
    }
    public IDisposable Lock(string path)
    {
        // Pin every destination ancestor against rename/deletion for the entire transaction.
        // Checking reparse attributes alone would leave a junction-swap race across elevation.
        var directories = new List<SafeFileHandle>();
        try
        {
            var chain = new Stack<string>();
            for (var cursor = Path.GetDirectoryName(Path.GetFullPath(path)); cursor is not null; cursor = Path.GetDirectoryName(cursor)) chain.Push(cursor);
            foreach (var directory in chain)
            {
                var handle = CreateFile(directory, 0, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
                if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); if (error == 5) throw new UnauthorizedAccessException("Update directory access denied."); throw new Win32Exception(error); }
                directories.Add(handle);
                if (!GetFileInformationByHandleEx(handle, 9, out var info, (uint)Marshal.SizeOf<FileAttributeTag>())) throw new Win32Exception(Marshal.GetLastWin32Error());
                if ((info.Attributes & 0x400) != 0) throw new IOException("Update directory contains a reparse point.");
            }
            Check(path);
            var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new HeldLock(file, directories);
        }
        catch { foreach (var directory in directories) directory.Dispose(); throw; }
    }
    sealed class HeldLock(FileStream file, List<SafeFileHandle> directories) : IDisposable
    {
        public void Dispose() { file.Dispose(); foreach (var directory in directories) directory.Dispose(); }
    }
    [StructLayout(LayoutKind.Sequential)] struct FileAttributeTag { public uint Attributes; public uint Tag; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, out FileAttributeTag information, uint size);
}
