using UnityEngine;

namespace MouseGame
{
    /// <summary>
    /// Per-level look for the toon shader: the colour of the shaded side (day = warm soft, night = blue).
    /// One global shader value, so it costs nothing at runtime.
    /// </summary>
    [ExecuteAlways]
    public class ToonSceneLighting : MonoBehaviour
    {
        static readonly int AmbientId = Shader.PropertyToID("_ToonAmbient");

        [ColorUsage(false, true)] public Color shadeColor = new Color(0.62f, 0.58f, 0.66f);

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var c = shadeColor;
            c.a = 1f;   // a > 0 tells the shader the value is set
            Shader.SetGlobalColor(AmbientId, c);
        }
    }
}
