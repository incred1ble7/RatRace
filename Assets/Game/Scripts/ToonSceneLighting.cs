using UnityEngine;

namespace MouseGame
{
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
            c.a = 1f;
            Shader.SetGlobalColor(AmbientId, c);
        }
    }
}
