using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BluePrinceExternalReader
{
    internal static class SaveReader
    {
        private const string KeyString =
            "D#vnrl%TI_9q0euFPIx+wKRuNx%Aja2-AtuH1jtMSk2k%H1jXjUPor08QaeQE=p5l=" +
            "LAIWaSYms-68SYVS0PPoWxgM1B8?8tirM+UGr=cp!5a3=B5tBsKYEUfqxN!H9DvRVkL" +
            "W?6cMeZWxgov%OOXmfl9zRiqWPsXq95lEc4yax7hqf5m_i5ssn-OGgLA8LJu2ETibBi7" +
            "DwLc-zQ4M9jRGIdV_izS_J_=3FA=rAo0HUiEr-HWYVnuK$OQUyaVMchXxf%EBo3A7Z-PXYm" +
            "$6PPG%fJfWzV7M$L5he#y5cb?kVR67IfGzG$UzBcLhNMDhQFwQSEX59ZG7hP32q?6Pgir" +
            "mvGTd-45+7ZKyG$FrDHoNw7ceUhrxYdzYSHd0yRz0T_RR_R5$GZda%DDfCUPHIaVlIhMq4FE" +
            "Ozo?GL7wyXr9XD7SD_QGpjZh&NDwycjnBeOy2mmFazlOV5eR7jsiwYDde9jCOH&cOxeTody" +
            "=iUEt|l7JCQ8IyX|0g3H&NO6DMveVqC9|OPkOZpO3DpM|||3LJ7PX40rZJXmLILu0UXU" +
            "9hpM5";

        public static readonly string SavePath = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "..", "LocalLow", "Dogubomb", "BLUE PRINCE", "storage", "MtHollyBlueprint.es3"));

        public sealed record SlotData(int Slot, int Day, DateTime LastModified, string Snapshot,
            IReadOnlyDictionary<string, int> Upgrades,
            IReadOnlySet<string> BoolKeys,
            int ContraptionsCount,
            int OuterRoomId = -1);

        public static string DecryptFile(string path)
        {
            var raw  = File.ReadAllBytes(path);
            var salt = raw[..16];
            var ct   = raw[16..];
            byte[] key;
            using (var kdf = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(KeyString), salt, 100, HashAlgorithmName.SHA1))
                key = kdf.GetBytes(16);
            using var aes = Aes.Create();
            aes.Key = key; aes.IV = salt;
            aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            return Encoding.UTF8.GetString(dec.TransformFinalBlock(ct, 0, ct.Length));
        }

        private static readonly Regex SlotRx = new(
            @"""(BluePrint\d*)"".*?""objs""\s*:\s*\{(.*?)\}\s*\},\s*""arrays""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex UpgradeRx = new(
            @"""(Upgrade [^""]+)""\s*:\s*\{\s*""__type""\s*:\s*""System\.Int32""\s*(-?\d+)",
            RegexOptions.Compiled);

        private static readonly Regex BoolFieldRx = new(
            @"""([^""]+)""\s*:\s*\{\s*""__type""\s*:\s*""System\.Boolean""\s*(true|false)",
            RegexOptions.Compiled);

        // Captures any System.Int32 field not already matched by UpgradeRx (e.g. room records).
        private static readonly Regex GeneralInt32Rx = new(
            @"""([^""]+)""\s*:\s*\{\s*""__type""\s*:\s*""System\.Int32""\s*(-?\d+)",
            RegexOptions.Compiled);

        private static readonly Regex DayRx = new(
            @"""DAY""\s*:\s*\{\s*""__type""\s*:\s*""System\.Int32""\s*(\d+)",
            RegexOptions.Compiled);

        // OuterRoom is a RoomID enum (int). ES3 may store it as System.Int32 or the enum type name.
        private static readonly Regex OuterRoomRx = new(
            @"""OuterRoom""\s*:\s*\{\s*""__type""\s*:\s*""[^""]*""\s*(-?\d+)",
            RegexOptions.Compiled);

        public static List<SlotData> ReadAllSlots(string path, bool debugOuter = false)
        {
            var plain = DecryptFile(path);

            // Search full save text for any "outer" key when requested (fires once per save change)
            if (debugOuter)
            {
                int searchPos = 0;
                bool foundAny = false;
                while (true)
                {
                    int idx = plain.IndexOf("outer", searchPos, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) break;
                    foundAny = true;
                    int start = Math.Max(0, idx - 2);
                    int end   = Math.Min(plain.Length, idx + 100);
                    Console.WriteLine($"\n[save-outer] {plain[start..end].Replace('\n',' ').Replace('\r',' ')}");
                    searchPos = idx + 5;
                    if (searchPos > plain.Length) break;
                }
                if (!foundAny) Console.WriteLine("\n[save-outer] no 'outer' text found anywhere in save file");
            }

            // Extract SaveFileInfo.data_last_modified using IndexOf (one block per slot, in order).
            var lastModByIndex = new List<DateTime>();
            int searchFrom = 0;
            while (true)
            {
                int infoIdx = plain.IndexOf("\"SaveFileInfo\"", searchFrom, StringComparison.Ordinal);
                if (infoIdx < 0) break;
                int modIdx = plain.IndexOf("\"data_last_modified\"", infoIdx, StringComparison.Ordinal);
                if (modIdx < 0 || modIdx - infoIdx > 2000) { searchFrom = infoIdx + 1; continue; }
                int typeIdx = plain.IndexOf("\"System.String\"\"", modIdx, StringComparison.Ordinal);
                if (typeIdx < 0 || typeIdx - modIdx > 200) { searchFrom = infoIdx + 1; continue; }
                int valStart = typeIdx + "\"System.String\"\"".Length;
                int valEnd   = plain.IndexOf('"', valStart);
                if (valEnd > 0 && valEnd - valStart <= 30)
                    lastModByIndex.Add(DateTime.TryParse(plain[valStart..valEnd], out var dt) ? dt : DateTime.MinValue);
                searchFrom = modIdx + 1;
            }

            var slots = new List<SlotData>();
            var slotMatches = SlotRx.Matches(plain).Cast<Match>().ToList();
            int slotIndex = 0;
            for (int si = 0; si < slotMatches.Count; si++)
            {
                var m       = slotMatches[si];
                var slotKey = m.Groups[1].Value;
                var objs    = m.Groups[2].Value;
                int slotNum = slotKey == "BluePrint" ? 1 : int.Parse(slotKey["BluePrint".Length..]);
                int day     = DayRx.Match(objs) is { Success: true } dm
                              ? int.Parse(dm.Groups[1].Value) : 0;
                var lastMod = slotIndex < lastModByIndex.Count ? lastModByIndex[slotIndex] : DateTime.MinValue;
                slotIndex++;

                var boolKeys = new HashSet<string>();
                var ups = new Dictionary<string, int>();
                foreach (Match fm in UpgradeRx.Matches(objs))
                    ups[fm.Groups[1].Value] = int.Parse(fm.Groups[2].Value);
                foreach (Match fm in BoolFieldRx.Matches(objs))
                {
                    boolKeys.Add(fm.Groups[1].Value);
                    ups.TryAdd(fm.Groups[1].Value, fm.Groups[2].Value == "true" ? 1 : 0);
                }
                foreach (Match fm in GeneralInt32Rx.Matches(objs))
                    ups.TryAdd(fm.Groups[1].Value, int.Parse(fm.Groups[2].Value));

                // TCount Contraptions lives in the arrays section; search bounded to this slot's text
                int searchEnd  = si + 1 < slotMatches.Count ? slotMatches[si + 1].Index : plain.Length;
                int contrCount = 0;
                int contrPos   = plain.IndexOf("\"TCount Contraptions\"", m.Index + m.Length,
                                               Math.Max(0, searchEnd - (m.Index + m.Length)), StringComparison.Ordinal);
                if (contrPos >= 0)
                {
                    int arrOpen  = plain.IndexOf('[', contrPos);
                    int arrClose = arrOpen >= 0 ? plain.IndexOf(']', arrOpen) : -1;
                    if (arrOpen >= 0 && arrClose > arrOpen)
                    {
                        var arrContent = plain[arrOpen..arrClose];
                        contrCount = Regex.Matches(arrContent, @"System\.Boolean""\s*true").Count;
                    }
                }

                int outerRoomId = -1;
                var oom = OuterRoomRx.Match(objs);
                if (oom.Success) outerRoomId = int.Parse(oom.Groups[1].Value);

                slots.Add(new SlotData(slotNum, day, lastMod, objs, ups, boolKeys, contrCount, outerRoomId));
            }
            return slots;
        }

        // First-read fallback: pick the most recently modified slot.
        public static SlotData? BestSlot(List<SlotData> slots)
            => slots.Count == 0 ? null : slots.MaxBy(s => (s.LastModified, -s.Slot));

        // On subsequent reads: find which single slot's content changed since last read.
        // Returns that slot, or null if zero or multiple slots changed (keep previous active).
        public static SlotData? FindChangedSlot(List<SlotData> prev, List<SlotData> next)
        {
            var changed = next
                .Where(n => prev.FirstOrDefault(p => p.Slot == n.Slot) is { } p && p.Snapshot != n.Snapshot)
                .ToList();
            return changed.Count == 1 ? changed[0] : null;
        }
    }
}
