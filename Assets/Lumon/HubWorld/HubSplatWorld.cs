using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumon.HubWorld
{
    public enum LumonStationId { None, Team, Nest, EggShop, Fusion, Improve, Expeditions, Settings, Party, WestScope, EastScope }

    [Serializable]
    public sealed class SplatStationSpot
    {
        public LumonStationId id;
        public string label;
        public Vector3 position;
        public Vector3 approach;
        public float radius = 0.8f;
        public float obstacle = 0.8f;
    }

    [Serializable]
    public sealed class SplatObstacle
    {
        public Vector3 position;
        public float radius;
        public Vector3 axis;
        public float halfSpan;
    }

    public sealed class HubSplatWorld : MonoBehaviour
    {
        private const float MaxCollisionStep = 0.16f;

        [SerializeField] private SplatGuardian guardian;
        [SerializeField] private Camera view;
        [SerializeField] private SplatStationSpot[] spots = Array.Empty<SplatStationSpot>();
        [SerializeField] private SplatObstacle[] obstacles = Array.Empty<SplatObstacle>();
        [SerializeField] private float worldRadius = 4.65f;
        private readonly HashSet<object> pauses = new();

        public event Action<LumonStationId> StationRequested;
        public event Action<LumonStationId> NearbyChanged;
        public Func<Vector2, bool> PointerOverInterface { get; set; }
        public LumonStationId Nearby { get; private set; }
        public bool InputPaused => pauses.Count > 0;
        internal SplatGuardian LocalGuardian => guardian;
        public bool ReducedMotion { get; set; }
        public IReadOnlyList<SplatStationSpot> Spots => spots;
        public Vector3 GuardianPosition => guardian != null ? guardian.transform.position : Vector3.zero;

        public void Configure(SplatGuardian actor, Camera camera, SplatStationSpot[] stations, SplatObstacle[] blocks, float radius)
        {
            guardian = actor;
            view = camera;
            spots = stations;
            obstacles = blocks;
            worldRadius = radius;
        }

        public SplatStationSpot Spot(LumonStationId id)
        {
            foreach (SplatStationSpot spot in spots) if (spot.id == id) return spot;
            return null;
        }

        public void SetPaused(object owner, bool paused)
        {
            if (owner == null) return;
            if (paused) pauses.Add(owner); else pauses.Remove(owner);
        }

        public bool Request(LumonStationId id)
        {
            SplatStationSpot spot = Spot(id);
            if (spot == null || InputPaused || (guardian.transform.position - spot.approach).sqrMagnitude > spot.radius * spot.radius) return false;
            StationRequested?.Invoke(id);
            return true;
        }

        public Vector3 Constrain(Vector3 from, Vector3 target)
        {
            Vector2 p = new(from.x, from.z);
            Vector2 delta = new(target.x - from.x, target.z - from.z);
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / MaxCollisionStep));
            Vector2 step = delta / steps;
            for (int i = 0; i < steps; i++) p = ConstrainStep(p + step);
            return new Vector3(p.x, target.y, p.y);
        }

        private Vector2 ConstrainStep(Vector2 p)
        {
            if (p.magnitude > worldRadius) p = p.normalized * worldRadius;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (SplatStationSpot spot in spots) p = Outside(p, new Vector2(spot.position.x, spot.position.z), spot.obstacle);
                foreach (SplatObstacle obstacle in obstacles)
                {
                    Vector2 center = new(obstacle.position.x, obstacle.position.z);
                    if (obstacle.halfSpan <= 0f)
                    {
                        p = Outside(p, center, obstacle.radius);
                        continue;
                    }
                    Vector2 axis = new(obstacle.axis.x, obstacle.axis.z);
                    float along = Mathf.Clamp(Vector2.Dot(p - center, axis), -obstacle.halfSpan, obstacle.halfSpan);
                    Vector2 nearest = center + along * axis;
                    p = Outside(p, nearest, obstacle.radius, new Vector2(-axis.y, axis.x));
                }
            }
            return p;
        }

        private static Vector2 Outside(Vector2 point, Vector2 center, float radius, Vector2 fallback = default)
        {
            Vector2 delta = point - center;
            if (delta.sqrMagnitude >= radius * radius) return point;
            if (delta.sqrMagnitude >= 0.0001f) return center + delta.normalized * radius;
            return center + (fallback == Vector2.zero ? Vector2.up : fallback) * radius;
        }

        private void Update()
        {
            RefreshNearby();
            if (InputPaused || guardian == null) return;
            if (HubQaInput.GetKeyDown(KeyCode.E)) Request(Nearby);
            if (HubQaInput.GetMouseButtonDown(0)) Click(HubQaInput.MousePosition);
        }

        private void RefreshNearby()
        {
            LumonStationId nearest = LumonStationId.None;
            float best = float.MaxValue;
            if (guardian != null)
            {
                foreach (SplatStationSpot spot in spots)
                {
                    float distance = (guardian.transform.position - spot.approach).sqrMagnitude;
                    if (distance <= spot.radius * spot.radius && distance < best) { best = distance; nearest = spot.id; }
                }
            }
            if (nearest == Nearby) return;
            Nearby = nearest;
            NearbyChanged?.Invoke(nearest);
        }

        private void Click(Vector2 screen)
        {
            if (view == null || (PointerOverInterface != null && PointerOverInterface(screen))) return;
            Ray ray = view.ScreenPointToRay(screen);
            Plane ground = new(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float enter)) return;
            Vector3 point = ray.GetPoint(enter);
            foreach (SplatStationSpot spot in spots)
                if ((point - spot.position).sqrMagnitude <= 1.1f * 1.1f) { Request(spot.id); return; }
        }
    }
}
