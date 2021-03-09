using System;

namespace KoiSkinOverlayX
{
    /// <summary>
    /// Names are important, don't change! - used for filenames and extended data keys
    /// </summary>
    public enum TexType
    {
        Unknown = 0,
        BodyOver = 1,
        FaceOver = 2,
        BodyUnder = 3,
        FaceUnder = 4,
        [Obsolete]
        EyeUnder = 5,
        [Obsolete]
        EyeOver = 6,
        EyeUnderL = 7,
        EyeOverL = 8,
        EyeUnderR = 9,
        EyeOverR = 10
    }
}