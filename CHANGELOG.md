# Changelog

What changed in each release of UE MM, newest first: one short line per change.
The app shows this list (the **Changelog** link in the sidebar's update box), so
every release adds its section at the top, as `## <version> (<yyyy-mm-dd>)`.

## 1.6.0-manual.2 (2026-11-05)
- Ignore .log and .tmp files in the unmanaged-file warning without deleting them; keep unknown mods and settings visible.

## 1.6.0-manual.1 (2026-11-05)
- Modified PBZ Mod Hub build based on upstream 1.5.5; this is not an official UE MM release.
- Scan and adopt installed PAK, UE4SS and PBZ script mods without moving their game files; keep settings and backups.
- Recognize PBZModLoader packages, explicit Mods Menu routes and alternative proxy entries; manually map unknown layouts with a destination preview.
- Separate enabled/deployed status from timestamped loader success or failure.

## 1.5.5 (2026-11-04)
- Script mods: files below Content/JavaScript now install, such as Mods Menu
- Refresh button on the Mods page, to check the mods folder again without a restart
- Game files and Exe files mods have a blue row, and a new one shows a short note before it installs
- The window no longer goes below its start size

## 1.5.4 (2026-11-04)
- Blue UPDATE on the Mods entry in the sidebar when the recommended mod has a new version

## 1.5.3 (2026-11-04)
- Recommended mod in its own card at the top of the Mods page, with an update check at every start
- One click installs it, and installs or updates UE4SS when the mod needs it
- First mod: UE4SS C++ BYPASS brings script-based pak mods back after the game's new script check (experimental)
- Technical notes on the card: how the mod works and where its source code is

## 1.5.2 (2026-11-03)
- Data, app updates and UE4SS downloads from Cloudflare first, GitHub as backup
- Remote box in the sidebar: where the data came from, with a short explanation
- "Try again" in that window when no data came at start
- Help reports name where the data and the update check came from
- Traditional Chinese: "Open exe folder" names the right folder (thanks, CooperZhuang)

## 1.5.1 (2026-11-02)
- Open folder links in the mod details: each folder of a mod opens in the game

## 1.5.0 (2026-11-02)
- Traditional Chinese (thanks, CooperZhuang)
- More natural Simplified Chinese texts (thanks, CooperZhuang)
- Traditional Chinese in a Taiwan font
- UE4SS log in tabs: UE4SS, Mods and Unnamed
- Known harmless UE4SS lines no longer count as errors
- Keep my version: a mod file changed outside the manager no longer blocks the mod
- Same title size on every page; long contributor lines wrap

## 1.4.1 (2026-11-01)
- Changelog link in the update box: what changed in each version
- A file another program holds gets a clear message, not an error (thanks, CooperZhuang)

## 1.4.0 (2026-11-01)
- Key binding and config files in the game's own folders (thanks, CooperZhuang)
- ReShade, RenoDX and other loader mods next to the game's exe
- The game's own files kept and put back; saves never touched
- A settings .txt next to a pak installs in any folder
- Install window shows where files go and names what it skips

## 1.3.2 (2026-11-01)
- Rename installed mods: blue Edit box in the mod details
- "Optional" hint translated

## 1.3.1 (2026-10-31)
- UE4SS mods with Scripts/config.json get the settings editor

## 1.3.0 (2026-10-31)
- Vietnamese
- Warning for an unofficial UE4SS build
- Mod names stay visible in a narrow window
- More natural Chinese texts

## 1.2.2 (2026-10-30)
- UE4SS folders without code install as they are, for mod settings pages

## 1.2.1 (2026-10-30)
- UE4SS mods with shared Lua libraries install completely
- File tree button for each mod
- Clear deletes the UE4SS log
- Update notes shown formatted

## 1.2.0 (2026-10-30)
- Mod details with every file a mod placed
- Mod sets: turn groups of mods on with one click
- Test the game without mods
- Restore a save backup
- Own settings editor with colours; a .txt next to a pak counts as settings
- UE4SS errors: note when the game closes, red mark in the list, log search
- Simplified Chinese (thanks, Null993)
- Window opens where it was; Ctrl+F and F5

## 1.1.1 (2026-10-29)
- Settings files next to a pak (.json, .ini and more) install with it
- Cleaner names for downloaded mods

## 1.1.0 (2026-10-29)
- First full release
- UE4SS for Phantom Blade Zero with one click
- UE4SS updates keep the settings you changed
- Finds the game's exe with another name, as on other stores

## 1.0.1 (2026-10-29)
- Folders of the released game (Steam)
- Save backups
- Locate game folder accepts only the game's folder

## 1.0.0 (2026-10-28)
- First pre-release
