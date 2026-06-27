using System.Text.Json;
using BluePrinceExternalReader;

const string PROCESS_NAME = "BLUE PRINCE";
const string MODULE_NAME  = "GameAssembly.dll";
const int    POLL_MS      = 500;

string OUTPUT_PATH = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "tracker_stats.json");
OUTPUT_PATH = Path.GetFullPath(OUTPUT_PATH);

Console.WriteLine("Blue Prince External Reader  [build 2026-06-27]");
Console.WriteLine($"Output: {OUTPUT_PATH}");
Console.WriteLine($"Save:   {SaveReader.SavePath}");

// ── Upgrade remaps from upgrades.json ────────────────────────────────────────
var upgradeRemaps = new List<(string SaveField, string BaseKey, Dictionary<string, string> Variants)>();
var upgradesPath = Path.Combine(AppContext.BaseDirectory, "upgrades.json");
if (File.Exists(upgradesPath))
{
    try
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(upgradesPath));
        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            if (entry.Name.StartsWith("_")) continue;
            var obj = entry.Value;
            if (!obj.TryGetProperty("base_key", out var bk)) continue;
            var baseKey = bk.GetString()!;
            var variants = new Dictionary<string, string>();
            if (obj.TryGetProperty("variants", out var vs))
                foreach (var v in vs.EnumerateObject())
                    if (!v.Name.StartsWith("_") && v.Value.ValueKind == JsonValueKind.String)
                    {
                        var mapped = v.Value.GetString()!;
                        if (!mapped.StartsWith("TODO"))
                            variants[v.Name] = mapped;
                    }
            upgradeRemaps.Add((entry.Name, baseKey, variants));
        }
        Console.WriteLine($"[upgrades] Loaded {upgradeRemaps.Count} upgrade entries from upgrades.json");
    }
    catch (Exception ex) { Console.WriteLine($"[upgrades] Failed to load upgrades.json: {ex.Message}"); }
}

// ── Save file state ───────────────────────────────────────────────────────────
int manualSlot = 0;
var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
if (File.Exists(configPath))
{
    try
    {
        using var cfgDoc = JsonDocument.Parse(File.ReadAllText(configPath));
        if (cfgDoc.RootElement.TryGetProperty("save_slot", out var slotProp))
            manualSlot = slotProp.GetInt32();
    }
    catch { }
}
if (manualSlot > 0)
    Console.WriteLine($"[save] manual slot override: slot {manualSlot}");

SaveReader.SlotData? activeSlot = null;
List<SaveReader.SlotData> previousSlots = new();
DateTime saveFileLastWrite = DateTime.MinValue;
Task? saveTask = null;
// ── Upgrade diagnostic: track which remaps have been printed this session ─────
var loggedUpgradeRemaps = new HashSet<string>();

// ── GridManager cache ─────────────────────────────────────────────────────────
IntPtr cachedGmPtr  = IntPtr.Zero;
int    runId        = 0;  // incremented each time a new GM is locked in; sent to overlay so it knows to clear
int    prevRoomCount = 0;
int    maxRoomCount  = 0; // peak rooms seen for current GM — used for end-of-run detection
// All GM addresses we've ever latched on — excluded from every rescan until disconnect.
// Prevents re-latching on any stale GM from a previous run, not just the most recent one.
var excludedGmPtrs = new HashSet<IntPtr>();
int  skipOnlyCount       = 0;     // consecutive scans that found only excluded GMs
bool lastScanHadExcluded = false; // set by FindGridManager
IntPtr cachedSlActive    = IntPtr.Zero;  // StatsLogger.Active singleton (persists across runs)

void RefreshSave()
{
    if (!File.Exists(SaveReader.SavePath)) return;
    var writeTime = File.GetLastWriteTimeUtc(SaveReader.SavePath);
    if (writeTime == saveFileLastWrite) return;
    if (saveTask is { IsCompleted: false }) return;
    saveFileLastWrite = writeTime;
    saveTask = Task.Run(() =>
    {
        try
        {
            var slots = SaveReader.ReadAllSlots(SaveReader.SavePath);

            SaveReader.SlotData? active;
            if (manualSlot > 0)
            {
                active = slots.FirstOrDefault(s => s.Slot == manualSlot);
            }
            else if (previousSlots.Count == 0)
            {
                active = SaveReader.BestSlot(slots);
            }
            else
            {
                var changed = SaveReader.FindChangedSlot(previousSlots, slots);
                active = changed ?? activeSlot;
            }

            previousSlots = slots;
            activeSlot    = active;

            if (active != null)
            {
                Console.WriteLine($"\n[save] slot={active.Slot}  day={active.Day}  upgrades={active.Upgrades.Count}  outerRoom={active.OuterRoomId}");
                foreach (var kv in active.Upgrades.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key))
                    Console.WriteLine($"  {kv.Key} = {kv.Value}");

                // Cross-reference: save fields with no matching remap entry
                var remapFields = new HashSet<string>(upgradeRemaps.Select(r => r.SaveField));
                foreach (var kv in active.Upgrades.Where(kv => kv.Value > 0 && !remapFields.Contains(kv.Key)))
                    Console.WriteLine($"  [untracked] {kv.Key} = {kv.Value}  ← no entry in upgrades.json");
                // Remap entries where the save has no matching field (likely wrong field name in upgrades.json)
                foreach (var (sf, bk, _) in upgradeRemaps.Where(r => !active.Upgrades.ContainsKey(r.SaveField)))
                    Console.WriteLine($"  [no-save-field] '{sf}' (base: {bk})  ← field not found in save file");
            }
        }
        catch (Exception ex) { Console.WriteLine($"\n[save error] {ex.Message}"); }
    });
}

// ── Control HTTP server (buttons in overlay POST here) ────────────────────────
var httpListener = new System.Net.HttpListener();
httpListener.Prefixes.Add("http://localhost:5799/");
try
{
    httpListener.Start();
    _ = Task.Run(async () =>
    {
        while (true)
        {
            try
            {
                var ctx  = await httpListener.GetContextAsync();
                var path = ctx.Request.Url!.AbsolutePath;
                string reply = "ok";

                if (path == "/rescan")
                {
                    if (cachedGmPtr != IntPtr.Zero)
                    {
                        excludedGmPtrs.Add(cachedGmPtr);
                        cachedGmPtr   = IntPtr.Zero;
                        prevRoomCount = 0;
                        maxRoomCount  = 0;
                        reply = "rescan triggered";
                        Console.WriteLine("\n[cmd] Manual rescan triggered");
                    }
                    else reply = "not tracking a GM";
                }
                else if (path == "/slot")
                {
                    var n = ctx.Request.QueryString["n"];
                    if (int.TryParse(n, out var s) && s >= 0 && s <= 4)
                    {
                        manualSlot = s;
                        saveFileLastWrite = DateTime.MinValue;  // force RefreshSave to re-read with new slot
                        try { File.WriteAllText(configPath, JsonSerializer.Serialize(new { save_slot = s }, new JsonSerializerOptions { WriteIndented = true })); }
                        catch { }
                        reply = $"slot={s}";
                        Console.WriteLine($"\n[cmd] Save slot set to {(s == 0 ? "auto" : s.ToString())}");
                    }
                    else reply = "invalid slot";
                }

                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.ContentType = "text/plain";
                var bytes = System.Text.Encoding.UTF8.GetBytes(reply);
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
            catch { }
        }
    });
    Console.WriteLine("[ctrl] Control server on http://localhost:5799/");
}
catch (Exception ex)
{
    Console.WriteLine($"[ctrl] Could not start control server: {ex.Message}");
}

while (true)
{
    try { Poll(); }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[error] {ex.Message}");
        WriteFile(new { connected = false, reason = ex.Message });
        excludedGmPtrs.Add(cachedGmPtr);
        cachedGmPtr   = IntPtr.Zero;
        prevRoomCount = 0;
        maxRoomCount  = 0;
    }
    await Task.Delay(POLL_MS);
}

// ── Main poll tick ────────────────────────────────────────────────────────────
void Poll()
{
    RefreshSave();

    using var mem = MemoryReader.TryAttach(PROCESS_NAME, MODULE_NAME);
    if (mem == null)
    {
        if (cachedGmPtr != IntPtr.Zero) Console.WriteLine("\n[disconnected]");
        cachedGmPtr        = IntPtr.Zero;
        runId              = 0;
        excludedGmPtrs.Clear(); // new process session — fresh address space
        loggedUpgradeRemaps.Clear();
        prevRoomCount  = 0;
        maxRoomCount   = 0;
        skipOnlyCount  = 0;
        cachedSlActive = IntPtr.Zero;
        WriteFile(new { connected = false });
        Console.Write(".");
        return;
    }

    if (cachedGmPtr != IntPtr.Zero && !ValidateGridManager(mem, cachedGmPtr))
    {
        Console.WriteLine("\n[rescan] GridManager stale — rescanning");
        excludedGmPtrs.Clear();         // old history may block valid reused addresses
        excludedGmPtrs.Add(cachedGmPtr);
        cachedGmPtr   = IntPtr.Zero;
        prevRoomCount = 0;
        maxRoomCount  = 0;
        skipOnlyCount = 0;
    }

    if (cachedGmPtr == IntPtr.Zero)
    {
        cachedGmPtr = FindGridManager(mem, allowExcluded: skipOnlyCount >= 3);
        if (cachedGmPtr == IntPtr.Zero)
        {
            if (lastScanHadExcluded) skipOnlyCount++;
            else skipOnlyCount = 0;
            WriteFile(new { connected = true, day = 0 });
            string skipInfo = excludedGmPtrs.Count > 0
                ? $"? [excl={excludedGmPtrs.Count} skipOnly={skipOnlyCount}/{(skipOnlyCount >= 3 ? "allowFallback" : "3")}]"
                : "?";
            Console.Write(skipInfo);
            return;
        }
        skipOnlyCount = 0;
        var (cx, cy) = mem.ReadVector2Int(cachedGmPtr + GameOffsets.GM_Cells);
        var rs       = mem.ReadFloat(cachedGmPtr + GameOffsets.GM_RoomSize);
        runId++;
        Console.WriteLine($"\n[found] GridManager @ 0x{cachedGmPtr:X}  run={runId}  Cells=({cx},{cy})  RoomSize={rs}");
        if (cx != 5 || cy != 9 || Math.Abs(rs - 10f) > 0.01f)
        {
            Console.WriteLine("[warn] GridManager fields look wrong — will retry next poll");
            cachedGmPtr = IntPtr.Zero;
            runId--;
            return;
        }
        // excludedGmPtrs intentionally kept — old addresses stay blocked for the session
    }

    var gmPtr = cachedGmPtr;

    var (playerX, playerY) = mem.ReadVector2Int(gmPtr + GameOffsets.GM_LastRoomCoord);
    var (cellsX,  cellsY)  = mem.ReadVector2Int(gmPtr + GameOffsets.GM_Cells);

    var roomsArrayPtr = mem.ReadPtr(gmPtr + GameOffsets.GM_Rooms);
    var (rooms, gridPtrs) = ReadRooms(mem, roomsArrayPtr, cellsX, cellsY);

    // Scan InstantiatedRoomList for Berry Picker outer rooms (spawned via normal flow)
    int instListCount = -1;
    try
    {
        var instListPtr = mem.ReadPtr(gmPtr + GameOffsets.GM_InstantiatedRoomList);
        if (instListPtr != IntPtr.Zero)
        {
            instListCount = mem.ReadInt32(instListPtr + 0x18);
            var arr       = mem.ReadPtr(instListPtr + 0x10);
            for (int i = 0; i < Math.Min(instListCount, 200); i++)
            {
                var rPtr = mem.ReadPtr(arr + (0x20 + i * 8));
                if (rPtr == IntPtr.Zero || gridPtrs.Contains(rPtr)) continue;
                try
                {
                    var r = ReadRoom(mem, rPtr, -1, -1);
                    if (r == null) continue;
                    rooms.Add(r);
                    gridPtrs.Add(rPtr);
                    Console.Write($" [extra={((dynamic)r).key}]");
                }
                catch { }
            }
        }
    }
    catch { }

    // Read outer room via EnvironmentManager (regular outer room path)
    {
        var envKey = ReadEnvMgrOuterKey(mem);
        if (!string.IsNullOrEmpty(envKey))
        {
            if (GameOffsets.KeyRemaps.TryGetValue(envKey, out var rk)) envKey = rk;
            foreach (var (saveField, baseKey, variants) in upgradeRemaps)
            {
                if (baseKey != envKey) continue;
                int lv = 0;
                activeSlot?.Upgrades.TryGetValue(saveField, out lv);
                if (lv > 0 && variants.TryGetValue(lv.ToString(), out string? uk) && uk != null)
                    envKey = uk;
                break;
            }
            if (!rooms.Any(r => ((dynamic)r).key == envKey))
            {
                rooms.Add(new { x = -1, y = -1, key = envKey, doors = "", wr = 0 });
                Console.Write($" [outer={envKey}]");
            }
        }
    }

    // Fallback: try _nextRoom at GridManager+0x110
    try
    {
        var nextPtr = mem.ReadPtr(gmPtr + 0x110);
        if (nextPtr != IntPtr.Zero)
        {
            var nr = ReadRoom(mem, nextPtr, -1, -1);
            if (nr != null && !rooms.Any(r => ((dynamic)r).key == ((dynamic)nr).key))
            {
                rooms.Add(nr);
                Console.Write($" [next={((dynamic)nr).key}]");
            }
        }
    }
    catch { }

    if (rooms.Count > maxRoomCount) maxRoomCount = rooms.Count;

    if (maxRoomCount >= 5 && rooms.Count <= 1)
    {
        Console.WriteLine($"\n[reset] Run ended (rooms {prevRoomCount}→{rooms.Count}, peak={maxRoomCount}) — rescanning for new GM");
        excludedGmPtrs.Clear();         // old history may block valid reused addresses
        excludedGmPtrs.Add(cachedGmPtr);
        cachedGmPtr    = IntPtr.Zero;
        cachedSlActive = IntPtr.Zero;
        prevRoomCount  = 0;
        maxRoomCount   = 0;
        skipOnlyCount  = 0;
        WriteFile(new { connected = true, day = 0 });
        return;
    }
    prevRoomCount = rooms.Count;

    if (cachedSlActive != IntPtr.Zero)
    {
        try { if (mem.ReadPtr(cachedSlActive + 0x10) == IntPtr.Zero) { Console.WriteLine("\n[events] StatsLogger stale — rescanning"); cachedSlActive = IntPtr.Zero; } }
        catch { cachedSlActive = IntPtr.Zero; }
    }
    if (cachedSlActive == IntPtr.Zero)
        cachedSlActive = FindStatsLoggerActive(mem);
    var dayEvents = ReadEventStats(mem, isDayEvents: true);
    var runEvents = ReadEventStats(mem, isDayEvents: false);

    var output = new
    {
        connected  = true,
        run_id     = runId,
        day        = 0,
        live       = new { keys = 0, gems = 0, gold = 0, steps = 0, stars = 0, allowance = 0 },
        map        = roomsArrayPtr == IntPtr.Zero
            ? null
            : (object)new { player_x = playerX, player_y = playerY, rooms },
        day_events = dayEvents,
        run_events = runEvents,
        save       = activeSlot == null ? null : BuildSaveSection(activeSlot)
    };

    WriteFile(output);
    string roomsInfo = roomsArrayPtr == IntPtr.Zero ? "no run" : $"rooms={rooms.Count}";
    Console.Write($"\r{DateTime.Now:HH:mm:ss}  {roomsInfo}  player=({playerX},{playerY})   ");
}

// ── Verify a candidate pointer looks like a GridManager ───────────────────────
bool ValidateGridManager(MemoryReader mem, IntPtr ptr)
{
    if (ptr == IntPtr.Zero) return false;
    try
    {
        // m_CachedPtr at +0x10: Unity nullifies this when the object is Destroy()ed.
        // The managed C# shell stays in memory (GC hasn't collected it yet), so Cells/RoomSize
        // still read fine — but the native object is gone and the GM is no longer active.
        if (mem.ReadPtr(ptr + 0x10) == IntPtr.Zero) return false;
        var (cx, cy) = mem.ReadVector2Int(ptr + GameOffsets.GM_Cells);
        float rs = mem.ReadFloat(ptr + GameOffsets.GM_RoomSize);
        return cx == 5 && cy == 9 && Math.Abs(rs - 10f) < 0.01f;
    }
    catch { return false; }
}

// ── Locate GridManager by scanning memory for its unique field signature ──────
IntPtr FindGridManager(MemoryReader mem, bool allowExcluded = false)
{
    lastScanHadExcluded = false;
    Console.Write("\n[scanning...]");
    long startAddr = 0x10000;
    IntPtr result = IntPtr.Zero;

    int totalHits = 0, nullCached = 0, wrongFields = 0, scanExceptions = 0;

    while (true)
    {
        var hitAddr = mem.ScanForPattern(GameOffsets.GM_ScanPattern, startAddr);
        if (hitAddr == IntPtr.Zero) break;

        var gmPtr = hitAddr - GameOffsets.GM_PatternOffset;
        totalHits++;

        // Verbose validation — mirrors ValidateGridManager but logs the reason for failure
        bool valid = false;
        try
        {
            var cached = mem.ReadPtr(gmPtr + 0x10);
            if (cached == IntPtr.Zero)
            {
                nullCached++;
                string extra = "";
                try
                {
                    var instPtr   = mem.ReadPtr(gmPtr + GameOffsets.GM_InstantiatedRoomList);
                    var activePtr = mem.ReadPtr(gmPtr + GameOffsets.GM_ActiveRoomList);
                    int instCount   = instPtr   != IntPtr.Zero ? mem.ReadInt32(instPtr   + 0x18) : -1;
                    int activeCount = activePtr != IntPtr.Zero ? mem.ReadInt32(activePtr + 0x18) : -1;
                    extra = $" inst={instCount} active={activeCount}";
                }
                catch { }
                Console.Write($" [miss @ 0x{gmPtr:X}: cached=null{extra}]");
            }
            else
            {
                var (cx, cy) = mem.ReadVector2Int(gmPtr + GameOffsets.GM_Cells);
                float rs     = mem.ReadFloat(gmPtr + GameOffsets.GM_RoomSize);
                bool cellsOk = cx == 5 && cy == 9;
                bool sizeOk  = Math.Abs(rs - 10f) < 0.01f;
                if (cellsOk && sizeOk)
                {
                    valid = true;
                }
                else
                {
                    wrongFields++;
                    Console.Write($" [miss @ 0x{gmPtr:X}: cells=({cx},{cy}) rs={rs:F2}]");
                }
            }
        }
        catch (Exception ex)
        {
            scanExceptions++;
            Console.Write($" [miss @ 0x{gmPtr:X}: ex={ex.GetType().Name}]");
        }

        if (valid)
        {
            if (excludedGmPtrs.Contains(gmPtr))
            {
                lastScanHadExcluded = true;
                if (!allowExcluded)
                {
                    Console.Write($" [skip excl @ 0x{gmPtr:X}]");
                    startAddr = (long)hitAddr + GameOffsets.GM_ScanPattern.Length;
                    continue;
                }
                Console.Write($" [fallback-excl @ 0x{gmPtr:X}]");
                excludedGmPtrs.Remove(gmPtr);
            }

            try
            {
                var (cx, cy)   = mem.ReadVector2Int(gmPtr + GameOffsets.GM_Cells);
                var roomsPtr   = mem.ReadPtr(gmPtr + GameOffsets.GM_Rooms);
                var (rooms, _) = ReadRooms(mem, roomsPtr, cx, cy);

                int listCount = -1;
                try
                {
                    var listPtr = mem.ReadPtr(gmPtr + GameOffsets.GM_ActiveRoomList);
                    if (listPtr != IntPtr.Zero)
                        listCount = mem.ReadInt32(listPtr + 0x18);
                }
                catch { }

                var (px, py) = mem.ReadVector2Int(gmPtr + GameOffsets.GM_LastRoomCoord);
                string listStr = listCount >= 0 ? $" list={listCount}" : "";
                Console.Write($" [0x{gmPtr:X} rooms={rooms.Count}{listStr} player=({px},{py})]");

                result = gmPtr;
            }
            catch { }
        }

        startAddr = (long)hitAddr + GameOffsets.GM_ScanPattern.Length;
    }

    if (result == IntPtr.Zero)
        Console.Write($" [scan done: {totalHits} hit(s) — null-cached={nullCached} wrong-fields={wrongFields} excl={excludedGmPtrs.Count} ex={scanExceptions}]");

    return result;
}

// ── Read outer room key from EnvironmentManager._outerRoom ────────────────────
// get_Instance() at RVA 0x9087A0. Scans for RIP-relative MOV/LEA instructions.
// Verified path: MOV at offset ~73 loads Il2CppClass*; static_fields at +0xB8;
// _instance at sf+0; _outerRoom at EM+0x30; _addressableData at EP+0x48; path at +0x20.
string ReadEnvMgrOuterKey(MemoryReader mem)
{
    const int GET_INSTANCE_RVA = 0x9087A0;
    try
    {
        var methodAddr = mem.ModuleBase + GET_INSTANCE_RVA;
        var bytes = mem.ReadBytes(methodAddr, 128);

        for (int i = 0; i < bytes.Length - 7; i++)
        {
            if (bytes[i] < 0x48 || bytes[i] > 0x4F) continue;
            bool isMov = bytes[i + 1] == 0x8B;
            bool isLea = bytes[i + 1] == 0x8D;
            if (!isMov && !isLea) continue;
            if ((bytes[i + 2] & 0xC7) != 0x05) continue;

            int disp32    = BitConverter.ToInt32(bytes, i + 3);
            long ripAfter = (long)methodAddr + i + 7;
            var target    = (IntPtr)(ripAfter + disp32);

            // For MOV: dereference to get candidate; for LEA: target IS the candidate
            var cand = isMov ? mem.ReadPtr(target) : target;
            if (cand == IntPtr.Zero) continue;

            // Try candidate as EnvironmentManager* directly
            if (mem.ReadPtr(cand + 0x10) != IntPtr.Zero)
            {
                var key = TryReadEnvPartKey(mem, cand);
                if (key != "") return key;
            }

            // Try candidate as Il2CppClass*: static_fields → _instance → EM*
            foreach (int sfOff in new[] { 0xB8, 0xA8, 0xC8, 0xD0, 0x98 })
            {
                try
                {
                    var sf = mem.ReadPtr(cand + sfOff);
                    if (sf == IntPtr.Zero) continue;
                    foreach (int instOff in new[] { 0, 8, 16 })
                    {
                        try
                        {
                            var inst = mem.ReadPtr(sf + instOff);
                            if (inst == IntPtr.Zero) continue;
                            if (mem.ReadPtr(inst + 0x10) == IntPtr.Zero) continue;
                            var key = TryReadEnvPartKey(mem, inst);
                            if (key != "") return key;
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
    catch { }
    return "";
}

string TryReadEnvPartKey(MemoryReader mem, IntPtr envMgr)
{
    try
    {
        var envPart  = mem.ReadPtr(envMgr + 0x30);  // _outerRoom (EnvironmentPart*)
        if (envPart == IntPtr.Zero) return "";
        var addrData = mem.ReadPtr(envPart + 0x48); // _addressableData
        if (addrData == IntPtr.Zero) return "";
        var pathPtr  = mem.ReadPtr(addrData + GameOffsets.AMR_MeshDataPath);
        var path     = mem.ReadManagedString(pathPtr) ?? "";
        if (string.IsNullOrEmpty(path) || !path.Contains('/')) return "";
        var seg  = path.Split('/');
        var last = seg[^1].ToLower();
        var dot  = last.LastIndexOf('.');
        var key  = dot > 0 ? last[..dot] : last;
        key = key.Replace(" ", "").Replace("-", "").Replace("'", "");
        return key;
    }
    catch { return ""; }
}

// ── Read all rooms from the Room[,] 2D array ──────────────────────────────────
(List<object>, HashSet<IntPtr>) ReadRooms(MemoryReader mem, IntPtr arrayPtr, int cellsX, int cellsY)
{
    var result = new List<object>();
    var ptrs   = new HashSet<IntPtr>();
    if (arrayPtr == IntPtr.Zero) return (result, ptrs);

    var (d0, d1) = mem.ReadArray2dDims(arrayPtr);
    if (d0 <= 0 || d1 <= 0) return (result, ptrs);

    int dimX = Math.Min(d0, cellsX);
    int dimY = Math.Min(d1, cellsY);

    for (int x = 0; x < dimX; x++)
        for (int y = 0; y < dimY; y++)
        {
            var roomPtr = mem.ReadArray2dElement(arrayPtr, x, y, d1);
            if (roomPtr == IntPtr.Zero) continue;
            try
            {
                var room = ReadRoom(mem, roomPtr, x, y);
                if (room == null) continue;
                result.Add(room);
                ptrs.Add(roomPtr);
            }
            catch { /* skip bad room */ }
        }
    return (result, ptrs);
}

// ── Read a single Room object ─────────────────────────────────────────────────
object? ReadRoom(MemoryReader mem, IntPtr roomPtr, int gx, int gz)
{
    int wn = mem.ReadInt32(roomPtr + GameOffsets.Room_WallNorth);
    int ws = mem.ReadInt32(roomPtr + GameOffsets.Room_WallSouth);
    int we = mem.ReadInt32(roomPtr + GameOffsets.Room_WallEast);
    int ww = mem.ReadInt32(roomPtr + GameOffsets.Room_WallWest);
    int wr = mem.ReadInt32(roomPtr + GameOffsets.Room_RotationSteps) & 3;

    int[] lw = { wn, we, ws, ww };
    int wnW = lw[(4 - wr) % 4];
    int wsW = lw[(6 - wr) % 4];
    int weW = lw[(5 - wr) % 4];
    int wwW = lw[(7 - wr) % 4];
    string doors = DoorSuffix(wnW, wsW, weW, wwW);

    string key = "";
    var addrPtr = mem.ReadPtr(roomPtr + GameOffsets.Room_AddressableData);
    if (addrPtr != IntPtr.Zero)
    {
        var pathStrPtr = mem.ReadPtr(addrPtr + GameOffsets.AMR_MeshDataPath);
        var path = mem.ReadManagedString(pathStrPtr);
        if (!string.IsNullOrEmpty(path))
        {
            var seg  = path.Split('/');
            var last = seg[^1].ToLower();
            var dot  = last.LastIndexOf('.');
            key = dot > 0 ? last[..dot] : last;
            key = key.Replace(" ", "").Replace("-", "").Replace("'", "");
        }
    }

    if (!string.IsNullOrEmpty(key) && GameOffsets.KeyRemaps.TryGetValue(key, out var remapped))
        key = remapped;

    if (string.IsNullOrEmpty(key))
    {
        if (!GameOffsets.FixedRoomKeys.TryGetValue((gx, gz), out key!))
            return null;
    }

    var slot = activeSlot;
    if (slot != null)
    {
        foreach (var (saveField, baseKey, variants) in upgradeRemaps)
        {
            if (baseKey != key) continue;
            int level = 0;
            slot.Upgrades.TryGetValue(saveField, out level);
            if (level > 0 && variants.TryGetValue(level.ToString(), out string? upgKey) && upgKey != null)
            {
                var logKey = $"{key}→{upgKey}";
                if (loggedUpgradeRemaps.Add(logKey))
                    Console.WriteLine($"\n[upgrade] {key} → {upgKey}  ({saveField}={level})");
                key = upgKey;
            }
            break;
        }
    }

    return new { x = gx, y = gz, key, doors, wr };
}

// ── Door suffix ───────────────────────────────────────────────────────────────
string DoorSuffix(int wn, int ws, int we, int ww)
{
    bool dn = wn == GameOffsets.WallType_Door;
    bool ds = ws == GameOffsets.WallType_Door;
    bool de = we == GameOffsets.WallType_Door;
    bool dw = ww == GameOffsets.WallType_Door;
    int  ct = (dn?1:0)+(ds?1:0)+(de?1:0)+(dw?1:0);
    return ct switch {
        4 => "4",
        3 => !dn?"n":!ds?"s":!de?"e":"w",
        2 => dn&&ds?"ns":de&&dw?"ew":!dn&&!de?"n":!de&&!ds?"e":!ds&&!dw?"s":"w",
        1 => dn?"s":ds?"n":de?"w":"e",
        _ => ""
    };
}

// ── Find StatsLogger.Active via Awake() machine code ─────────────────────────
// Scans RIP-relative instructions in Awake() to locate the Il2CppClass TypeInfo,
// then reads static_fields → Active (the singleton instance).
IntPtr FindStatsLoggerActive(MemoryReader mem)
{
    try
    {
        var methodAddr = mem.ModuleBase + GameOffsets.SL_Awake_RVA;
        var bytes      = mem.ReadBytes(methodAddr, 256);

        for (int i = 0; i < bytes.Length - 7; i++)
        {
            if (bytes[i] < 0x48 || bytes[i] > 0x4F) continue;
            if (bytes[i + 1] != 0x8B && bytes[i + 1] != 0x8D) continue;  // MOV or LEA
            if ((bytes[i + 2] & 0xC7) != 0x05) continue;                  // RIP-relative

            int  disp32   = BitConverter.ToInt32(bytes, i + 3);
            long ripAfter = (long)methodAddr + i + 7;
            var  cand     = mem.ReadPtr((IntPtr)(ripAfter + disp32));      // Il2CppClass*
            if (cand == IntPtr.Zero) continue;

            try
            {
                var sf     = mem.ReadPtr(cand + 0xB8);   // static_fields
                if (sf == IntPtr.Zero) continue;
                var active = mem.ReadPtr(sf);             // StatsLogger_StaticFields.Active
                if (active == IntPtr.Zero) continue;
                if (mem.ReadPtr(active + GameOffsets.SL_CurrentData) == IntPtr.Zero) continue;
                Console.WriteLine($"\n[events] StatsLogger.Active @ 0x{active:X}");
                return active;
            }
            catch { }
        }
    }
    catch { }
    return IntPtr.Zero;
}

// ── Read an EventStats.EventsCount dictionary ─────────────────────────────────
// isDayEvents=true  → DayData._currentRecordingDay.DayEvents  (resets each day)
// isDayEvents=false → RunStatsData.GlobalEvents                (accumulates all run)
Dictionary<string, int> ReadEventStats(MemoryReader mem, bool isDayEvents)
{
    var result = new Dictionary<string, int>();
    if (cachedSlActive == IntPtr.Zero) return result;
    try
    {
        var cd = mem.ReadPtr(cachedSlActive + GameOffsets.SL_CurrentData);
        if (cd == IntPtr.Zero) return result;

        IntPtr es;
        if (isDayEvents)
        {
            var day = mem.ReadPtr(cd + GameOffsets.RSData_CurrentDay);
            if (day == IntPtr.Zero) return result;
            es = mem.ReadPtr(day + GameOffsets.DayData_DayEvents);
        }
        else
        {
            es = mem.ReadPtr(cd + GameOffsets.RSData_GlobalEvents);
        }
        if (es == IntPtr.Zero) return result;

        var dict = mem.ReadPtr(es + GameOffsets.EventStats_EventsCount);
        if (dict == IntPtr.Zero) return result;

        var entries = mem.ReadPtr(dict + GameOffsets.Dict_Entries);
        int count   = mem.ReadInt32(dict + GameOffsets.Dict_Count);
        if (entries == IntPtr.Zero || count <= 0 || count > 5000) return result;

        for (int i = 0; i < count; i++)
        {
            var  addr     = entries + GameOffsets.Array1D_Data + i * GameOffsets.DictEntry_Size;
            int  hashCode = mem.ReadInt32(addr + GameOffsets.DictEntry_HashCode);
            if (hashCode < 0) continue;
            int  key = mem.ReadInt32(addr + GameOffsets.DictEntry_Key);
            int  val = mem.ReadInt32(addr + GameOffsets.DictEntry_Value);
            if (val <= 0) continue;
            string name = GameOffsets.EventIdNames.TryGetValue(key, out var n) ? n : key.ToString();
            result[name] = val;
        }
    }
    catch { }
    return result;
}

// ── Build save-file section for tracker_stats.json ───────────────────────────
object BuildSaveSection(SaveReader.SlotData slot)
{
    var u = slot.Upgrades;
    int Get(string key) => u.TryGetValue(key, out var v) ? v : 0;

    // Chamber additions = int-typed " Added" fields only; bool-typed ones (found/studio floorplans) excluded.
    int chamberAdditions = u
        .Where(kv => kv.Key.EndsWith(" Added", StringComparison.OrdinalIgnoreCase)
                     && kv.Value > 0
                     && !slot.BoolKeys.Contains(kv.Key))
        .Sum(kv => kv.Value);

    // Found floorplans — bool fields in objs, confirmed from save output
    string[] fpFoundKeys = {
        "Conservatory Added", "Planetarium Added", "Lost&Found Added",
        "Treasure Trove Added", "Throne Room Added", "Mechanarium Added",
        "Tunnel Added", "Closed Exhibit Added",
    };

    // Drafting Studio additions — bool fields in objs, confirmed from save output
    string[] fpStudioKeys = {
        "Dovecote Added", "Kennel Added", "Casino Added", "Clocktower Added",
        "Classroom Added", "Solarium Added", "Vestibule Added", "Dormitory Added",
    };

    return new
    {
        mjb = new Dictionary<string, int>
        {
            ["LostFound"]     = Get("MJB   -  LostFound"),
            ["ArchAries"]     = Get("MJB - Arch Aries"),
            ["ClosedExhibit"] = Get("MJB - Closed Exhibit"),
            ["Corarica"]      = Get("MJB - Corarica"),
            ["Eraja"]         = Get("MJB - Eraja"),
            ["FennAries"]     = Get("MJB - Fenn Aries"),
            ["MasterBedroom"] = Get("MJB - Master Bedroom"),
            ["MoraJai"]       = Get("MJB - Mora Jai"),
            ["Nuance"]        = Get("MJB - Nuance"),
            ["OrindaAries"]   = Get("MJB - Orinda Aries"),
            ["Solarium"]      = Get("MJB - Solarium"),
            ["Tomb"]          = Get("MJB - Tomb"),
            ["TradingPost"]   = Get("MJB - Trading Post"),
            ["Tunnel"]        = Get("MJB - Tunnel"),
            ["Verra"]         = Get("MJB - Verra"),
            ["WatchTower"]    = Get("MJB - Watch Tower"),
        },
        allowance_bonus = new
        {
            cloister_allowance = Get("Cloister Allowance"),
            opened_149         = Get("149 Opened"),
            opened_233         = Get("233 Opened"),
            entrance_hall      = Get("Entrance Hall Token"),
            room8_solved       = Get("Room 8 Solved"),
        },
        cabinet = new
        {
            c3 = Get("File Cabinet 3 Open"),
            c4 = Get("File Cabinet 4 Open"),
            c5 = Get("File Cabinet 5 Open"),
        },
        room8_drafted      = Get("TCount Room8 Puzzles"),
        chamber_additions  = chamberAdditions,
        contraptions_save  = slot.ContraptionsCount,
        upgrade_discs_save = Get("Upgrade Tally"),
        fp_found_save      = fpFoundKeys.Count(k => Get(k) > 0),
        fp_studio_save     = fpStudioKeys.Count(k => Get(k) > 0),
    };
}

// ── Write JSON output ─────────────────────────────────────────────────────────
void WriteFile(object obj)
    => File.WriteAllText(OUTPUT_PATH,
        JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
