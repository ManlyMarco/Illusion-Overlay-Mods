namespace KoiSkinOverlayX
{
    /// <summary>
    /// Names are important, don't change! - used for filenames and extended data keys
    /// </summary>
    public enum TexType
    {
        Unknown = 0,
        BodyOver,
        FaceOver,
        BodyUnder,
        FaceUnder,
        #if KK || EC
        EyeUnder, //todo eyes in ai/hs2
        EyeOver
        #endif
    }
}