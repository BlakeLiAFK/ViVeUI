using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ViVeUI.Core;

namespace ViVeUI.Windows;

/// <summary>Offline controlled-copy smoke. It never changes the installed executable or Windows feature state.</summary>
internal static class SelfUpdateSmoke
{
    public static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ViVeUI update 测试 " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Process? running = null;
        try
        {
            var current = Environment.ProcessPath ?? throw new IOException("Missing executable path.");
            var package = Path.Combine(root, $"ViVeUI-win-{SelfUpdateWorker.Architecture}.exe");
            File.Copy(current, package);
            var previous = Environment.GetEnvironmentVariable("VIVEUI_PREVIOUS_EXE");
            var priorDigest = Environment.GetEnvironmentVariable("VIVEUI_PREVIOUS_SHA256");
            var crossVersion = !string.IsNullOrWhiteSpace(previous);
            if (crossVersion && (string.IsNullOrWhiteSpace(priorDigest) || !Hash(previous!).Equals(priorDigest, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Previous release fixture requires a matching trusted GitHub SHA-256 digest.");
            var target = Path.Combine(root, "Current app 应用.exe"); File.Copy(crossVersion ? previous! : current, target);
            // A harmless trailer distinguishes old/new bytes while preserving the official runnable image.
            if (!crossVersion) using (var stream = new FileStream(target, FileMode.Append, FileAccess.Write)) stream.WriteByte(0x42);
            var oldHash = Hash(target); var newHash = Hash(package);
            var version = new PhysicalUpdateFiles().VersionOf(package);
            var installed = crossVersion ? new PhysicalUpdateFiles().VersionOf(target) : new Version(0, 0, 0, 0);
            var release = new AppRelease(version, new($"https://github.com/BlakeLiAFK/ViVeUI/releases/tag/v{version}"),
                new(Path.GetFileName(package), new($"https://github.com/BlakeLiAFK/ViVeUI/releases/download/v{version}/{Path.GetFileName(package)}"), newHash, new FileInfo(package).Length));
            PhysicalUpdateFiles files = crossVersion ? new PhysicalUpdateFiles() : new FixtureFiles(target, oldHash, installed);
            var installer = new SelfUpdateInstaller(files);
            var nonce = Guid.NewGuid().ToString("N");
            if (crossVersion)
            {
                var start = new ProcessStartInfo(target) { UseShellExecute = false }; start.ArgumentList.Add("--demo");
                running = Process.Start(start) ?? throw new IOException("Previous release did not start.");
                await Task.Delay(1500);
                if (running.HasExited) throw new IOException("Previous release fixture exited during startup.");
            }
            else
            {
                running = StartProbe(target, nonce, wait: true);
                if (await ReadLine(running) != nonce) throw new IOException("Controlled original did not start.");
            }
            var prepared = installer.Prepare(target, package, release, installed, SelfUpdateWorker.Architecture);
            if (running.HasExited || Hash(target) != newHash || Hash(prepared.BackupPath) != oldHash)
                throw new IOException("Running-original rename or replacement hash verification failed.");
            // No probe has been launched from the replacement yet: choosing Later is passive.
            var oldStillRunningWhenRestartDeclined = !running.HasExited;
            var restartedNonce = Guid.NewGuid().ToString("N");
            using (var restarted = StartProbe(target, restartedNonce, wait: false))
            {
                if (await ReadLine(restarted) != restartedNonce) throw new IOException("Authorized replacement probe failed.");
                await restarted.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                if (restarted.ExitCode != 0) throw new IOException("Replacement probe exited with failure.");
            }
            if (crossVersion) running.Kill(); // Only the PID created for this isolated --demo fixture.
            else { await running.StandardInput.WriteLineAsync("exit"); await running.StandardInput.FlushAsync(); }
            await running.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); running.Dispose(); running = null;
            // Simulate launch failure by holding the replacement against deletion: rollback must preserve both copies and the journal.
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool refused = false;
                try { installer.Rollback(target); } catch (IOException) { refused = true; }
                if (!refused || Hash(target) != newHash || !File.Exists(prepared.BackupPath) || !File.Exists(target + SelfUpdateInstaller.JournalSuffix))
                    throw new IOException("Locked replacement rollback did not retain recoverable files.");
            }
            installer.Rollback(target);
            if (Hash(target) != oldHash || File.Exists(prepared.BackupPath)) throw new IOException("Rollback did not restore exact original bytes.");
            installer.Prepare(target, package, release, installed, SelfUpdateWorker.Architecture);
            var launchRefused = false;
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                try { using var unexpected = StartProbe(target, Guid.NewGuid().ToString("N"), false); unexpected.Kill(); }
                catch (System.ComponentModel.Win32Exception) { launchRefused = true; }
            }
            if (!launchRefused) throw new IOException("Locked replacement unexpectedly launched.");
            installer.Rollback(target);
            if (Hash(target) != oldHash) throw new IOException("Launch-failure rollback did not restore original.");
            // A locked original must fail before replacement and leave the original intact.
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool refused = false;
                try { installer.Prepare(target, package, release, installed, SelfUpdateWorker.Architecture); } catch (IOException) { refused = true; }
                if (!refused || Hash(target) != oldHash) throw new IOException("Locked original was not safely preserved.");
            }
            // Recreate the crash boundary between old->backup and new->original.
            var journal = new SelfUpdateJournal(oldHash, newHash, version.ToString(), "Prepared");
            files.WriteTextAtomic(target + SelfUpdateInstaller.JournalSuffix, JsonSerializer.Serialize(journal));
            files.CopyNew(package, target + SelfUpdateInstaller.StageSuffix); files.Move(target, target + SelfUpdateInstaller.BackupSuffix);
            installer.Recover(target);
            if (Hash(target) != oldHash) throw new IOException("Crash recovery failed.");
            File.WriteAllText("self-update-result.json", JsonSerializer.Serialize(new
            {
                passed = true, nativeWindowsFileOperations = true, sameDirectoryRunningExeRename = true,
                originalNamePreserved = true, oldStillRunningWhenRestartDeclined, authorizedNonceProbe = true,
                packageHash = newHash, exactOriginalRollback = true, lockedOriginal = true, lockedReplacement = true, launchFailureRollback = true,
                crashJournalRecovery = true, pathsWithSpacesAndUnicode = true, noWindowsFeatureWrites = true,
                crossVersion, previousVersion = installed.ToString(), replacementVersion = version.ToString(), previousHash = oldHash,
                sameBuildCopies = !crossVersion, installedVersionMetadataSimulated = !crossVersion,
                trustedNetworkMetadataAndUacNotExercised = true
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (running is not null) { try { running.Kill(); await running.WaitForExitAsync(); } catch { } running.Dispose(); }
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }
    static Process StartProbe(string path, string nonce, bool wait)
    {
        var info = new ProcessStartInfo(path) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        info.ArgumentList.Add("--update-probe"); info.ArgumentList.Add(nonce); if (wait) info.ArgumentList.Add("--wait");
        return Process.Start(info) ?? throw new IOException("Probe did not start.");
    }
    static async Task<string?> ReadLine(Process process) => await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
    static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    sealed class FixtureFiles(string target, string originalHash, Version installed) : PhysicalUpdateFiles
    {
        public override Version VersionOf(string path) => path == target && Hash(path) == originalHash ? installed : base.VersionOf(path);
    }
}
