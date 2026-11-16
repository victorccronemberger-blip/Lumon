using System;
using UnityEngine;

namespace Lumon.GuardianReview
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GuardianSpriteFacing : MonoBehaviour
    {
        public enum Direction { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest }

        // Displacements at or below this length count as standing still.
        public const float RestDistance = 0.00001f;

        [Serializable]
        private sealed class WalkCycle
        {
            public Direction direction;
            [Min(0.01f)] public float distancePerCycle;
            public Sprite[] frames;
        }

        [SerializeField] private Sprite[] idleByDirection = new Sprite[8];
        [SerializeField] private Direction startingDirection = Direction.SouthEast;
        [SerializeField] private WalkCycle[] walkCycles = Array.Empty<WalkCycle>();
        private SpriteRenderer spriteRenderer;
        private float phase;

        public Direction Current { get; private set; }

        private void Awake()
        {
            Current = startingDirection;
            Render(idleByDirection[(int)Current]);
        }

        public void Face(Vector2 input)
        {
            SetMotion(input, 0f);
        }

        public void SetMotion(Vector2 input, float distance)
        {
            if (input.sqrMagnitude > 0f)
            {
                float angle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
                Current = (Direction)((Mathf.RoundToInt(angle / 45f) + 8) % 8);
            }

            if (distance > RestDistance)
            {
                foreach (WalkCycle cycle in walkCycles)
                {
                    if (cycle.direction != Current || cycle.frames.Length == 0 || cycle.distancePerCycle <= 0f)
                        continue;

                    // The stride follows the ground actually covered, so it keeps its place
                    // for as long as the guardian keeps moving along a rumo that has a cycle.
                    phase = Mathf.Repeat(phase + distance / cycle.distancePerCycle, 1f);
                    int frame = Mathf.Min(Mathf.FloorToInt(phase * cycle.frames.Length), cycle.frames.Length - 1);
                    Render(cycle.frames[frame]);
                    return;
                }
            }

            // Standing still, blocked, or a rumo still waiting for its walk art:
            // show the rest pose and let the next stride start from its first frame.
            phase = 0f;
            Render(idleByDirection[(int)Current]);
        }

        private void Render(Sprite sprite)
        {
            if (sprite == null)
                return;
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer.sprite != sprite)
                spriteRenderer.sprite = sprite;
        }
    }
}
