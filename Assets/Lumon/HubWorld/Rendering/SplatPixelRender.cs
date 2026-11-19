using UnityEngine;

namespace Lumon.HubWorld
{
    [RequireComponent(typeof(Camera))]
    public sealed class SplatPixelRender : MonoBehaviour
    {
        [SerializeField, Range(1, 6)] private int pixelSize = 2;
        [SerializeField, Range(0, 1)] private float dither = 0.24f;
        [SerializeField] private bool paintSky;
        private Material material;
        private Camera view;

        internal int PixelSize => pixelSize;

        private void Awake() => view = GetComponent<Camera>();

        public void Configure(int size, bool withSky = false)
        {
            pixelSize = Mathf.Clamp(size, 1, 6);
            paintSky = withSky;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>("LumonPixelPass");
                if (shader == null || !shader.isSupported)
                {
                    Graphics.Blit(source, destination);
                    return;
                }
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                if (paintSky) material.SetTexture("_DawnValley", Resources.Load<Texture2D>("DawnValley"));
            }

            int width = Mathf.Max(1, source.width / pixelSize);
            int height = Mathf.Max(1, source.height / pixelSize);
            RenderTexture low = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                low.filterMode = FilterMode.Point;
                material.SetFloat("_Dither", dither);
                material.SetFloat("_PaintSky", paintSky ? 1f : 0f);
                if (paintSky)
                {
                    if (view == null) view = GetComponent<Camera>();
                    material.SetColor("_ClearColor", view.backgroundColor);
                    Vector3 angle = view.transform.eulerAngles;
                    material.SetVector("_ValleyView", new Vector4(0.45f + (angle.y - 32f) / 180f,
                        Mathf.Clamp(view.aspect / 3f, 0.30f, 0.95f), (32f - angle.x) / 180f, 0f));
                }
                Graphics.Blit(source, low, material);
                Graphics.Blit(low, destination);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(low);
            }
        }

        private void OnDisable()
        {
            if (material != null)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            material = null;
        }
    }
}
