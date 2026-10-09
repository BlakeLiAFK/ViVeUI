using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ViVeUI.Core;

public interface IUpdateFileSystem
{
    bool Exists(string path);
    Stream OpenRead(string path);
    Version VersionOf(string path);
    void CopyNew(string source, string target);
    void Move(string source, string target);
    void Delete(string path);
    string ReadText(string path);
    void WriteTextAtomic(string path, string text);
    IDisposable Lock(string path);
}

public sealed record PreparedSelfUpdate(string TargetPath, string BackupPath, string Sha256, Version Version);
public sealed record SelfUpdateJournal(string OldHash, string NewHash, string Version, string Phase);

/// <summary>A journaled same-directory swap. The host supplies trusted, fixed paths and owns restart consent.</summary>
public sealed class SelfUpdateInstaller(IUpdateFileSystem files)
{
    readonly IUpdateFileSystem files = files ?? throw new ArgumentNullException(nameof(files));
    public const string BackupSuffix = ".viveui-backup.exe";
    public const string StageSuffix = ".viveui-new.exe";
    public const string JournalSuffix = ".viveui-update.json";
    public const string LockSuffix = ".viveui-update.lock";
    static Version Normalized(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    public PreparedSelfUpdate Prepare(string target, string package, AppRelease release, Version installed, string architecture)
    {
        target = FullTarget(target); package = Path.GetFullPath(package);
        ValidateRelease(release, installed, architecture);
        if (string.Equals(target, package, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update package cannot be the running executable.");
        using var gate = files.Lock(target + LockSuffix);
        var recovered = RecoverLocked(target);
        if (recovered is not null)
        {
            if (!recovered.Sha256.Equals(release.Asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A different update is already prepared. Restart before updating again.");
            return recovered;
        }
        if (!files.Exists(target) || Normalized(files.VersionOf(target)) != Normalized(installed))
            throw new InvalidDataException("Installed executable version changed. Restart and check again.");
        var stage = target + StageSuffix; var backup = target + BackupSuffix;
        if (files.Exists(stage) || files.Exists(backup)) throw new IOException("An unrecognized update file already exists; no files were replaced.");
        ValidatePackage(package, release, architecture);
        var journal = new SelfUpdateJournal(Hash(target), release.Asset.Sha256.ToUpperInvariant(), release.Version.ToString(), "Prepared");
        // Journal precedes even the temporary copy so an interrupted copy can be recovered.
        Save(target, journal);
        try
        {
            files.CopyNew(package, stage);
            ValidatePackage(stage, release, architecture);
            if (Hash(target) != journal.OldHash) throw new IOException("Installed executable changed during update preparation.");
            files.Move(target, backup);
            files.Move(stage, target);
            if (Hash(target) != journal.NewHash) throw new IOException("Replacement verification failed.");
            Save(target, journal with { Phase = "Installed" });
            return Result(target, journal);
        }
        catch (Exception failure)
        {
            try { RollbackLocked(target, journal); }
            catch (Exception rollback) { throw new AggregateException("Update failed and automatic recovery could not finish. Keep the backup and journal for recovery.", failure, rollback); }
            throw;
        }
    }

    public PreparedSelfUpdate? Recover(string target)
    {
        target = FullTarget(target); using var gate = files.Lock(target + LockSuffix);
        return RecoverLocked(target);
    }
    public void Rollback(string target)
    {
        target = FullTarget(target); using var gate = files.Lock(target + LockSuffix);
        if (files.Exists(target + JournalSuffix)) RollbackLocked(target, Journal(target));
    }
    /// <summary>Only call in a new process whose version/hash matches the prepared executable.</summary>
    public void Complete(string target, Version runningVersion)
    {
        target = FullTarget(target); using var gate = files.Lock(target + LockSuffix);
        if (!files.Exists(target + JournalSuffix)) return;
        var journal = Journal(target);
        if (journal.Phase != "Installed" || !files.Exists(target) || Hash(target) != journal.NewHash ||
            Normalized(runningVersion) != Normalized(Version.Parse(journal.Version)))
            throw new InvalidDataException("Running version does not match the prepared update.");
        if (files.Exists(target + BackupSuffix))
        {
            RequireHash(target + BackupSuffix, journal.OldHash);
            files.Delete(target + BackupSuffix); // A still-running old process may prevent cleanup; retry next startup.
        }
        DeleteStage(target); files.Delete(target + JournalSuffix);
    }
    PreparedSelfUpdate? RecoverLocked(string target)
    {
        if (!files.Exists(target + JournalSuffix)) return null;
        var journal = Journal(target);
        if (files.Exists(target) && Hash(target) == journal.NewHash && files.Exists(target + BackupSuffix))
        {
            RequireHash(target + BackupSuffix, journal.OldHash);
            Save(target, journal with { Phase = "Installed" });
            DeleteStage(target); return Result(target, journal);
        }
        // Completed cleanup may have removed the backup immediately before a crash.
        if (journal.Phase == "Installed" && files.Exists(target) && Hash(target) == journal.NewHash && !files.Exists(target + BackupSuffix))
        { DeleteStage(target); files.Delete(target + JournalSuffix); return null; }
        RollbackLocked(target, journal); return null;
    }
    void RollbackLocked(string target, SelfUpdateJournal journal)
    {
        var backup = target + BackupSuffix;
        if (files.Exists(backup))
        {
            RequireHash(backup, journal.OldHash);
            if (files.Exists(target))
            {
                RequireHash(target, journal.NewHash);
                // Keep both complete executables until the restored original has its final name.
                DeleteStage(target); files.Move(target, target + StageSuffix);
            }
            files.Move(backup, target);
        }
        else RequireHash(target, journal.OldHash);
        RequireHash(target, journal.OldHash);
        DeleteStage(target); files.Delete(target + JournalSuffix);
    }
    void DeleteStage(string target) { if (files.Exists(target + StageSuffix)) files.Delete(target + StageSuffix); }
    SelfUpdateJournal Journal(string target)
    {
        var text = files.ReadText(target + JournalSuffix);
        if (text.Length > 4096) throw new InvalidDataException("Invalid update recovery journal.");
        var journal = JsonSerializer.Deserialize<SelfUpdateJournal>(text) ?? throw new InvalidDataException("Empty update journal.");
        if (!Regex.IsMatch(journal.OldHash ?? "", "^[A-Fa-f0-9]{64}$") || !Regex.IsMatch(journal.NewHash ?? "", "^[A-Fa-f0-9]{64}$") ||
            !Version.TryParse(journal.Version, out _) || journal.Phase is not ("Prepared" or "Installed"))
            throw new InvalidDataException("Invalid update recovery journal.");
        return journal with { OldHash = journal.OldHash!.ToUpperInvariant(), NewHash = journal.NewHash!.ToUpperInvariant() };
    }
    void Save(string target, SelfUpdateJournal journal) => files.WriteTextAtomic(target + JournalSuffix, JsonSerializer.Serialize(journal));
    static PreparedSelfUpdate Result(string target, SelfUpdateJournal journal) => new(target, target + BackupSuffix, journal.NewHash, Version.Parse(journal.Version));
    void RequireHash(string path, string expected)
    { if (!files.Exists(path) || Hash(path) != expected) throw new IOException("Update recovery found an unexpected file; it was not overwritten."); }
    string Hash(string path) { using var stream = files.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    static string FullTarget(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update target must be an executable.");
        return full;
    }
    public static void ValidateRelease(AppRelease release, Version installed, string architecture)
    {
        if (architecture is not ("x64" or "arm64") || Normalized(release.Version) <= Normalized(installed))
            throw new InvalidDataException("Update must be a newer supported version.");
        var page = release.Page; var asset = release.Asset;
        var prefix = $"/{UpdateService.Repository}/releases/tag/";
        if (page.Scheme != "https" || page.Host != "github.com" || !page.IsDefaultPort || page.UserInfo.Length != 0 || page.Query.Length != 0 || page.Fragment.Length != 0 ||
            !page.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Untrusted release source.");
        var tag = page.AbsolutePath[prefix.Length..];
        if (!Version.TryParse(tag.TrimStart('v'), out var tagged) || Normalized(tagged) != Normalized(release.Version)) throw new InvalidDataException("Release version does not match its tag.");
        var expectedName = $"ViVeUI-win-{architecture}.exe";
        var expectedUrl = $"https://github.com/{UpdateService.Repository}/releases/download/{tag}/{expectedName}";
        if (asset.Name != expectedName || asset.Url.AbsoluteUri != expectedUrl || !Regex.IsMatch(asset.Sha256, "^[a-fA-F0-9]{64}$") || asset.Size is <= 0 or > 250_000_000)
            throw new InvalidDataException("Untrusted update package metadata.");
    }
    void ValidatePackage(string path, AppRelease release, string architecture)
    {
        using (var stream = files.OpenRead(path))
        {
            if (stream.Length != release.Asset.Size || !Convert.ToHexString(SHA256.HashData(stream)).Equals(release.Asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Update package hash or size mismatch.");
            stream.Position = 0;
            using var pe = new PEReader(stream);
            var expected = architecture == "arm64" ? Machine.Arm64 : Machine.Amd64;
            if (pe.PEHeaders.PEHeader is null || pe.PEHeaders.CoffHeader.Machine != expected ||
                !pe.PEHeaders.CoffHeader.Characteristics.HasFlag(Characteristics.ExecutableImage) || pe.PEHeaders.CoffHeader.Characteristics.HasFlag(Characteristics.Dll))
                throw new InvalidDataException("Update package architecture or executable type mismatch.");
        }
        if (Normalized(files.VersionOf(path)) != Normalized(release.Version)) throw new InvalidDataException("Update executable version does not match the release.");
    }
}
