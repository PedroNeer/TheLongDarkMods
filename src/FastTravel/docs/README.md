**Fast Travel** is a [The Long Dark] survival mode mod that lets you save places (like your home
base), and fast travel to them anytime through a destination list or shortcut key.

For example, you can have one home base while you explore the world without the tedium of
transferring your hoard to each region.

> ![](images/save-location.png)<br />![](images/travel.png)

> [!WARNING]  
> **This mod is still experimental.**  
> I strongly recommend [keeping save backups](../../SaveBackup#readme) when using this mod.

## Contents
* [Install](#install)
* [Use](#use)
* [Configure](#configure)
* [Compatibility](#compatibility)
* [Security](#security)
* [See also](#see-also)

## Install
1. Install [MelonLoader], [ModData][TLDMods], and [ModSettings][TLDMods].
2. [Download this mod][mod page] directly into your game's `Mods` subfolder.
3. Launch the game.

You can [edit the mod settings](#configure) to choose when you can fast travel.

## Use
You can save any number of fast travel destinations. Each one can be bound to a shortcut key (by
default numpad 1 through 9); you can change all the shortcut keys in the [mod options](#configure)
if needed.

To use fast travel (with the default options):
- Open the destination list by pressing `[numpad period]`, then choose where to travel.
- Travel to the destination bound to a shortcut key by pressing that key.
- Save your current position as a new fast travel destination by pressing `[numpad +]` + destination
  key. If that key already had a destination, the old destination stays in your list but loses the
  shortcut.
- Delete a saved fast travel destination by pressing `[numpad -]` + destination key, or delete the
  selected destination from the destination list.
- Rename a destination by opening the destination list, selecting it, and pressing `[R]`. Leave the
  name empty to use the default location name.
- Change the destination list text size by pressing `[` or `]` until the displayed scale feels
  comfortable.
- Rebind a destination by opening the destination list, selecting it, pressing `[numpad +]`, then
  pressing the new destination key.
- Return to where you were before your _most recent_ fast travel by pressing `[numpad 0]`.

The mod always asks for confirmation, so you can't fast travel or change your saved destinations by
mistake.

You can fast travel freely by default. You can change that in the [mod options](#configure).

## Configure
From the game's Options menu, click "Mod Settings" and then navigate to "Fast Travel". Point the
cursor at any field to see an explanation on the right.

> ![](images/config.png)

## Compatibility
- Compatible with The Long Dark 2.50+ (including 2.55) and MelonLoader 0.7.2+.
- For **survival mode only**. Wintermute's story triggers are very fragile; you shouldn't use fast
  travel in Wintermute even if you get it to work.

Pairs well with [Save Backup](../SaveBackup) in case of any issue.

## Security
This mod is fully open-source. All its source code is public in this repository, so anyone can
verify that it's not doing anything malicious.

Each release also has a [public attestation][GitHub attestations], an unfalsifiable record which
proves exactly how the release file was created. That lets anyone verify that it _only_ contains
this code, and hasn't been modified in any way.

## See also
* [Release notes](release-notes.md)
* [Nexus mod][mod page]

[mod page]: https://www.nexusmods.com/thelongdark/mods/54

[GitHub attestations]: https://docs.github.com/en/actions/concepts/security/artifact-attestations
[MelonLoader]: https://tldmods.net/install.html
[TLDMods]: https://tldmods.net
[The Long Dark]: https://www.thelongdark.com
