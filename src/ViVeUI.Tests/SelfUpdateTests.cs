using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ViVeUI.Core;

public static class SelfUpdateTests
{
    static readonly Version OldVersion = new(1, 0, 0, 0), NewVersion = new(2, 0, 0, 0);
    static readonly string Target = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "应用 with spaces", "ViVeUI.exe"));
    static readonly string Package = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "download ü.exe"));
    static void Assert(bool condition) { if (!condition) throw new Exception("Self-update assertion failed."); }
    static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    static byte[] Image(ushort machine = 0x8664, byte marker = 1)
    {
        var bytes = new byte[1024]; bytes[0] = 0x4d; bytes[1] = 0x5a;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c), 128);
        bytes[128] = (byte)'P'; bytes[129] = (byte)'E';
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(132), machine);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(148), 240);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(150), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(152), 0x20b);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(184), 4096);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(188), 512);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(208), 4096);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(212), 512);
        bytes[900] = marker; return bytes;
    }
    static AppRelease Release(byte[] image) => new(NewVersion, new("https://github.com/BlakeLiAFK/ViVeUI/releases/tag/v2.0.0.0"),
        new("ViVeUI-win-x64.exe", new("https://github.com/BlakeLiAFK/ViVeUI/releases/download/v2.0.0.0/ViVeUI-win-x64.exe"), Hash(image), image.Length));
    static (MemoryFiles Fs, SelfUpdateInstaller Installer, AppRelease Release) Fixture()
    {
        var fs = new MemoryFiles(); fs.Put(Target, Image(marker: 1), OldVersion); fs.Put(Package, Image(marker: 2), NewVersion);
        return (fs, new(fs), Release(fs.Data[Package].Bytes));
    }
    static void Prepare((MemoryFiles Fs, SelfUpdateInstaller Installer, AppRelease Release) f) => f.Installer.Prepare(Target, Package, f.Release, OldVersion, "x64");
    public static int Run()
    {
        int passed = 0;
        void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS Self update: " + name); } catch (Exception error) { throw new Exception(name, error); } }
        Test("Verified same-directory swap preserves backup and never launches", () =>
        {
            var f = Fixture(); var original = f.Fs.Data[Target].Bytes; Prepare(f);
            Assert(Hash(f.Fs.Data[Target].Bytes) == f.Release.Asset.Sha256 && f.Fs.Data[Target + SelfUpdateInstaller.BackupSuffix].Bytes.SequenceEqual(original));
            Assert(!f.Fs.Exists(Target + SelfUpdateInstaller.StageSuffix) && f.Installer.Recover(Target)?.Version == NewVersion);
        });
        Test("Duplicate prepare is idempotent; a different prepared release is refused", () =>
        {
            var f = Fixture(); Prepare(f); int moves = f.Fs.Moves; Prepare(f); Assert(f.Fs.Moves == moves);
            var other = f.Release with { Asset = f.Release.Asset with { Sha256 = new string('A', 64) } };
            Throws<InvalidOperationException>(() => f.Installer.Prepare(Target, Package, other, OldVersion, "x64"));
        });
        Test("Untrusted metadata, version and architecture are refused before filesystem writes", () =>
        {
            var f = Fixture();
            foreach (var release in new[] {
                f.Release with { Page = new("https://evil.example/releases/tag/v2.0.0.0") },
                f.Release with { Page = new("https://github.com/BlakeLiAFK/ViVeUI/releases/tag/v9.0") },
                f.Release with { Asset = f.Release.Asset with { Url = new("https://github.com/attacker/app/releases/download/v2.0.0.0/ViVeUI-win-x64.exe") } },
                f.Release with { Asset = f.Release.Asset with { Name = "../other.exe" } },
                f.Release with { Asset = f.Release.Asset with { Sha256 = "bad" } },
                f.Release with { Version = OldVersion } })
                Throws<InvalidDataException>(() => f.Installer.Prepare(Target, Package, release, OldVersion, "x64"));
            Throws<InvalidDataException>(() => f.Installer.Prepare(Target, Package, f.Release, OldVersion, "x86"));
            Assert(f.Fs.Moves == 0 && f.Fs.Data.Count == 2);
        });
        Test("Hash, size, PE machine, DLL, malformed image and embedded version fail closed", () =>
        {
            foreach (var mode in Enumerable.Range(0, 6))
            {
                var f = Fixture(); var image = f.Fs.Data[Package].Bytes.ToArray(); var release = f.Release;
                if (mode == 0) image[950] ^= 1;
                if (mode == 1) release = release with { Asset = release.Asset with { Size = image.Length + 1 } };
                if (mode == 2) { image = Image(0xaa64, 2); release = Release(image); }
                if (mode == 3) { image[151] = 0x20; release = Release(image); }
                if (mode == 4) { image = new byte[1024]; release = Release(image); }
                f.Fs.Put(Package, image, mode == 5 ? OldVersion : NewVersion);
                Throws<Exception>(() => f.Installer.Prepare(Target, Package, release, OldVersion, "x64"));
                Assert(f.Fs.Moves == 0 && !f.Fs.Exists(Target + SelfUpdateInstaller.JournalSuffix));
            }
        });
        Test("Second rename failure restores original", () =>
        {
            var f = Fixture(); f.Fs.FailMove = (_, destination) => destination == Target && f.Fs.Moves == 2;
            Throws<IOException>(() => Prepare(f));
            Assert(f.Fs.VersionOf(Target) == OldVersion && !f.Fs.Exists(Target + SelfUpdateInstaller.BackupSuffix) && !f.Fs.Exists(Target + SelfUpdateInstaller.JournalSuffix));
        });
        Test("Locked running executable leaves original untouched", () =>
        {
            var f = Fixture(); f.Fs.FailMove = (source, _) => source == Target;
            Throws<IOException>(() => Prepare(f));
            Assert(f.Fs.VersionOf(Target) == OldVersion && !f.Fs.Exists(Target + SelfUpdateInstaller.StageSuffix));
        });
        Test("Copy failure and disk-full simulation preserve original", () =>
        {
            var f = Fixture(); f.Fs.FailCopy = true; Throws<IOException>(() => Prepare(f));
            Assert(f.Fs.VersionOf(Target) == OldVersion && !f.Fs.Exists(Target + SelfUpdateInstaller.JournalSuffix));
        });
        Test("Failed launch rollback restores exact original bytes", () =>
        {
            var f = Fixture(); var original = Hash(f.Fs.Data[Target].Bytes); Prepare(f); f.Installer.Rollback(Target);
            Assert(Hash(f.Fs.Data[Target].Bytes) == original && !f.Fs.Exists(Target + SelfUpdateInstaller.JournalSuffix));
        });
        Test("Crash after moving original recovers backup", () =>
        {
            var f = Fixture(); var old = Hash(f.Fs.Data[Target].Bytes);
            f.Fs.WriteTextAtomic(Target + SelfUpdateInstaller.JournalSuffix, JsonSerializer.Serialize(new SelfUpdateJournal(old, f.Release.Asset.Sha256, NewVersion.ToString(), "Prepared")));
            f.Fs.CopyNew(Package, Target + SelfUpdateInstaller.StageSuffix); f.Fs.Move(Target, Target + SelfUpdateInstaller.BackupSuffix);
            Assert(f.Installer.Recover(Target) is null && Hash(f.Fs.Data[Target].Bytes) == old && !f.Fs.Exists(Target + SelfUpdateInstaller.StageSuffix));
        });
        Test("Crash after replacing original retains verified prepared update", () =>
        {
            var f = Fixture(); Prepare(f);
            var path = Target + SelfUpdateInstaller.JournalSuffix; var journal = JsonSerializer.Deserialize<SelfUpdateJournal>(f.Fs.ReadText(path))!;
            f.Fs.WriteTextAtomic(path, JsonSerializer.Serialize(journal with { Phase = "Prepared" }));
            Assert(f.Installer.Recover(Target)?.Sha256 == f.Release.Asset.Sha256 && f.Fs.Exists(Target + SelfUpdateInstaller.BackupSuffix));
        });
        Test("Recovery never overwrites unexpected target or backup", () =>
        {
            var f = Fixture(); Prepare(f); f.Fs.Put(Target, new byte[] { 7 }, NewVersion);
            Throws<IOException>(() => f.Installer.Rollback(Target)); Assert(f.Fs.Data[Target].Bytes.SequenceEqual(new byte[] { 7 }));
            Assert(f.Fs.Exists(Target + SelfUpdateInstaller.BackupSuffix) && f.Fs.Exists(Target + SelfUpdateInstaller.JournalSuffix));
        });
        Test("Rollback interruption retains recovery journal", () =>
        {
            var f = Fixture(); Prepare(f); f.Fs.FailMove = (source, _) => source.EndsWith(SelfUpdateInstaller.BackupSuffix);
            Throws<IOException>(() => f.Installer.Rollback(Target)); Assert(f.Fs.Exists(Target + SelfUpdateInstaller.JournalSuffix));
            f.Fs.FailMove = null; f.Installer.Recover(Target); Assert(f.Fs.VersionOf(Target) == OldVersion);
        });
        Test("Exclusive installation lock rejects simultaneous update", () =>
        {
            var f = Fixture(); using var gate = f.Fs.Lock(Target + SelfUpdateInstaller.LockSuffix);
            Throws<IOException>(() => Prepare(f)); Assert(f.Fs.Moves == 0);
        });
        Test("Only the updated process can complete cleanup", () =>
        {
            var f = Fixture(); Prepare(f); Throws<InvalidDataException>(() => f.Installer.Complete(Target, OldVersion));
            f.Installer.Complete(Target, NewVersion);
            Assert(f.Fs.VersionOf(Target) == NewVersion && !f.Fs.Exists(Target + SelfUpdateInstaller.BackupSuffix) && !f.Fs.Exists(Target + SelfUpdateInstaller.JournalSuffix));
        });
        return passed;
    }
    sealed record Entry(byte[] Bytes, Version Version);
    sealed class MemoryFiles : IUpdateFileSystem
    {
        public readonly Dictionary<string, Entry> Data = new();
        public int Moves; public bool FailCopy; public Func<string, string, bool>? FailMove;
        readonly HashSet<string> locks = new();
        public void Put(string path, byte[] bytes, Version version) => Data[path] = new(bytes, version);
        public bool Exists(string path) => Data.ContainsKey(path);
        public Stream OpenRead(string path) => new MemoryStream(Data[path].Bytes, false);
        public Version VersionOf(string path) => Data[path].Version;
        public void CopyNew(string source, string target) { if (FailCopy) throw new IOException("Disk full"); Data.Add(target, Data[source] with { Bytes = Data[source].Bytes.ToArray() }); }
        public void Move(string source, string target) { Moves++; if (FailMove?.Invoke(source, target) == true) throw new IOException("Locked"); Data.Add(target, Data[source]); Data.Remove(source); }
        public void Delete(string path) => Data.Remove(path);
        public string ReadText(string path) => Encoding.UTF8.GetString(Data[path].Bytes);
        public void WriteTextAtomic(string path, string text) => Put(path, Encoding.UTF8.GetBytes(text), OldVersion);
        public IDisposable Lock(string path) { if (!locks.Add(path)) throw new IOException("Update busy"); return new Unlock(() => locks.Remove(path)); }
        sealed class Unlock(Action release) : IDisposable { public void Dispose() => release(); }
    }
}
