using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;

namespace BluePrinceExternalReader
{
    internal sealed class MemoryReader : IDisposable
    {
        // ── Win32 imports ────────────────────────────────────────────────────
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool   CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] static extern bool   ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, int size, out int read);
        [DllImport("kernel32.dll")] static extern int    VirtualQueryEx(IntPtr h, IntPtr addr, out MEMORY_BASIC_INFORMATION mbi, uint len);

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint   AllocationProtect;
            public uint   __alignment1;
            public ulong  RegionSize;
            public uint   State;
            public uint   Protect;
            public uint   Type;
            public uint   __alignment2;
        }

        const int    PROCESS_VM_READ    = 0x0010;
        const int    PROCESS_QUERY_INFO = 0x0400;
        const uint   MEM_COMMIT         = 0x1000;
        // any protect that allows reading (READONLY, READWRITE, EXECUTE_READ, EXECUTE_READWRITE)
        const uint   PAGE_READABLE      = 0x02 | 0x04 | 0x20 | 0x40;

        private IntPtr _handle;
        public  IntPtr ModuleBase { get; private set; }
        public  bool   IsOpen     => _handle != IntPtr.Zero;

        // ── Attach ───────────────────────────────────────────────────────────
        public static MemoryReader? TryAttach(string processName, string moduleName)
        {
            var procs = Process.GetProcessesByName(processName);
            if (procs.Length == 0) return null;

            var handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFO, false, procs[0].Id);
            if (handle == IntPtr.Zero) return null;

            IntPtr modBase = IntPtr.Zero;
            foreach (ProcessModule mod in procs[0].Modules)
            {
                if (mod.ModuleName?.Equals(moduleName, StringComparison.OrdinalIgnoreCase) == true)
                { modBase = mod.BaseAddress; break; }
            }

            if (modBase == IntPtr.Zero) { CloseHandle(handle); return null; }
            return new MemoryReader { _handle = handle, ModuleBase = modBase };
        }

        // ── Primitive reads ──────────────────────────────────────────────────
        public byte[] ReadBytes(IntPtr addr, int count)
        {
            var buf = new byte[count];
            ReadProcessMemory(_handle, addr, buf, count, out _);
            return buf;
        }
        public int    ReadInt32(IntPtr addr)  => BitConverter.ToInt32(ReadBytes(addr, 4), 0);
        public long   ReadInt64(IntPtr addr)  => BitConverter.ToInt64(ReadBytes(addr, 8), 0);
        public float  ReadFloat(IntPtr addr)  => BitConverter.ToSingle(ReadBytes(addr, 4), 0);
        public IntPtr ReadPtr(IntPtr addr)    => (IntPtr)ReadInt64(addr);

        public (int x, int y) ReadVector2Int(IntPtr addr)
            => (ReadInt32(addr), ReadInt32(addr + 4));

        // ── IL2CPP managed string ────────────────────────────────────────────
        public string? ReadManagedString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            int len = ReadInt32(ptr + GameOffsets.STR_Length);
            if (len <= 0 || len > 2048) return null;
            return Encoding.Unicode.GetString(ReadBytes(ptr + GameOffsets.STR_Chars, len * 2));
        }

        // ── IL2CPP 2D array (T[,]) ───────────────────────────────────────────
        // Returns (dim0Length, dim1Length) and lets caller index elements.
        public (int d0, int d1) ReadArray2dDims(IntPtr arrayPtr)
        {
            if (arrayPtr == IntPtr.Zero) return (0, 0);
            var boundsPtr = ReadPtr(arrayPtr + GameOffsets.ARRAY2D_BoundsPtr);
            if (boundsPtr == IntPtr.Zero) return (0, 0);
            // Each Il2CppArrayBounds = {uintptr_t length, intptr_t lower} = 16 bytes
            int d0 = (int)ReadInt64(boundsPtr);
            int d1 = (int)ReadInt64(boundsPtr + GameOffsets.ARRAY2D_BoundsEntrySize);
            return (d0, d1);
        }

        // Read element pointer at [i, j] in a 2D reference-type array
        public IntPtr ReadArray2dElement(IntPtr arrayPtr, int i, int j, int dim1)
            => ReadPtr(arrayPtr + GameOffsets.ARRAY2D_Data + (i * dim1 + j) * 8);

        // ── Memory scan ──────────────────────────────────────────────────────
        // Returns the address of the FIRST occurrence of pattern in process memory,
        // or IntPtr.Zero if not found.
        public IntPtr ScanForPattern(byte[] pattern, long startAddr = 0x10000)
        {
            const int CHUNK = 0x1000000; // 16 MB per read
            long addr = Math.Max(startAddr, 0x10000);
            long end  = 0x7FFFFFFF0000L;

            while (addr < end)
            {
                if (VirtualQueryEx(_handle, (IntPtr)addr, out var mbi,
                        (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                { addr += 0x1000; continue; } // skip unqueryable page, keep scanning

                long regionEnd = addr + (long)mbi.RegionSize;

                if (mbi.State == MEM_COMMIT && (mbi.Protect & PAGE_READABLE) != 0)
                {
                    long scanAddr = addr;
                    while (scanAddr < regionEnd)
                    {
                        int toRead = (int)Math.Min(CHUNK, regionEnd - scanAddr);
                        var buf = new byte[toRead];
                        if (ReadProcessMemory(_handle, (IntPtr)scanAddr, buf, toRead, out int bytesRead) && bytesRead > 0)
                        {
                            int found = IndexOfPattern(buf, bytesRead, pattern);
                            if (found >= 0) return (IntPtr)(scanAddr + found);
                        }
                        scanAddr += toRead;
                    }
                }

                addr = (long)Math.Max(regionEnd, addr + 1);
            }
            return IntPtr.Zero;
        }

        private static int IndexOfPattern(byte[] buf, int len, byte[] pat)
        {
            int limit = len - pat.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < pat.Length; j++)
                {
                    if (buf[i + j] != pat[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero) { CloseHandle(_handle); _handle = IntPtr.Zero; }
        }
    }
}
