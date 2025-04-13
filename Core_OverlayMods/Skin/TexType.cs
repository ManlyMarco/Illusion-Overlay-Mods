namespace KoiSkinOverlayX
{
    // Names and values are important, don't change!
    public enum TexType
    {
        Unknown = 0,
        BodyOver = 1,
        FaceOver = 2,
        BodyUnder = 3,
        FaceUnder = 4,
        /// <summary>
        /// Same as using both EyeUnderL and EyeUnderR
        /// </summary>
        EyeUnder = 5,
        /// <summary>
        /// Same as using both EyeOverL and EyeOverR
        /// </summary>
        EyeOver = 6,
        EyeUnderL = 7,
        EyeOverL = 8,
        EyeUnderR = 9,
        EyeOverR = 10,

        /// <summary>
        /// There's no EyebrowOver because it's effectively the same as FaceOver
        /// </summary>
        EyebrowUnder = 20,
        
        /// <summary>
        /// There's no up/down separation because it's effectively the same texture in KK. Also, there is no up/down separation in HS2 at all.
        /// </summary>
        EyelineUnder = 30,

        /// <summary>
        /// Overlay for DetailMainTex (metallic/gloss) map; applies on top of tattoos
        /// </summary>
        BodyOverGloss = 41,

        /// <summary>
        /// Overlay for DetailMainTex (metallic/gloss) map; applies on top of tattoos, lips, eye shadow and blush
        /// </summary>
        FaceOverGloss = 42,

        /// <summary>
        /// Underlay for DetailMainTex (metallic/gloss) map; applies before tattoos
        /// </summary>
        BodyUnderGloss = 43,

		/// <summary>
		/// Underlay for DetailMainTex (metallic/gloss) map; applies before tattoos, lips, eye shadow and blush
		/// </summary>
		FaceUnderGloss = 44,
    }
}