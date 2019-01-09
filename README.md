![preview](https://user-images.githubusercontent.com/39247311/49687441-f5f85880-fb02-11e8-90e9-a5103ca13a51.png)
# KSOX (KoiSkinOverlayX / Overlay mod X)
Koikatu! mod that allows adding overlay textures (also called tattoos) to character's face and body. These additional textures are saved inside the card and used by the main game and studio.

This mod is based on the original KoiSkinOverlay mod by essu and adds saving to cards, GUI, underlays and some other new features.

You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco

## How to use 
1. Make sure [BepInEx](https://github.com/BepInEx/BepInEx) and [BepisPlugins](https://github.com/bbepis/BepisPlugins) are installed, and your game is updated. [MakerAPI](https://github.com/ManlyMarco/MakerAPI) is used for the GUI and you might need to get it as well.
2. Download the latest release from [here](https://github.com/ManlyMarco/KoiSkinOverlayX/releases).
3. Extract the dll files into the folder `Koikatu\BepInEx` in your game's directory.
4. Start character maker, you should see new tab "Overlays" show up under the Body tab.
5. [A full guide on creating overlays available here](Guide/%5BSylvers%5D%20KK%20Overlay%20Tutorial.md).

### Importing old overlays
- Overlays from folders in BepInEx/KoiSkinOverlay with the char's name will be imported on character load. In these folders, "body.png" and/or "face.png" will be loaded. When you save the character, these files will be saved inside the character card and removed from the folder.
- If you downloaded a character with additional overlay files (.png images) you can load the character, go to the "Overlays" tab in maker, and load them there. Once you save the chracter they will be saved inside the character card.
- Old overlay files are of the "overlay", not "underlay" type. If you try to use old-style overlays as underlays, they will look different.

## Changes from KoiSkinOverlay
- Overlays are saved to the cards now (no need to share overlays with the cards now).
- Characters with identical names can have different overlays now.
- Added integrated interface to character maker for managing overlays (Body\Overlays).
- Support for 2 different overlay types: old-type overlays (above everything) and underlays (below tatoos, blushes, etc.). Both types can be used at the same time.
- Ability to load overlays from other cards.
- And more as time goes on...
