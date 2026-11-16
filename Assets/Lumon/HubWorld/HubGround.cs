using System;
using System.Collections.Generic;
using Lumon.GuardianReview;
using UnityEngine;

namespace Lumon.HubWorld
{
    // The floor of the Hub. The outlines are authored in pixels of the painted plate (origin at
    // the top-left corner) and describe where the guardian's feet may be: inside the walkable
    // outline and outside every obstacle. Bind() projects them once through the fixed camera
    // onto the ground plane and hands the guardian this floor in place of her review rectangle.
    public sealed class HubGround : MonoBehaviour, GuardianMotion.IGround
    {
        [Serializable]
        public sealed class Outline
        {
            public string name = "";
            public Vector2[] points = Array.Empty<Vector2>();
        }

        // Ground units. A long frame is walked in pieces this short, so it bends around corners
        // the way a run of short frames would. About 4.6 plate px sideways, 2.3 in depth.
        private const float StepLength = 0.04f;
        // Clearance kept from every edge, so rounding can never leave the feet on the far side.
        private const float Skin = 0.004f;

        [SerializeField] private HubStage stage;
        [SerializeField] private GuardianMotion guardian;
        [Tooltip("Plate pixels. Where the feet may be.")]
        [SerializeField] private Vector2[] walkable = Array.Empty<Vector2>();
        [Tooltip("Plate pixels, foot margin included. Where the feet may not be.")]
        [SerializeField] private Outline[] obstacles = Array.Empty<Outline>();

        private readonly HashSet<object> pauses = new HashSet<object>();
        private Vector2[][] loops = Array.Empty<Vector2[]>();
        private Vector2 plateSize;
        private float pixelsPerUnit;
        private float groundHeight;

        public bool Ready { get; private set; }
        public HubStage Stage => stage;
        public GuardianMotion Guardian => guardian;
        public IReadOnlyList<Vector2> Walkable => walkable;
        public IReadOnlyList<Outline> Obstacles => obstacles;

        // Where the guardian's feet are on the painted plate.
        public Vector2 FeetPlate => WorldToPlate(guardian.transform.position);

        public bool InputPaused
        {
            get
            {
                // A modal that was destroyed without resuming must not freeze the world.
                pauses.RemoveWhere(owner => owner is UnityEngine.Object item && item == null);
                return pauses.Count > 0;
            }
        }

        // Each owner pauses and resumes for itself, so stacked modals do not undo each other.
        public void SetInputPaused(object owner, bool paused)
        {
            if (owner == null)
                return;
            if (paused)
                pauses.Add(owner);
            else
                pauses.Remove(owner);
        }

        public void Configure(HubStage plate, GuardianMotion actor, Vector2[] floor, Outline[] blocked)
        {
            stage = plate;
            guardian = actor;
            walkable = floor;
            obstacles = blocked;
            Ready = false;
        }

        // Called on enable, and by the editor check, which runs without Play Mode.
        public bool Bind()
        {
            Release();
            if (stage == null)
                stage = InScene<HubStage>();
            if (guardian == null)
                guardian = InScene<GuardianMotion>();
            if (stage == null || guardian == null || walkable == null || walkable.Length < 3)
                return false;
            Sprite plate = stage.BaseRenderer != null ? stage.BaseRenderer.sprite : null;
            plateSize = stage.PlateSize;
            // The view must look down on the ground, or plate pixels have no place on it.
            if (plate == null || plateSize.x <= 0f || plateSize.y <= 0f || Mathf.Abs(stage.transform.forward.y) < 0.01f)
                return false;
            pixelsPerUnit = plate.pixelsPerUnit;
            groundHeight = guardian.transform.position.y;

            var baked = new List<Vector2[]> { Bake(walkable) };
            if (obstacles != null)
                foreach (Outline obstacle in obstacles)
                    if (obstacle != null && obstacle.points != null && obstacle.points.Length >= 3)
                        baked.Add(Bake(obstacle.points));
            loops = baked.ToArray();
            Ready = true;
            guardian.Ground = this;
            return true;
        }

        // Gives the guardian back her own limits.
        public void Release()
        {
            Ready = false;
            if (guardian != null && ReferenceEquals(guardian.Ground, this))
                guardian.Ground = null;
        }

        public Vector2 WorldToPlate(Vector3 world)
        {
            // The stage faces the fixed orthographic camera, so dropping its local depth is the
            // same as looking at the point through the camera.
            Vector3 local = stage.transform.InverseTransformPoint(world);
            return new Vector2(local.x * pixelsPerUnit + plateSize.x * 0.5f, plateSize.y * 0.5f - local.y * pixelsPerUnit);
        }

        // The point of the painted plate itself, for prompts and markers.
        public Vector3 PlateToStage(Vector2 plate)
        {
            return stage.transform.TransformPoint(new Vector3((plate.x - plateSize.x * 0.5f) / pixelsPerUnit,
                (plateSize.y * 0.5f - plate.y) / pixelsPerUnit, 0f));
        }

        // The point of the ground that the camera sees at that plate pixel.
        public Vector3 PlateToGround(Vector2 plate)
        {
            Vector3 point = PlateToStage(plate);
            Vector3 view = stage.transform.forward;
            return point + view * ((groundHeight - point.y) / view.y);
        }

        public bool IsWalkablePlate(Vector2 plate)
        {
            Vector3 ground = PlateToGround(plate);
            return IsWalkable(new Vector2(ground.x, ground.z));
        }

        public bool IsWalkableWorld(Vector3 world)
        {
            return IsWalkable(new Vector2(world.x, world.z));
        }

        public Vector3 Constrain(Vector3 from, Vector3 to)
        {
            if (!Ready)
                return to;
            var feet = new Vector2(from.x, from.z);
            // Feet that start off the floor are let back in instead of being held outside.
            if (!IsWalkable(feet))
                return to;
            Vector2 travel = new Vector2(to.x, to.z) - feet;
            float left = travel.magnitude;
            if (left <= 0f)
                return to;
            Vector2 heading = travel / left;
            while (left > 0.000001f)
            {
                float length = Mathf.Min(left, StepLength);
                left -= length;
                feet = Step(feet, heading * length);
            }
            return new Vector3(feet.x, to.y, feet.y);
        }

        private void OnEnable()
        {
            if (!Bind())
                Debug.LogWarning("Lumon HubGround: no stage, guardian or floor outline. The guardian keeps her own limits.", this);
        }

        private void OnDisable()
        {
            Release();
            pauses.Clear();
        }

        private Vector2 Step(Vector2 feet, Vector2 move)
        {
            Vector2 target = feet + move;
            if (!FirstHit(feet, target, out Vector2 hit, out Vector2 along, out Vector2 away))
                return target;
            // Up to the edge, then what is left of the move runs along it.
            Vector2 rest = Reach(feet, hit + away * Skin);
            float slide = Vector2.Dot(target - rest, along);
            if (Mathf.Abs(slide) < 0.000001f)
                return rest;
            target = rest + along * slide;
            if (!FirstHit(rest, target, out hit, out _, out away))
                return target;
            return Reach(rest, hit + away * Skin);
        }

        // A resting point is taken only when it is on the floor and the way to it is clear.
        private Vector2 Reach(Vector2 from, Vector2 candidate)
        {
            return IsWalkable(candidate) && !FirstHit(from, candidate, out _, out _, out _) ? candidate : from;
        }

        // Nearest edge crossed on the way from a to b: where, the edge direction, and the edge
        // normal on the side the move comes from. The whole segment is tested, so the length
        // of the move does not matter.
        private bool FirstHit(Vector2 a, Vector2 b, out Vector2 hit, out Vector2 along, out Vector2 away)
        {
            hit = along = away = Vector2.zero;
            Vector2 move = b - a;
            float nearest = float.MaxValue;
            foreach (Vector2[] loop in loops)
            {
                for (int i = 0; i < loop.Length; i++)
                {
                    Vector2 start = loop[i];
                    Vector2 edge = loop[(i + 1) % loop.Length] - start;
                    float length = edge.magnitude;
                    if (length <= 0f)
                        continue;
                    Vector2 direction = edge / length;
                    var normal = new Vector2(-direction.y, direction.x);
                    float side = Vector2.Dot(a - start, normal);
                    if (side == 0f)
                        continue;
                    if (side < 0f)
                        normal = -normal;
                    // Only a move towards the edge can cross it.
                    if (Vector2.Dot(move, normal) >= 0f)
                        continue;
                    float cross = Cross(move, edge);
                    if (Mathf.Abs(cross) < 1e-12f)
                        continue;
                    float t = Cross(start - a, edge) / cross;
                    float u = Cross(start - a, move) / cross;
                    // The edge is a hair longer than drawn, so no gap opens at a shared corner.
                    if (t < 0f || t > 1f || u < -0.0001f || u > 1.0001f || t >= nearest)
                        continue;
                    nearest = t;
                    hit = a + move * t;
                    along = direction;
                    away = normal;
                }
            }
            return nearest <= 1f;
        }

        private bool IsWalkable(Vector2 point)
        {
            if (loops.Length == 0 || !Inside(loops[0], point))
                return false;
            for (int i = 1; i < loops.Length; i++)
                if (Inside(loops[i], point))
                    return false;
            return true;
        }

        private static bool Inside(Vector2[] loop, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
            {
                Vector2 a = loop[i], b = loop[j];
                if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private Vector2[] Bake(Vector2[] plate)
        {
            var ground = new Vector2[plate.Length];
            for (int i = 0; i < plate.Length; i++)
            {
                Vector3 point = PlateToGround(plate[i]);
                ground[i] = new Vector2(point.x, point.z);
            }
            return ground;
        }

        private T InScene<T>() where T : Component
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
