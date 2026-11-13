using System.Runtime.InteropServices;

// File type, permissions and ownership through libc: uid/gid and the file type are not in the BCL.
static class Posix
{
    public readonly record struct Stat(uint Mode, uint Uid, uint Gid)
    {
        public UnixFileMode Permissions => (UnixFileMode)(Mode & 0xFFF);
        public EntryKind Kind => (Mode & 0xF000) switch
        {
            0x4000 => EntryKind.Directory, 0xA000 => EntryKind.Symlink, 0x1000 => EntryKind.Fifo,
            0xC000 => EntryKind.Socket, 0x2000 => EntryKind.CharDevice, 0x6000 => EntryKind.BlockDevice,
            _ => EntryKind.File,
        };
    }

    // The parts of struct stat we read, per platform ABI.
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    struct Darwin { [FieldOffset(4)] public ushort Mode; [FieldOffset(16)] public uint Uid; [FieldOffset(20)] public uint Gid; }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    struct LinuxX64 { [FieldOffset(24)] public uint Mode; [FieldOffset(28)] public uint Uid; [FieldOffset(32)] public uint Gid; }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    struct LinuxArm64 { [FieldOffset(16)] public uint Mode; [FieldOffset(24)] public uint Uid; [FieldOffset(28)] public uint Gid; }

    [DllImport("libc", EntryPoint = "lstat")] static extern int lstat(string path, out Darwin st);
    [DllImport("libc", EntryPoint = "lstat$INODE64")] static extern int lstat_inode64(string path, out Darwin st);
    [DllImport("libc", EntryPoint = "lstat")] static extern int lstat(string path, out LinuxX64 st);
    [DllImport("libc", EntryPoint = "lstat")] static extern int lstat(string path, out LinuxArm64 st);
    [DllImport("libc")] static extern IntPtr getpwuid(uint uid);
    [DllImport("libc")] static extern IntPtr getgrgid(uint gid);

    static Func<string, Stat?>? platformLstat = (OperatingSystem.IsMacOS(), OperatingSystem.IsLinux(), RuntimeInformation.ProcessArchitecture) switch
    {
        (true, _, Architecture.Arm64) => p => lstat(p, out Darwin s) == 0 ? new Stat(s.Mode, s.Uid, s.Gid) : null,
        (true, _, Architecture.X64) => p => lstat_inode64(p, out var s) == 0 ? new Stat(s.Mode, s.Uid, s.Gid) : null,
        (_, true, Architecture.X64) => p => lstat(p, out LinuxX64 s) == 0 ? new Stat(s.Mode, s.Uid, s.Gid) : null,
        (_, true, Architecture.Arm64) => p => lstat(p, out LinuxArm64 s) == 0 ? new Stat(s.Mode, s.Uid, s.Gid) : null,
        _ => null,
    };

    /// <summary>Null on platforms without a known struct stat layout, or when the call fails.</summary>
    public static Stat? Lstat(string path)
    {
        try { return platformLstat?.Invoke(path); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            platformLstat = null;   // no usable libc here: stop trying
            return null;
        }
    }

    static readonly Dictionary<uint, string> users = [], groups = [];

    public static string UserName(uint uid) => Name(users, uid, getpwuid);
    public static string GroupName(uint gid) => Name(groups, gid, getgrgid);

    static string Name(Dictionary<uint, string> cache, uint id, Func<uint, IntPtr> lookup)
    {
        if (cache.TryGetValue(id, out var name)) return name;
        var entry = lookup(id);   // struct passwd and struct group both start with a char* name
        name = entry == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(entry));
        return cache[id] = name ?? id.ToString();
    }
}
