using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ViVeUI.Windows;
// ACL authorizes the same user SID across integrity levels. Do NOT use CurrentUserOnly:
// on Windows it additionally compares elevation and rejects the intended UAC boundary.
internal static class WorkerChannel
{
    public static string NewName() => "ViVeUI-" + Guid.NewGuid().ToString("N");
    public static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static NamedPipeServerStream Server(string name)
    {
        ValidateName(name);
        var sid = WindowsIdentity.GetCurrent().User ?? throw new IOException("No Windows identity.");
        var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
        security.SetOwner(sid);
        security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security);
    }
    public static NamedPipeClientStream Client(string name)
    {
        ValidateName(name);
        return new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
    }
    static void ValidateName(string name)
    { if (!name.StartsWith("ViVeUI-", StringComparison.Ordinal) || !Guid.TryParseExact(name[7..], "N", out _)) throw new InvalidDataException("Invalid pipe name."); }
    public static void VerifyPeer(PipeStream pipe, int expectedPid, bool server)
    {
        uint pid; bool ok = server ? GetNamedPipeClientProcessId(pipe.SafePipeHandle, out pid) : GetNamedPipeServerProcessId(pipe.SafePipeHandle, out pid);
        if (!ok || pid != expectedPid) throw new UnauthorizedAccessException("Unexpected IPC process identity.");
        using var handle = OpenProcess(0x1000, false, (int)pid);
        var path = new StringBuilder(32768); var length = path.Capacity;
        if (handle.IsInvalid || !QueryFullProcessImageName(handle, 0, path, ref length) || !Path.GetFullPath(path.ToString()).Equals(Path.GetFullPath(Environment.ProcessPath!), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Unexpected IPC executable.");
    }
    public static async Task AuthenticateServer(PipeStream pipe, int clientPid, string secret, CancellationToken token)
    {
        VerifyPeer(pipe, clientPid, true);
        var proof = await Read(pipe, token);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(proof), Encoding.UTF8.GetBytes(secret))) throw new UnauthorizedAccessException("IPC authentication failed.");
        await Write(pipe, "ViVeUI/1:" + secret, token);
    }
    public static async Task AuthenticateClient(PipeStream pipe, int serverPid, string secret, CancellationToken token)
    {
        VerifyPeer(pipe, serverPid, false);
        await Write(pipe, secret, token);
        if (await Read(pipe, token) != "ViVeUI/1:" + secret) throw new UnauthorizedAccessException("IPC server authentication failed.");
    }
    // Length-framed and bounded: no unbounded ReadLine allocation from IPC input.
    public static async Task Write(Stream pipe, string value, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(value); if (bytes.Length > 100_000) throw new InvalidDataException("IPC request too large.");
        await pipe.WriteAsync(BitConverter.GetBytes(bytes.Length), token); await pipe.WriteAsync(bytes, token); await pipe.FlushAsync(token);
    }
    public static async Task<string> Read(Stream pipe, CancellationToken token)
    {
        var header = new byte[4]; await pipe.ReadExactlyAsync(header, token); var length = BitConverter.ToInt32(header);
        if (length is < 1 or > 100_000) throw new InvalidDataException("Invalid IPC frame size.");
        var data = new byte[length]; await pipe.ReadExactlyAsync(data, token); return Encoding.UTF8.GetString(data);
    }
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll", SetLastError=true)] static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder path, ref int length);
}
