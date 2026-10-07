using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace ViVeUI.Windows;
// Handshake-only diagnostics. Never constructs WindowsStore or calls ViVe APIs.
internal static class IpcSmoke
{
    public static void Run(string[] args)
    {
        try
        {
            if (args[0] == "--ipc-probe-server" && args.Length == 4) Server(args[1], int.Parse(args[2]), args[3]).GetAwaiter().GetResult();
            else if (args[0] == "--ipc-smoke") Probe().GetAwaiter().GetResult();
            else throw new InvalidDataException("Invalid IPC test arguments.");
        }
        catch (Exception e) { File.WriteAllText("ipc-error-" + Environment.ProcessId + ".txt", e.ToString()); Environment.ExitCode = 1; }
    }
    static async Task Server(string name, int expectedClient, string secret)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var pipe = WorkerChannel.Server(name);
        await pipe.WaitForConnectionAsync(timeout.Token);
        await WorkerChannel.AuthenticateServer(pipe, expectedClient, secret, timeout.Token);
        var message = await WorkerChannel.Read(pipe, timeout.Token);
        if (message != "probe-only") throw new InvalidDataException("Probe does not accept changes.");
        await WorkerChannel.Write(pipe, JsonSerializer.Serialize(new { integrity = Integrity(), elevated = Elevated(), administrator = Administrator(), restricted = Restricted(), noFeatureWrites = true }), timeout.Token);
        if (await WorkerChannel.Read(pipe, timeout.Token) != "done") throw new InvalidDataException("Missing test acknowledgement.");
    }
    static async Task Probe()
    {
        var name = WorkerChannel.NewName(); var secret = WorkerChannel.NewSecret();
        var arguments = $"--ipc-probe-server {name} {Environment.ProcessId} {secret}";
        using var child = MediumChild(arguments);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var pipe = WorkerChannel.Client(name);
        await pipe.ConnectAsync(timeout.Token);
        await WorkerChannel.AuthenticateClient(pipe, child.Id, secret, timeout.Token);
        // A wrong expected process ID must fail without accepting any request.
        try { WorkerChannel.VerifyPeer(pipe, -1, false); throw new Exception("Wrong peer accepted"); }
        catch (UnauthorizedAccessException) { }
        await WorkerChannel.Write(pipe, "probe-only", timeout.Token);
        var report = await WorkerChannel.Read(pipe, timeout.Token);
        using var parsed = JsonDocument.Parse(report);
        if (Integrity() < 0x3000 || !Elevated() || !Administrator() || parsed.RootElement.GetProperty("integrity").GetInt32() != 0x2000 || parsed.RootElement.GetProperty("administrator").GetBoolean() || (!parsed.RootElement.GetProperty("restricted").GetBoolean() && parsed.RootElement.GetProperty("elevated").GetBoolean()))
            throw new InvalidOperationException("Test did not establish high-admin to medium non-admin IPC: " + report);
        await WorkerChannel.Write(pipe, "done", timeout.Token);
        await child.WaitForExitAsync(timeout.Token); if (child.ExitCode != 0) throw new IOException("Probe server failed.");
        File.WriteAllText("ipc-result.json", JsonSerializer.Serialize(new { passed = true, server = JsonSerializer.Deserialize<JsonElement>(report), client = new { integrity = Integrity(), elevated = Elevated(), administrator = Administrator() }, authentication = "user SID ACL + both peer PIDs/executable + random challenge", rejectedWrongPeer = true, noFeatureWrites = true }));
    }
    // On UAC-disabled hosted runners a restricted token retains TokenElevation=true.
    // Effective administrator membership and mandatory integrity establish the boundary.
    static bool Administrator() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    static bool Restricted()
    {
        using var identity = WindowsIdentity.GetCurrent(); var memory = Marshal.AllocHGlobal(4);
        try { Check(GetTokenInformation(identity.Token, 21, memory, 4, out _)); return Marshal.ReadInt32(memory) != 0; }
        finally { Marshal.FreeHGlobal(memory); }
    }
    static bool Elevated()
    {
        using var identity = WindowsIdentity.GetCurrent(); var memory = Marshal.AllocHGlobal(4);
        try { Check(GetTokenInformation(identity.Token, 20, memory, 4, out _)); return Marshal.ReadInt32(memory) != 0; }
        finally { Marshal.FreeHGlobal(memory); }
    }
    static int Integrity()
    {
        using var identity = WindowsIdentity.GetCurrent(); GetTokenInformation(identity.Token, 25, IntPtr.Zero, 0, out var length);
        var memory = Marshal.AllocHGlobal(length);
        try { Check(GetTokenInformation(identity.Token, 25, memory, length, out _)); var sid = Marshal.ReadIntPtr(memory); var count = Marshal.ReadByte(GetSidSubAuthorityCount(sid)); return Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1))); }
        finally { Marshal.FreeHGlobal(memory); }
    }
    static Process MediumChild(string args)
    {
        using var identity = WindowsIdentity.GetCurrent();
        IntPtr baseToken = IntPtr.Zero; var linked = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            if (GetTokenInformation(identity.Token, 19, linked, IntPtr.Size, out _)) baseToken = Marshal.ReadIntPtr(linked);
            else
            {
                Check(ConvertStringSidToSid("S-1-5-32-544", out var adminSid));
                try { var deny = new SidAndAttributes { Sid = adminSid }; Check(CreateRestrictedToken(identity.Token, 1, 1, ref deny, 0, IntPtr.Zero, 0, IntPtr.Zero, out baseToken)); }
                finally { LocalFree(adminSid); }
            }
            Check(DuplicateTokenEx(baseToken, 0xF01FF, IntPtr.Zero, 2, 1, out var token));
            try
            {
                Check(ConvertStringSidToSid("S-1-16-8192", out var sid));
                try
                {
                    var length = Marshal.SizeOf<SidAndAttributes>() + (int)GetLengthSid(sid);
                    var data = Marshal.AllocHGlobal(length);
                    try { var label = new SidAndAttributes { Sid = data + Marshal.SizeOf<SidAndAttributes>(), Attributes = 0x20 }; Check(CopySid(GetLengthSid(sid), label.Sid, sid)); Marshal.StructureToPtr(label, data, false); Check(SetTokenInformation(token, 25, data, length)); }
                    finally { Marshal.FreeHGlobal(data); }
                }
                finally { LocalFree(sid); }
                var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
                var command = new StringBuilder('"' + Environment.ProcessPath! + "\" " + args);
                Check(CreateProcessWithTokenW(token, 0, Environment.ProcessPath!, command, 0, IntPtr.Zero, Environment.CurrentDirectory, ref startup, out var info));
                CloseHandle(info.Thread); CloseHandle(info.Process); return Process.GetProcessById(info.ProcessId);
            }
            finally { CloseHandle(token); }
        }
        finally { if (baseToken != IntPtr.Zero) CloseHandle(baseToken); Marshal.FreeHGlobal(linked); }
    }
    static void Check(bool result) { if (!result) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    [StructLayout(LayoutKind.Sequential)] struct SidAndAttributes { public IntPtr Sid; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] struct StartupInfo { public int cb; public string? reserved, desktop, title; public int x,y,width,height,xChars,yChars,fill,flags; public short show,reserved2; public IntPtr data,input,output,error; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr Process,Thread; public int ProcessId,ThreadId; }
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int kind,IntPtr data,int length,out int needed);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool SetTokenInformation(IntPtr token,int kind,IntPtr data,int length);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool DuplicateTokenEx(IntPtr existing,uint access,IntPtr attributes,int level,int type,out IntPtr token);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool CreateRestrictedToken(IntPtr existing,uint flags,uint disabled,ref SidAndAttributes sid,uint privileges,IntPtr privilegeList,uint restricted,IntPtr restrictedList,out IntPtr token);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool ConvertStringSidToSid(string value,out IntPtr sid);
    [DllImport("advapi32.dll")] static extern uint GetLengthSid(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid,uint index);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool CopySid(uint size,IntPtr target,IntPtr source);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool CreateProcessWithTokenW(IntPtr token,uint logon,string app,StringBuilder command,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr handle);
}
