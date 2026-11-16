using UnityEngine;

namespace Lumon.HubWorld
{
    // The order is the index used by the manifest motion names in HubWorldBuilder. Append only.
    public enum HubMotion
    {
        Static,
        Flame,
        Lantern,
        Banner,
        Tree,
        Glow,
    }

    // Ambient motion of one Hub layer. The neutral pose is stored, not read from the live
    // transform, so the sprite always returns to the exact place it has on the painted plate.
    // Sample(seconds) is a pure function of its argument: the same time gives the same pose.
    //
    // Amplitude meaning (unchanged manifest field, so the builder proof still compares it 1:1):
    //   Tree    plate pixels of the largest horizontal travel at the free tip, gusts included.
    //   Banner  plate pixels, legacy shaping (75 % main sway plus ripple), unchanged.
    //   Lantern degrees of the largest swing, gusts included.
    //   Flame   fraction from 0 to 1 of height change.
    //   Glow    fraction from 0 to 1 of how far the halo dims. It never brightens past the painted alpha.
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HubLayerMotion : MonoBehaviour
    {
        private static readonly int SwayParams = Shader.PropertyToID("_SwayParams");
        private static readonly int SwayRipple = Shader.PropertyToID("_SwayRipple");

        // Raised whenever the per-layer shape defaults change, so an existing scene picks them up once
        // on the next build while tuning done afterwards in the Inspector survives later rebuilds.
        private const int ShapeRevision = 1;

        [SerializeField] private string layerId = "";
        [SerializeField] private HubMotion motion = HubMotion.Static;

        [Tooltip("Tree: plate pixels at the tip. Banner: plate pixels. Lantern: degrees. Flame/Glow: fraction from 0 to 1.")]
        [SerializeField, Min(0f)] private float amplitude;
        [Tooltip("Seconds per cycle.")]
        [SerializeField, Min(0.01f)] private float period = 1f;
        [Tooltip("Radians.")]
        [SerializeField] private float phase;

        [Header("Shape")]
        [Tooltip("Banner: fraction of the height, from the top, that stays fixed. Tree: fraction from the bottom (trunk).")]
        [SerializeField, Range(0f, 0.9f)] private float heldFraction;
        [Tooltip("Banner/Tree: turns of the secondary ripple along the free part.")]
        [SerializeField, Range(0f, 3f)] private float ripple = 1f;
        [Tooltip("Flame: degrees of lean around the base at amplitude 1.")]
        [SerializeField, Range(0f, 30f)] private float flameLean = 10f;
        [Tooltip("Glow: id of the layer this halo hangs from. Empty: the id without 'Glow' (or with 'Flame').")]
        [SerializeField] private string followId = "";
        [Tooltip("Flame, play mode only: small embers rising from the fire and short flame tongues.")]
        [SerializeField] private bool fireDetails = true;
        [SerializeField, HideInInspector] private int shapeRevision;

        [Header("Neutral pose (plate alignment)")]
        [SerializeField] private Vector3 neutralPosition;
        [SerializeField] private Vector3 neutralScale = Vector3.one;
        [SerializeField] private Color neutralColor = Color.white;

        private SpriteRenderer art;
        private MaterialPropertyBlock block;

        [System.NonSerialized] private HubLayerMotion host;
        [System.NonSerialized] private bool hostResolved;

        // Fire details live beside the flame (not under it, so the flame's scale does not reach them)
        // and are never saved. They exist only in play mode, so the edit-mode captures stay inside
        // the flame's own rectangle.
        private const int EmberCount = 7;
        private const int TongueCount = 2;
        [System.NonSerialized] private SpriteRenderer[] embers;
        [System.NonSerialized] private SpriteRenderer[] tongues;
        private static Sprite emberSprite;

        public string LayerId => layerId;
        public HubMotion Motion => motion;
        public float Amplitude => amplitude;
        public float Period => period;
        public float Phase => phase;
        public Vector3 NeutralPosition => neutralPosition;

        // Signed position in the cycle at the given time, -1 to 1. Zero at t = 0 with phase 0.
        public float Wave(float seconds)
        {
            return Mathf.Sin(Angle(seconds));
        }

        // Called by the builder. Shape values are only replaced when the kind of motion changes
        // (or the shape defaults were revised), so tuning done in the Inspector survives a rebuild.
        public void Configure(string id, HubMotion kind, float size, float seconds, float offset, Vector3 localPosition)
        {
            bool changed = kind != motion || layerId != id;
            if (changed || (shapeRevision < ShapeRevision && kind == HubMotion.Tree))
            {
                heldFraction = kind == HubMotion.Tree ? TreeHeld(id) : kind == HubMotion.Banner ? 0.06f : 0f;
                ripple = kind == HubMotion.Tree ? 0.5f : 1f;
            }
            shapeRevision = ShapeRevision;
            layerId = id;
            motion = kind;
            amplitude = Mathf.Max(0f, size);
            period = Mathf.Max(0.01f, seconds);
            phase = offset;
            neutralPosition = localPosition;
            neutralScale = Vector3.one;
            neutralColor = Color.white;
            hostResolved = false;
            ResetNeutral();
        }

        // Fraction of the sprite height, from the bottom, that never moves. The shader weight is
        // ((along - held) / (1 - held))^2, so just above it the travel is still close to zero.
        private static float TreeHeld(string id)
        {
            // The west framing tree hangs into the corner: its trunk runs up the screen edge to about
            // 73 % of the cut-out height. Holding 55 % keeps the trunk under ~0.16 of the tip travel
            // and lets the canopy along the top edge carry the gusts.
            if (id == "FrameTreeWest")
                return 0.55f;
            // The plaza trunk ends at ~29 % of the cut-out height.
            return 0.22f;
        }

        [ContextMenu("Store current transform as neutral pose")]
        private void StoreNeutral()
        {
            neutralPosition = transform.localPosition;
            neutralScale = transform.localScale;
            neutralColor = Art.color;
        }

        [ContextMenu("Return to neutral pose")]
        public void ResetNeutral()
        {
            transform.localPosition = neutralPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = neutralScale;
            Art.color = neutralColor;
            SetSway(0f, 0f);
            HideFireDetails();
        }

        // ------------------------------------------------------------ shared, pure curves

        // Wind gust strength 0..1, shared by every layer so the trees and lanterns answer the same gust.
        // Zero at t = 0; one strong gust roughly every 11 s with uneven strength.
        public static float Gust(float seconds)
        {
            float swell = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * seconds / 11.3f);
            float bias = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * seconds / 4.7f + 1.3f);
            return swell * swell * (0.55f + 0.45f * bias);
        }

        // Lantern swing in degrees, never beyond the amplitude. Zero at t = 0 with phase 0.
        public float SwingDegrees(float seconds)
        {
            if (motion != HubMotion.Lantern)
                return 0f;
            float angle = Angle(seconds);
            float gust = Gust(seconds - 0.15f * phase);
            // A pendulum is close to a pure sine. The gust widens the swing and leans it downwind.
            float swing = (0.7f + 0.18f * gust) * Mathf.Sin(angle) + 0.12f * gust * Mathf.Sin(0.5f * angle) * Mathf.Sin(0.5f * angle);
            return amplitude * swing;
        }

        // Flame height change -1..1. Zero at t = 0 with phase 0.
        public float Heat(float seconds)
        {
            if (motion != HubMotion.Flame)
                return 0f;
            float a = Angle(seconds);
            return 0.55f * Mathf.Sin(a) + 0.3f * Mathf.Sin(2.7f * a) + 0.15f * Mathf.Sin(6.1f * a);
        }

        public void Sample(float seconds)
        {
            float angle = Angle(seconds);
            float wave = Mathf.Sin(angle);
            Vector3 position = neutralPosition;
            Quaternion rotation = Quaternion.identity;
            Vector3 scale = neutralScale;
            Color color = neutralColor;
            float sway = 0f, swayRipple = 0f;

            switch (motion)
            {
                case HubMotion.Flame:
                {
                    // The sprite pivot is the base of the flame, so scale and lean leave it in place.
                    // Height and width use different frequency mixes, so the silhouette does not just
                    // breathe: it stretches thin, squats wide and leans on its own beat.
                    float size = Mathf.Clamp01(amplitude);
                    float height = Heat(seconds);
                    float width = 0.6f * height + 0.4f * (0.5f * Mathf.Sin(1.9f * angle) + 0.5f * Mathf.Sin(3.7f * angle));
                    scale = new Vector3(neutralScale.x * (1f - 0.4f * size * width),
                        neutralScale.y * (1f + size * height), neutralScale.z);
                    float lean = 0.6f * Mathf.Sin(1.3f * angle) + 0.4f * Mathf.Sin(3.1f * angle);
                    rotation = Quaternion.Euler(0f, 0f, flameLean * size * (lean - 0.35f * Gust(seconds) * Mathf.Abs(lean)));
                    // Soft heat: when the flame drops it cools a little toward red. Never brighter than painted.
                    float cool = Mathf.Max(0f, -height);
                    color.g *= 1f - 0.07f * cool;
                    color.b *= 1f - 0.16f * cool;
                    SampleFireDetails(seconds, angle);
                    break;
                }
                case HubMotion.Lantern:
                    // Rigid swing around the pivot, which is the top of the chain.
                    rotation = Quaternion.Euler(0f, 0f, SwingDegrees(seconds));
                    break;
                case HubMotion.Banner:
                {
                    Sprite sprite = Art.sprite;
                    float width = sprite != null ? sprite.rect.width : 0f;
                    if (width > 0f)
                    {
                        float reach = amplitude / width;
                        sway = 0.75f * reach * wave;
                        swayRipple = 0.25f * reach * Mathf.Sin(2f * angle);
                    }
                    break;
                }
                case HubMotion.Tree:
                {
                    Sprite sprite = Art.sprite;
                    float width = sprite != null ? sprite.rect.width : 0f;
                    if (width > 0f)
                    {
                        // Calm: about 45 % of the amplitude. In a gust the crown pushes downwind to the
                        // full amplitude and springs back. |main| <= 1 and |ripple| <= 0.3 keep the
                        // tip, and every row below it, within the amplitude (shader weight <= 1).
                        float reach = amplitude / width;
                        float gust = Gust(seconds - 0.15f * phase);
                        float main = (0.55f + 0.3f * gust) * wave + 0.15f * gust * Mathf.Sin(2.6f * angle)
                            + 0.25f * gust * (0.5f - 0.5f * Mathf.Cos(angle));
                        sway = reach * main / 1.25f;
                        swayRipple = reach * (0.18f + 0.12f * gust) * Mathf.Sin(2.3f * angle + 0.7f) * Mathf.Abs(wave);
                    }
                    break;
                }
                case HubMotion.Glow:
                {
                    // Several incommensurate dims instead of one slow fade, so the halo flickers softly.
                    // Each term is 0..1 and zero at angle 0; their weights add up to 1.
                    float flicker = 0.45f * Dip(angle) + 0.35f * Dip(2.9f * angle) + 0.2f * Dip(6.7f * angle);
                    HubLayerMotion source = Host;
                    if (source != null && source.motion == HubMotion.Lantern)
                    {
                        // The halo hangs from the lantern: same pivot, same angle.
                        float degrees = source.SwingDegrees(seconds);
                        rotation = Quaternion.Euler(0f, 0f, degrees);
                        Vector3 arm = neutralPosition - source.neutralPosition;
                        Vector3 swung = rotation * new Vector3(arm.x, arm.y, 0f);
                        position = new Vector3(source.neutralPosition.x + swung.x, source.neutralPosition.y + swung.y,
                            neutralPosition.z);
                        // A swinging flame flares a little at the ends of the arc.
                        float tilt = source.amplitude > 0f ? Mathf.Abs(degrees) / source.amplitude : 0f;
                        flicker = Mathf.Clamp01(flicker * (1f - 0.35f * tilt));
                    }
                    else if (source != null && source.motion == HubMotion.Flame)
                    {
                        // The fire light rises and falls with the flame.
                        float heat = source.Heat(seconds);
                        flicker = Mathf.Clamp01(0.55f * Mathf.Max(0f, -heat) + 0.45f * flicker);
                    }
                    float dim = Mathf.Clamp01(amplitude) * flicker;
                    color.a = neutralColor.a * (1f - dim);
                    // The halo pulls in slightly as it dims; its pivot is its centre.
                    scale = neutralScale * (1f - 0.12f * dim);
                    break;
                }
            }

            transform.localPosition = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
            Art.color = color;
            SetSway(sway, swayRipple);
        }

        private static float Dip(float angle)
        {
            return 0.5f - 0.5f * Mathf.Cos(angle);
        }

        private SpriteRenderer Art => art != null ? art : art = GetComponent<SpriteRenderer>();

        private float Angle(float seconds)
        {
            return 2f * Mathf.PI * seconds / Mathf.Max(0.01f, period) + phase;
        }

        // The layer a halo follows, found by id among the stage's layers. Resolved once.
        private HubLayerMotion Host
        {
            get
            {
                if (hostResolved && (host == null || host.transform.parent == transform.parent))
                    return host;
                hostResolved = true;
                host = null;
                Transform stage = transform.parent;
                if (stage == null)
                    return null;
                string wanted = followId;
                string fallback = null;
                if (string.IsNullOrEmpty(wanted) && layerId.EndsWith("Glow"))
                {
                    wanted = layerId.Substring(0, layerId.Length - 4);
                    fallback = wanted + "Flame";
                }
                if (string.IsNullOrEmpty(wanted))
                    return null;
                HubLayerMotion second = null;
                foreach (HubLayerMotion candidate in stage.GetComponentsInChildren<HubLayerMotion>(true))
                {
                    if (candidate == this || candidate.transform.parent != stage)
                        continue;
                    if (string.Equals(candidate.layerId, wanted, System.StringComparison.OrdinalIgnoreCase))
                        host = candidate;
                    else if (fallback != null && string.Equals(candidate.layerId, fallback, System.StringComparison.OrdinalIgnoreCase))
                        second = candidate;
                }
                if (host == null)
                    host = second;
                return host;
            }
        }

        private void SetSway(float main, float secondary)
        {
            if (block == null)
                block = new MaterialPropertyBlock();
            Art.GetPropertyBlock(block);
            block.SetVector(SwayParams, new Vector4(main, secondary, heldFraction, motion == HubMotion.Banner ? 1f : 0f));
            block.SetFloat(SwayRipple, ripple);
            Art.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------ fire details (play mode)

        private static float Hash(int i, int k)
        {
            float v = Mathf.Sin(i * 12.9898f + k * 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        private static Sprite EmberSprite(float pixelsPerUnit)
        {
            if (emberSprite != null)
                return emberSprite;
            // A 4 px soft dot, white so the renderer colour carries the heat.
            const int size = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f), dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            emberSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            emberSprite.hideFlags = HideFlags.HideAndDontSave;
            return emberSprite;
        }

        private void CreateFireDetails()
        {
            if (!Application.isPlaying || !fireDetails || motion != HubMotion.Flame || embers != null)
                return;
            Transform stage = transform.parent;
            Sprite flame = Art.sprite;
            if (stage == null || flame == null)
                return;
            Sprite dot = EmberSprite(flame.pixelsPerUnit);
            embers = new SpriteRenderer[EmberCount];
            for (int i = 0; i < EmberCount; i++)
                embers[i] = Detail($"{name}.Ember{i}", stage, dot);
            tongues = new SpriteRenderer[TongueCount];
            for (int i = 0; i < TongueCount; i++)
                tongues[i] = Detail($"{name}.Tongue{i}", stage, flame);
        }

        private SpriteRenderer Detail(string label, Transform stage, Sprite sprite)
        {
            var go = new GameObject(label) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(stage, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = Art.sharedMaterial;
            renderer.sortingLayerID = Art.sortingLayerID;
            renderer.enabled = false;
            return renderer;
        }

        private void DestroyFireDetails()
        {
            DestroyAll(embers);
            DestroyAll(tongues);
            embers = tongues = null;
        }

        private static void DestroyAll(SpriteRenderer[] items)
        {
            if (items == null)
                return;
            foreach (SpriteRenderer item in items)
                if (item != null)
                {
                    if (Application.isPlaying)
                        Destroy(item.gameObject);
                    else
                        DestroyImmediate(item.gameObject);
                }
        }

        private void HideFireDetails()
        {
            if (embers != null)
                foreach (SpriteRenderer item in embers)
                    if (item != null)
                        item.enabled = false;
            if (tongues != null)
                foreach (SpriteRenderer item in tongues)
                    if (item != null)
                        item.enabled = false;
        }

        private void SyncDetailOrder()
        {
            // HubDepthSort moves the flame in front of or behind the guardian; the details follow it.
            int order = Art.sortingOrder;
            if (tongues != null)
                foreach (SpriteRenderer item in tongues)
                    if (item != null)
                        item.sortingOrder = order;
            if (embers != null)
                foreach (SpriteRenderer item in embers)
                    if (item != null)
                        item.sortingOrder = order + 1;
        }

        // All positions are plate pixels from the flame's base pivot, converted with the sprite's
        // pixels per unit in the stage's local space. Embers rise at most ~55 px, which keeps them
        // inside the flame rectangle plus the review margin.
        private void SampleFireDetails(float seconds, float angle)
        {
            if (embers == null || tongues == null)
                return;
            Sprite flame = Art.sprite;
            if (flame == null)
                return;
            float unit = 1f / flame.pixelsPerUnit;
            float gust = Gust(seconds);
            float size = Mathf.Clamp01(amplitude);
            Color tint = neutralColor;

            for (int i = 0; i < embers.Length; i++)
            {
                SpriteRenderer ember = embers[i];
                if (ember == null)
                    continue;
                float life = 1.5f + 0.9f * Hash(i, 0);
                float t = seconds / life + Hash(i, 1);
                float u = t - Mathf.Floor(t);
                float startX = (Hash(i, 2) - 0.5f) * 22f;
                float startY = 8f + 6f * Hash(i, 3);
                float rise = 30f + 14f * Hash(i, 4);
                float x = startX * (1f - 0.35f * u)
                    + 3.5f * u * Mathf.Sin(2f * Mathf.PI * (u * (0.8f + 0.5f * Hash(i, 5)) + Hash(i, 6)))
                    + 5f * gust * u * u;
                float y = startY + rise * u * (1.15f - 0.15f * u);
                float fade = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.3f) * (0.5f + 0.4f * Hash(i, 7));
                fade *= 0.78f + 0.22f * Mathf.Sin(37f * u + 6.3f * Hash(i, 8));
                float scale = (0.4f + 0.3f * Hash(i, 9)) * (1f - 0.45f * u);
                ember.transform.localPosition = new Vector3(neutralPosition.x + x * unit, neutralPosition.y + y * unit,
                    neutralPosition.z - 0.001f);
                ember.transform.localRotation = Quaternion.identity;
                ember.transform.localScale = new Vector3(scale, scale, 1f);
                ember.color = new Color(1f, Mathf.Lerp(0.82f, 0.48f, u), Mathf.Lerp(0.46f, 0.2f, u), Mathf.Clamp01(fade));
                ember.enabled = true;
            }

            for (int i = 0; i < tongues.Length; i++)
            {
                SpriteRenderer tongue = tongues[i];
                if (tongue == null)
                    continue;
                // A narrow copy of the flame that licks up beside the core for a moment, then fades.
                float side = i == 0 ? -1f : 1f;
                float pulse = Dip(1.7f * angle + 2.1f * i + 0.9f);
                pulse = pulse * pulse * pulse;
                float stretch = 0.8f + 0.28f * pulse + 0.4f * size * Heat(seconds + 0.13f * (i + 1));
                tongue.transform.localPosition = new Vector3(neutralPosition.x + side * (3f + 2f * pulse) * unit,
                    neutralPosition.y, neutralPosition.z - 0.0005f);
                tongue.transform.localRotation = Quaternion.Euler(0f, 0f, -side * (7f + 7f * pulse) - 4f * gust);
                tongue.transform.localScale = new Vector3(neutralScale.x * 0.45f, neutralScale.y * stretch, neutralScale.z);
                tongue.color = new Color(tint.r, tint.g, tint.b, tint.a * 0.55f * pulse);
                tongue.enabled = pulse > 0.01f;
            }
            SyncDetailOrder();
        }

        // ------------------------------------------------------------ lifecycle

        private void OnEnable()
        {
            hostResolved = false;
            CreateFireDetails();
        }

        private void Update()
        {
            if (motion != HubMotion.Static)
                Sample(Time.timeSinceLevelLoad);
        }

        private void LateUpdate()
        {
            if (embers != null)
                SyncDetailOrder();
        }

        private void OnDisable()
        {
            ResetNeutral();
            DestroyFireDetails();
        }
    }
}
