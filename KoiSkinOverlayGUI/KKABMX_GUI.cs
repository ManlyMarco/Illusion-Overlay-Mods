using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using KKABMX.Core;
using MakerAPI;
using UniRx;
using UnityEngine;
using Logger = BepInEx.Logger;

namespace KKABMX.GUI
{
    [BepInPlugin("KKABMX.GUI", "KKABMX GUI", KoiSkinOverlay.Version)]
    [BepInDependency(MakerAPI.MakerAPI.GUID)]
    [BepInDependency(KoiSkinOverlay.GUID)]
    public class KKABMX_GUI : BaseUnityPlugin
    {
    }
}