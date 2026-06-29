# Blue Prince External Reader

Reads the Blue Prince game's memory in real time and outputs a `tracker_stats.json` file to track data in the OBS overlay.

---

## Prerequisites

### 1. Install the .NET 6 Runtime
This tool requires the **.NET 6 Runtime** (or newer). Download it from Microsoft:

**https://dotnet.microsoft.com/en-us/download/dotnet/6.0**

Under **"Run desktop apps"**, download the **.NET Desktop Runtime 6.x** for Windows x64.

If you're unsure whether you have it, try running the tool — Windows will prompt you with a direct download link if the runtime is missing.

### 2. Run as Administrator
The tool reads another process's memory, which requires elevated privileges on Windows. Right-click `BluePrinceExternalReader.exe` and choose **Run as administrator**, or run it from an elevated command prompt.

---

## How to Use

1. Launch **Blue Prince**
2. Run `BluePrinceExternalReader.exe` as administrator
3. Open `overlay/index.html` and `overlay/map.html` *NOT LEGAL FOR WEEKLIES* in a browser for the overlay UI
4. The tool will scan for the game automatically — once connected, the overlay updates in real time

The overlay works best pinned as a browser window alongside the game, or on a second monitor.




## KNOWN ISSUES
 - Sometimes, the gridmanager just can't be found and I can't find out why yet. I'll keep working on it
 - Won't properly track if you ignore getting the room 8 trophy for multiple allowance tokens
 - Estate title tracker stuff is finnicky at best because its complicated.
 - Occasionally the tracker for upgrade discs, cabinet keys, and permanent floorplans will duplicate what was done on that day temporarily on day end, will fix itself within a minute

## Any Issues or Suggestions
Dm me and ill get back to you in 3-5 business days
