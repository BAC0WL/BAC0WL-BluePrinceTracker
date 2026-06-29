# Blue Prince External Reader

Reads the Blue Prince game's memory in real time and outputs a `tracker_stats.json` file to track data in the OBS overlay.

---

## Prerequisites

### 1. Install the .NET 6 Runtime
This tool requires the **.NET 6 Runtime** (or newer). Download it from Microsoft:

**https://dotnet.microsoft.com/en-us/download/dotnet/6.0**

Under **"Run desktop apps"**, download the **.NET Desktop Runtime 6.x** for Windows x64.

If you're unsure whether you have it, try running the exe and Windows will give you the download link if it is missing

### 2. Run as Administrator
The tool reads another process's memory, which requires elevated privileges on Windows. Right-click `BluePrinceExternalReader.exe` and choose **Run as administrator**, or run it from an elevated command prompt.

---

## How to Use

1. Launch **Blue Prince**
2. Run `BluePrinceExternalReader.exe` as administrator
3. Open `overlay/index.html` (and `overlay/map.html` *NOT LEGAL FOR WEEKLIES*) as a browser source in OBS for the overlay UI 
OR Open localhost:5799/overlay/index.html & localhost:5799/overlay/map.html
4. The tool will scan for the game automatically and update in real time probably




## KNOWN ISSUES
 - Sometimes, the gridmanager just randomly can't be found and I can't find out why yet. I'll keep working on it. It usually fixes itself when you end the day and any misc tracking should still be tracked since I use the save file as a backup, just not until the day ends.
 - Won't properly track if you ignore getting the room 8 trophy for multiple allowance tokens
 - Estate title tracker stuff is finnicky at best because its complicated.
 - Occasionally the tracker for upgrade discs, cabinet keys, and permanent floorplans will duplicate what was done on that day temporarily on day end, will fix itself within a minute.
 - Rotunda actively rotating in estate may not work
 - Guess Bedroom & Quest Bedroom aren't tracked
 - Room Upgrades & Power Hammering the Greenhouse won't update it on the map until the next day
 - Probably a lot more that I forgot about

## Any Issues or Suggestions
Dm me and ill get back to you in 3-5 business days
