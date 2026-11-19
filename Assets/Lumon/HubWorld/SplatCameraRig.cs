using UnityEngine;
using GaussianSplatting.Runtime;

namespace Lumon.HubWorld
{
    public sealed class SplatCameraRig : MonoBehaviour
    {
        private const float MinimumPitch = 24f;
        private const float MaximumPitch = 62f;
        private const float FramingStartPitch = 45f;
        private const float HighPitchPadding = 0.34f;

        [SerializeField] private Camera view;
        [SerializeField] private HubSplatWorld world;
        [SerializeField] private Vector3 target = new(0, 0.35f, 0);
        [SerializeField] private float yaw = 32f;
        [SerializeField] private float pitch = 48f;
        [SerializeField] private float distance = 14f;
        [SerializeField] private float orthographicSize = 5.65f;
        private Vector3 lastMouse;
        private bool dragging;
        private float homeYaw;
        private float homePitch;
        private SplatStationSpot inspected;
        [SerializeField] private GaussianSplatRenderer[] foreground = System.Array.Empty<GaussianSplatRenderer>();

        private void Awake()
        {
            homeYaw = yaw;
            homePitch = pitch;
        }

        public float Yaw => yaw;
        public float Pitch => pitch;
        private bool overview;
        public bool Overview => overview;
        public bool Inspecting => inspected != null;

        public void Configure(Camera camera, Vector3 lookAt, float heading, float elevation, float size)
        {
            view = camera;
            target = lookAt;
            yaw = homeYaw = heading;
            pitch = homePitch = elevation;
            orthographicSize = size;
            Apply();
        }

        public void Orbit(float degrees, float elevationChange = 0f)
        {
            yaw = Mathf.Repeat(yaw + degrees, 360f);
            pitch = Mathf.Clamp(pitch + elevationChange, MinimumPitch, MaximumPitch);
            Apply();
        }

        public void ResetView()
        {
            inspected = null;
            yaw = homeYaw;
            pitch = homePitch;
            Apply();
        }

        public void ConfigureInput(HubSplatWorld hub, GaussianSplatRenderer[] foregroundObjects)
        {
            world = hub;
            foreground = foregroundObjects;
        }

        private void Update()
        {
            if (inspected != null && world != null && inspected.id != world.Nearby)
            {
                inspected = null;
                Apply();
            }
            if (world != null && world.InputPaused)
            {
                dragging = false;
                return;
            }
            if (HubQaInput.GetKeyDown(KeyCode.V)) { overview = !overview; inspected = null; Apply(); }
            if (HubQaInput.GetKey(KeyCode.Q)) Orbit(-48f * Time.deltaTime);
            if (HubQaInput.GetKeyDown(KeyCode.F) && world != null && world.Nearby == LumonStationId.Improve)
            {
                inspected = inspected == null ? world.Spot(world.Nearby) : null;
                Apply();
            }
            if (HubQaInput.GetKey(KeyCode.R)) Orbit(48f * Time.deltaTime);
            if (HubQaInput.GetMouseButtonDown(1)) { dragging = true; lastMouse = HubQaInput.MousePosition; }
            if (HubQaInput.GetMouseButtonUp(1)) dragging = false;
            if (!dragging) return;
            Vector3 now = HubQaInput.MousePosition;
            Vector3 delta = now - lastMouse;
            lastMouse = now;
            Orbit(delta.x * 0.24f, -delta.y * 0.12f);
        }

        private void LateUpdate()
        {
            Apply();
            foreach (var item in foreground)
            {
                if (item == null) continue;
                bool blocked = false;
                if (inspected != null && item.HasValidRenderSetup)
                {
                    Vector3 stage = inspected.position + new Vector3(-.5f,.7f,-.75f);
                    for (int i=-1; i<=1 && !blocked; i++)
                    {
                        Vector3 end = item.transform.InverseTransformPoint(stage + view.transform.right * (i * .83f));
                        Vector3 start = end - item.transform.InverseTransformVector(view.transform.forward * distance);
                        Vector3 delta = end - start;
                        blocked = item.localBounds.IntersectRay(new Ray(start,delta.normalized),out float enter) && enter < delta.magnitude;
                    }
                }
                item.m_OpacityScale = Mathf.MoveTowards(item.m_OpacityScale, blocked ? 0f : 1f, 12f * Time.unscaledDeltaTime);
            }
        }

        private void Apply()
        {
            if (view == null) return;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 center = inspected == null ? target : inspected.position + new Vector3(-0.52f, 0.89f, -0.48f);
            if (Application.isPlaying && !overview && inspected == null && world != null) center = world.GuardianPosition + Vector3.up * 2.68f;
            view.transform.SetPositionAndRotation(center - rotation * Vector3.forward * (overview ? 23f : distance), rotation);
            view.orthographic = true;
            float highPitch = Mathf.InverseLerp(FramingStartPitch, MaximumPitch, pitch);
            view.orthographicSize = inspected == null ? (overview ? Mathf.Max(orthographicSize, 10.7f / Mathf.Max(0.5f, view.aspect)) : 4.35f) + Mathf.SmoothStep(0f, HighPitchPadding, highPitch) : 1.64f;
        }
    }
}
