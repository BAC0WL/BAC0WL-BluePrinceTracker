// GameOffsets.cs — filled in from Il2CppDumper output (Blue Prince, current build)
namespace BluePrinceExternalReader
{
    internal static class GameOffsets
    {
        // ── GridManager instance field offsets (from dump.cs) ─────────────────
        public const int GM_Cells               = 0x30;  // Vector2Int  (x at +0, y at +4)
        public const int GM_RoomSize            = 0x38;  // float
        public const int GM_Rooms               = 0x70;  // Room[,]  ← 2D array, index = [x, y]
        public const int GM_LastRoomCoord       = 0x84;  // Vector2Int  player grid position
        public const int GM_InstantiatedRoomList = 0xD0;  // List<Room>  all rooms ever placed this run
        public const int GM_ActiveRoomList      = 0xD8;  // List<Room>
        public const int GM_OuterNormalRoom    = 0xE8;  // Room*  (outer room when Berry Picker converts it)

        // ── Room instance field offsets (from dump.cs) ────────────────────────
        public const int Room_AddressableData   = 0x90;  // AddressablesMeshReferences*
        public const int Room_WallNorth         = 0xC0;  // WallType (int)
        public const int Room_WallSouth         = 0xC4;  // WallType (int)
        public const int Room_WallEast          = 0xC8;  // WallType (int)
        public const int Room_WallWest          = 0xCC;  // WallType (int)
        public const int Room_RotationSteps     = 0xF4;  // int  (pre-computed, same as euler.y/90 & 3)

        // ── AddressablesMeshReferences field offsets (from dump.cs) ───────────
        public const int AMR_MeshDataPath       = 0x20;  // IL2CPP string*

        // ── WallType enum values (from dump.cs) ───────────────────────────────
        public const int WallType_Door          = 1;

        // ── IL2CPP 2D array (T[,]) layout ─────────────────────────────────────
        // [0x10] bounds* → { Il2CppArrayBounds[2] }, each bounds = {length(8), lower(8)}
        // [0x18] max_length (uintptr_t)
        // [0x20] elements (row-major: element[i,j] at index i*dim1+j, 8 bytes each)
        public const int ARRAY2D_BoundsPtr      = 0x10;
        public const int ARRAY2D_MaxLength      = 0x18;
        public const int ARRAY2D_Data           = 0x20;
        public const int ARRAY2D_BoundsEntrySize = 16;   // sizeof(Il2CppArrayBounds)

        // ── IL2CPP managed string layout ──────────────────────────────────────
        // [0x10] int length   [0x14] UTF-16 chars[length]
        public const int STR_Length             = 0x10;
        public const int STR_Chars              = 0x14;

        // ── RoomID enum → base room key (first name per integer value, lowercased, no underscores) ─
        public static readonly Dictionary<int, string> RoomIdKeys = new()
        {
            {   1, "thefoundation"    }, {   2, "entrancehall"     }, {   3, "spareroom"         },
            {   4, "rotunda"          }, {   5, "parlor"            }, {   6, "billiardroom"      },
            {   7, "gallery"          }, {   8, "room8"             }, {   9, "closet"            },
            {  10, "walkincloset"     }, {  11, "attic"             }, {  12, "storeroom"         },
            {  13, "nook"             }, {  14, "garage"            }, {  15, "musicroom"         },
            {  16, "lockerroom"       }, {  17, "den"               }, {  18, "winecellar"        },
            {  19, "trophyroom"       }, {  20, "ballroom"          }, {  21, "pantry"            },
            {  22, "rumpusroom"       }, {  23, "vault"             }, {  24, "office"            },
            {  25, "drawingroom"      }, {  26, "study"             }, {  27, "library"           },
            {  28, "chamberofmirrors" }, {  29, "thepool"           }, {  30, "draftingstudio"    },
            {  31, "utilitycloset"    }, {  32, "boilerroom"        }, {  33, "pumproom"          },
            {  34, "security"         }, {  35, "workshop"          }, {  36, "laboratory"        },
            {  37, "sauan"            }, {  38, "coatcheck"         }, {  39, "mailroom"          },
            {  40, "freezer"          }, {  41, "diningroom"        }, {  42, "observatory"       },
            {  43, "conferenceroom"   }, {  44, "aquarium"          }, {  45, "antechamber"       },
            {  46, "room46"           }, {  47, "bedroom"           }, {  48, "boudoir"           },
            {  49, "guestbedroom"     }, {  50, "nursery"           }, {  51, "servantsquarters"  },
            {  52, "bunkroom"         }, {  53, "herladyshipschamber"}, { 54, "masterbedroom"     },
            {  55, "hallway"          }, {  56, "westwinghall"      }, {  57, "eastwinghall"      },
            {  58, "corridor"         }, {  59, "passageway"        }, {  60, "secretpassage"     },
            {  61, "foyer"            }, {  62, "greathall"         }, {  63, "terrace"           },
            {  64, "patio"            }, {  65, "courtyard"         }, {  66, "cloister"          },
            {  67, "veranda"          }, {  68, "greenhouse"        }, {  69, "morningroom"       },
            {  70, "secretgarden"     }, {  71, "commissary"        }, {  72, "kitchen"           },
            {  73, "locksmith"        }, {  74, "showroom"          }, {  75, "laundryroom"       },
            {  76, "bookshop"         }, {  77, "thearmory"         }, {  78, "giftshop"          },
            {  79, "lavatory"         }, {  80, "chapel"            }, {  81, "maidschamber"      },
            {  82, "archives"         }, {  83, "gymnasium"         }, {  84, "darkroom"          },
            {  85, "weightroom"       }, {  86, "furnace"           }, {  87, "dovecote"          },
            {  88, "thekennel"        }, {  89, "clocktower"        }, {  90, "classroom"         },
            {  91, "solarium"         }, {  92, "dormitory"         }, {  93, "vestibule"         },
            {  94, "casino"           }, {  95, "planetarium"       }, {  96, "mechanarium"       },
            {  97, "treasuretrove"    }, {  98, "throneroom"        }, {  99, "lostandfound"      },
            { 100, "conservatory"     }, { 101, "tunnel"            }, { 102, "closedexhibit"     },
            { 103, "toolshed"         }, { 104, "shelter"           }, { 105, "schoolhouse"       },
            { 106, "shrine"           }, { 107, "rootcellar"        }, { 108, "hovel"             },
            { 109, "tradingpost"      }, { 110, "tomb"              },
        };

        // ── Addressable key remaps (game path gives wrong key, map to image filename) ─
        public static readonly Dictionary<string, string> KeyRemaps = new()
        {
            { "foundation", "thefoundation" },
        };

        // ── Permanent rooms with no AddressableData (built-in scene objects) ─────
        public static readonly Dictionary<(int, int), string> FixedRoomKeys = new()
        {
            { (2, 8), "antechamber"  },
            { (2, 0), "entrancehall" },
        };

        // ── Pattern used to locate GridManager in memory ──────────────────────
        // At GridManager+0x30: Cells.x=5, Cells.y=9, RoomSize=10.0f
        public static readonly byte[] GM_ScanPattern = {
            0x05, 0x00, 0x00, 0x00,   // Cells.x = 5
            0x09, 0x00, 0x00, 0x00,   // Cells.y = 9
            0x00, 0x00, 0x20, 0x41    // RoomSize = 10.0f  (IEEE-754 LE)
        };
        public const int GM_PatternOffset      = 0x30;  // pattern lives at obj+0x30

        // ── StatsLogger event reading ─────────────────────────────────────────
        // StatsLogger$$Awake RVA (Address: 8183104 = 0x7CDD40 from script.json)
        public const int SL_Awake_RVA           = 0x7CDD40;
        // Pointer chain: StatsLogger_o → RunStatsData_o → DayData_o → EventStats_o
        public const int SL_CurrentData         = 0x48;  // RunStatsData* in StatsLogger_o
        public const int RSData_GlobalEvents     = 0x38;  // EventStats* GlobalEvents in RunStatsData_o
        public const int RSData_CurrentDay       = 0x48;  // DayData* _currentRecordingDay in RunStatsData_o
        public const int DayData_DayEvents       = 0x30;  // EventStats* in DayData_o
        public const int EventStats_EventsCount  = 0x18;  // Dictionary<EventID,int>* in EventStats_o
        // Dictionary<EventID,int> object offsets (+0x10 for IL2CPP object header)
        public const int Dict_Entries            = 0x18;  // Entry[] _entries
        public const int Dict_Count              = 0x20;  // int _count
        // IL2CPP 1D array: klass(8)+monitor(8)+bounds(8)+maxLen(8) = 0x20 header
        public const int Array1D_Data            = 0x20;
        // Entry<EventID,int> is monomorphized with int keys: hashCode(4)+next(4)+key(4)+value(4) = 16 bytes
        public const int DictEntry_Size          = 16;
        public const int DictEntry_HashCode      = 0;
        public const int DictEntry_Key           = 8;
        public const int DictEntry_Value         = 12;

        // ── EventID enum → name (from dump.cs TypeDefIndex 225) ──────────────
        public static readonly Dictionary<int, string> EventIdNames = new()
        {
            {    0, "Null" },
            {    1, "Dark_Room_Lit" },
            {    2, "Garage_Opened" },
            {    3, "West_Path_Gate_Unlocked" },
            {    4, "Gemstone_Cavern_Unlocked" },
            {    5, "Orchard_Unlocked" },
            {    6, "Microchip_1_Found" },
            {    7, "Microchip_2_Found" },
            {    8, "Cabinet_key_1_found" },
            {    9, "Cabinet_key_2_found" },
            {   10, "Cabinet_1_opened" },
            {   11, "Cabinet_2_opened" },
            {   12, "Satellite_Raised" },
            {   13, "Boudoir_Safe_Opened" },
            {   14, "Drawing_Room_Safe_Opened" },
            {   15, "Study_Safe_Opened" },
            {   16, "Office_Safe_Opened" },
            {   17, "Shelter_Safe_Opened" },
            {   18, "Drafting_Studio_Safe_Opened" },
            {   19, "Mayait_Opened" },
            {   20, "Boiler_Solved" },
            {   21, "Blackbridge_Powered" },
            {   22, "Blackbridge_Admin_Login" },
            {   23, "Blackbridge_Full_Access" },
            {   24, "Well_Drained" },
            {   25, "Pool_Drained" },
            {   26, "Secret_Garden_Found" },
            {   27, "Foundation_Drafted" },
            {   28, "Foundation_Elevator_Lowered" },
            {   29, "Basement_Door_Unlocked" },
            {   30, "Basement_Puzzle_Solved" },
            {   31, "Gas_Orchard" },
            {   32, "Gas_Gemstone" },
            {   33, "Gas_Hovel" },
            {   34, "Gas_Schoolhouse" },
            {   35, "Chess_Solved" },
            {   36, "Chess_Selection" },
            {   37, "Basement_Wall_Knocked" },
            {   38, "Secret_Garden_Knocked" },
            {   39, "Greenhouse_Knocked" },
            {   40, "Weight_Room_Knocked" },
            {   41, "Cliffside_Knocked" },
            {   42, "Gemstone_Jack_mined" },
            {   43, "Gemstone_Collapse" },
            {   44, "Natural_Order_Opened" },
            {   45, "Reservoir_Drained" },
            {   46, "Secret_Garden_Key_Found" },
            {   47, "Network_Password_Accepted" },
            {   48, "Keeper_smashed" },
            {   49, "Antechamber_entered" },
            {   50, "Boat_ride" },
            {   51, "Radiation_unlock" },
            {  100, "Upgrade_Disk_MasterBedroom_found" },
            {  101, "Upgrade_Disk_MorningRoom_found" },
            {  102, "Upgrade_Disk_Foundation_found" },
            {  103, "Upgrade_Disk_Cloister_found" },
            {  104, "Upgrade_Disk_Mechanarium_found" },
            {  105, "Upgrade_Disk_Freezer_found" },
            {  106, "Upgrade_Disk_TorchRoom_found" },
            {  107, "Upgrade_Disk_TradingPost_found" },
            {  108, "Upgrade_Disk_LostFound_found" },
            {  109, "Upgrade_Disk_GreatHall_found" },
            {  110, "Upgrade_Disk_Garage_found" },
            {  111, "Upgrade_Disk_Vault_found" },
            {  112, "Upgrade_Disk_BootLeg_found" },
            {  113, "Upgrade_Disk_Commissary_found" },
            {  114, "Upgrade_Disk_Office_found" },
            {  115, "Upgrade_Disk_Archives_found" },
            {  200, "Upgrade_Disk_Room_StoreRoom_used" },
            {  201, "Upgrade_Disk_Room_SpareRoom_used" },
            {  202, "Upgrade_Disk_Room_SpareRoom2_used" },
            {  203, "Upgrade_Disk_Room_Courtyard_used" },
            {  204, "Upgrade_Disk_Room_MailRoom_used" },
            {  205, "Upgrade_Disk_Room_BunkRoom_used" },
            {  206, "Upgrade_Disk_Room_Closet_used" },
            {  207, "Upgrade_Disk_Room_Hallway_used" },
            {  208, "Upgrade_Disk_Room_Boudoir_used" },
            {  209, "Upgrade_Disk_Room_Parlor_used" },
            {  210, "Upgrade_Disk_Room_BilliardRoom_used" },
            {  211, "Upgrade_Disk_Room_GuestBedroom_used" },
            {  212, "Upgrade_Disk_Room_Nursery_used" },
            {  213, "Upgrade_Disk_Room_Aquarium_used" },
            {  214, "Upgrade_Disk_Room_Nook_used" },
            {  215, "Upgrade_Disk_Room_Cloister_used" },
            {  300, "Vault_149_opened" },
            {  301, "Vault_233_opened" },
            {  302, "Vault_304_opened" },
            {  303, "Vault_370_opened" },
            {  400, "Conservatory_Floorplan_Found" },
            {  401, "Planetarium_Floorplan_Found" },
            {  402, "Lost_and_Found_Floorplan_Found" },
            {  403, "Treasure_Trove_Floorplan_Found" },
            {  404, "Throne_Room_Floorplan_Found" },
            {  405, "Mechanarium_Floorplan_Found" },
            {  406, "Tunnel_Floorplan_Found" },
            {  407, "Closed_Exhibit_Floorplan_Found" },
            {  500, "Dovecote_Added" },
            {  501, "Kennel_Added" },
            {  502, "Casino_Added" },
            {  503, "Clocktower_Added" },
            {  504, "Classroom_Added" },
            {  505, "Solarium_Added" },
            {  506, "Vestibule_Added" },
            {  507, "Dormitory_Added" },
            {  508, "Tomb_Solved" },
            {  509, "Catacombs_Opened" },
            {  510, "Chapel_Lit" },
            {  511, "Tomb_Room_Lit" },
            {  512, "Torch_Chamber_Lit" },
            {  513, "Epsen_Tomb_Lit" },
            {  514, "Freezer_Lit" },
            {  515, "Paper_Crown_Obtained" },
            {  600, "Gallery_5_solved" },
            {  601, "Gallery_6_solved" },
            {  602, "Gallery_7_solved" },
            {  603, "Gallery_8_solved" },
            {  604, "Room_8_reached" },
            {  605, "Room_46_solved" },
            {  606, "Laundry_KeysToGems" },
            {  607, "Laundry_KeysToCoins" },
            {  608, "Servants_drafted" },
            {  609, "Servants_note_read" },
            {  610, "Hidden_riddle_found" },
            {  611, "Upper_gear_reached" },
            {  612, "Laundry_GemsToCoins" },
            {  613, "Laundry_Shoeshine" },
            {  614, "Laundry_StarTreatment" },
            {  615, "Laundry_DieCleaning" },
            {  616, "Room_46_reached" },
            {  700, "Sanctum_Key_Found_Safehouse" },
            {  701, "Sanctum_Key_Found_clocktower" },
            {  702, "Sanctum_Key_Found_Room_46" },
            {  703, "Sanctum_Key_Found_Resevoir" },
            {  704, "Sanctum_Key_Found_Throne_Room" },
            {  705, "Sanctum_Key_Found_Vault" },
            {  706, "Sanctum_Key_Found_Music_Room" },
            {  707, "Sanctum_Key_Found_Mechanarium" },
            {  708, "Sanctum_Key_Used" },
            {  800, "Sigil_Solved_Orinda_Aries" },
            {  801, "Sigil_Solved_Fenn_Aries" },
            {  802, "Sigil_Solved_Arch_Aries" },
            {  803, "Sigil_Solved_Eraja" },
            {  804, "Sigil_Solved_Nuance" },
            {  805, "Sigil_Solved_Mora_Jai" },
            {  806, "Sigil_Solved_Verra" },
            {  807, "Sigil_Solved_Corarica" },
            {  900, "Full_House_trophy" },
            {  901, "Explorers_trophy" },
            {  902, "Bullseye_trophy" },
            {  903, "A_logical_trophy" },
            {  904, "Dirigitrophy" },
            {  905, "Trophy_of_sigils" },
            {  906, "Trophy_8" },
            {  907, "Trophy_of_wealth" },
            {  908, "Diploma_trophy" },
            {  909, "Inheritance_trophy" },
            {  910, "Dare_mode_victory" },
            {  911, "Curse_mode_victory" },
            {  912, "Day_one_victory" },
            {  913, "Trophy_of_trophies" },
            {  914, "Trophy_of_invention" },
            {  915, "Normal_victory" },
            {  916, "Speed_victory" },
            {  917, "Rank_5_Reached" },
            {  918, "Rank_8_Reached" },
            {  919, "Rank_9_Reached" },
            {  920, "Rank_10_Reached" },
            { 1000, "Furnace_event" },
            { 1001, "Diary_Unlock" },
            { 1002, "Chamber_of_Mirrors_Solved" },
            { 1003, "Alzara_1" },
            { 1004, "Alzara_2" },
            { 1005, "Alzara_3" },
            { 1006, "Alzara_4" },
            { 1007, "Alzara_5" },
            { 1008, "Alzara_6" },
            { 1009, "Shrine_cursed" },
            { 1010, "Effigy_found" },
            { 1011, "Sweepstakes_Winner" },
            { 1012, "Monk_note_found" },
            { 1013, "Trophy_list_Looked_at" },
            { 1014, "Music_Sheet_B" },
            { 1015, "Music_Sheet_G" },
            { 1016, "Music_Sheet_M" },
            { 1017, "Music_Sheet_W" },
            { 1018, "Throne_Room_Event" },
            { 1019, "Foundation_note_found" },
            { 1100, "Master_Key_Purchased" },
            { 1101, "Emerald_Bracelet_Purchased" },
            { 1102, "Ornate_Compass_Purchased" },
            { 1103, "Chronograph_Purchased" },
            { 1104, "Silver_Spoon_Purchased" },
            { 1105, "Moon_Pendant_Purchased" },
            { 1106, "A_New_Clue_Purchased" },
            { 1107, "Curse_of_Blackbridge_Purchased" },
            { 1108, "Realm_and_Rune_Purchased" },
            { 1109, "History_1st_Edition_Purchased" },
            { 1110, "Drafting_Strategy_vol_5_Purchased" },
            { 1111, "Drafting_Strategy_vol_6_purchased" },
            { 1200, "Antechamber_Lever_Pulled_GreenHouse" },
            { 1201, "Antechamber_Lever_Pulled_GreatHall" },
            { 1202, "Antechamber_Lever_Pulled_Mechanarium" },
            { 1203, "Antechamber_Lever_Pulled_WeightRoom" },
            { 1204, "Antechamber_Lever_Pulled_SecretGarden" },
            { 1205, "Antechamber_Lever_Pulled_SecretGarden2" },
            { 1206, "Antechamber_Lever_Pulled_Sanctum" },
            { 1207, "Antechamber_Lever_Pulled_ThroneRoom" },
            { 1300, "Crafted_PowerHammer" },
            { 1301, "Crafted_JackHammer" },
            { 1302, "Crafted_LuckyPurse" },
            { 1303, "Crafted_DowsingRod" },
            { 1304, "Crafted_Electromagnet" },
            { 1305, "Crafted_PicksoundAmplified" },
            { 1306, "Crafted_DetectorShovel" },
            { 1307, "Crafted_BurningGlass" },
            { 1400, "Pickup_MagGlass" },
            { 1500, "Chess_Selection_Pawn" },
            { 1501, "Chess_Selection_Knight" },
            { 1502, "Chess_Selection_Bishop" },
            { 1503, "Chess_Selection_Rook" },
            { 1504, "Chess_Selection_Queen" },
            { 1505, "Chess_Selection_King" },
            { 2000, "Drafted_Room" },
            { 2001, "Drafted_Room_Extern" },
            { 2002, "Coat_Check" },
            { 2003, "Rarity_Shift" },
            { 3000, "Timer_Start" },
        };
    }
}
