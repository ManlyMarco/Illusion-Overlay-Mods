![preview 1](https://user-images.githubusercontent.com/39247311/52307982-6bb26080-299c-11e9-9a64-99ede143fb6d.png)
# Koikatsu Overlay Mods - formerely KSOX (KoiSkinOverlayX) and KCOX (KoiClothesOverlayX)
Mod that allows adding overlay textures (tattoos) to character's face, body and clothes in Koikatu! and EmotionCreators. These additional textures are saved inside the card and used by the main game and studio.

## How to use 
1. Make sure that the [latest KKAPI/ECAPI](https://github.com/ManlyMarco/KKAPI) (depending on your game) is installed, and your game is updated.
2. Download the latest release from [here](https://github.com/ManlyMarco/Koikatu-Overlay-Mods/releases).
3. Place the dll file(s) into the folder `Koikatu\BepInEx` in your game's directory in case of Koikatsu version, or inside `BepInEx\plugins` in case of EmotionCreators version. You only need the version specific for your game.
4. Start character maker. You should see new tab "Overlays" show up under the Body tab, and overlay controls under clothes tabs.
5. [A full guide on creating overlays available here](Guide/%5BSylvers%5D%20KK%20Overlay%20Tutorial.md).

### Importing old overlays (only Koikatsu version)
- Overlays from folders in BepInEx/KoiSkinOverlay with the char's name will be imported on character load. In these folders, "body.png" and/or "face.png" will be loaded. When you save the character, these files will be saved inside the character card and removed from the folder.
- If you downloaded a character with additional overlay files (.png images) you can load the character, go to the "Overlays" tab in maker, and load them there. Once you save the chracter they will be saved inside the character card.
- Old overlay files are of the "overlay", not "underlay" type. If you try to use old-style overlays as underlays, they will look different.

## Changes from KoiSkinOverlay (the precursor to this mod)
This mod is based on the original KoiSkinOverlay mod by essu and many new features.
- Overlays are saved to the cards now (no need to share overlays with the cards now).
- Characters with identical names can have different overlays now.
- Added integrated interface to character maker for managing overlays (Body\Overlays).
- Support for 2 different overlay types: old-type overlays (above everything) and underlays (below tatoos, blushes, etc.). Both types can be used at the same time.
- Ability to load overlays from other cards.
- Added clothes overlays. It allows adding overlays to textures of almost all clothes.
- And more as time goes on...

![preview 2](https://user-images.githubusercontent.com/39247311/52307974-66551600-299c-11e9-8a8c-183006541530.png)
![preview 3](https://user-images.githubusercontent.com/39247311/49687441-f5f85880-fb02-11e8-90e9-a5103ca13a51.png)
![Eye overlay preview](https://user-images.githubusercontent.com/39247311/52975293-41fa3000-33c5-11e9-9735-07b25613520d.png)
Preview pictures 1 and 2 by @DeathWeasel1337. Character in picture 3 by HCM06, in picture 4 by Yata.
