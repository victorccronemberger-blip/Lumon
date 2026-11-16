using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumon.HubWorld
{
    // One thing standing on the floor that the guardian can walk behind.
    [Serializable]
    public sealed class HubOccluder
    {
        public string name = "";
        [Tooltip("Names of sprites under the stage (the manifest ids). A name that is missing is skipped.")]
        public string[] layers = Array.Empty<string>();
        [Tooltip("Foreground sprites placed by hand, anywhere in the scene.")]
        public SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        [Tooltip("Plate pixel row where it meets the ground. Feet above this row are behind it.")]
        public float baseline;
    }

    // Depth by sorting order alone, in three bands. Nothing is repainted and nothing moves.
    //   painted band     the orders written by the stage builder, all behind the guardian
    //   guardian band    the guardian and her shadow, left exactly as they are
    //   front band       painted order + frontOffset, taken by an occluder while her feet are
    //                    above its baseline; sprites of one occluder keep their relative order
    // Orders are changed in Play Mode only and handed back on disable, so the scene file and
    // the builder's checks always see the painted orders.
    public sealed class HubDepthSort : MonoBehaviour
    {
        [SerializeField] private HubGround ground;
        [SerializeField, Min(1)] private int frontOffset = 100;
        [Tooltip("Plate pixels. Keeps a guardian who stops on a baseline from flickering.")]
        [SerializeField, Min(0f)] private float hysteresis = 1.5f;
        [SerializeField] private HubOccluder[] occluders = Array.Empty<HubOccluder>();

        private sealed class Group
        {
            public HubOccluder source;
            public readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
            public readonly List<int> painted = new List<int>();
            public bool inFront;
        }

        private readonly List<Group> groups = new List<Group>();

        public int FrontOffset => frontOffset;
        public IReadOnlyList<HubOccluder> Occluders => occluders;

        public void Configure(HubGround floor, int offset, HubOccluder[] list)
        {
            ground = floor;
            frontOffset = Mathf.Max(1, offset);
            occluders = list;
        }

        // The rule itself: is something with this baseline in front of feet on this row?
        public bool InFront(float baseline, float feetRow, bool wasInFront)
        {
            return feetRow < baseline + (wasInFront ? hysteresis : -hysteresis);
        }

        // Sprites are looked up by name each time, never by position in the stage, so layers
        // may be added, removed or reordered by the art pipeline.
        public void Bind()
        {
            Restore();
            groups.Clear();
            Transform stage = ground != null && ground.Stage != null ? ground.Stage.transform : null;
            foreach (HubOccluder occluder in occluders)
            {
                if (occluder == null)
                    continue;
                var group = new Group { source = occluder };
                if (stage != null && occluder.layers != null)
                    foreach (string layer in occluder.layers)
                    {
                        Transform child = string.IsNullOrEmpty(layer) ? null : stage.Find(layer);
                        Add(group, child != null ? child.GetComponent<SpriteRenderer>() : null);
                    }
                if (occluder.renderers != null)
                    foreach (SpriteRenderer sprite in occluder.renderers)
                        Add(group, sprite);
                groups.Add(group);
            }
        }

        public void Apply()
        {
            if (ground == null || !ground.Ready)
                return;
            float feetRow = ground.FeetPlate.y;
            foreach (Group group in groups)
            {
                bool inFront = InFront(group.source.baseline, feetRow, group.inFront);
                if (inFront != group.inFront)
                    Set(group, inFront);
            }
        }

        public void Restore()
        {
            foreach (Group group in groups)
                Set(group, false);
        }

        // Sprites found for an occluder, and the orders they were painted with.
        public int Resolved(HubOccluder occluder, List<SpriteRenderer> sprites = null, List<int> painted = null)
        {
            foreach (Group group in groups)
            {
                if (group.source != occluder)
                    continue;
                sprites?.AddRange(group.sprites);
                painted?.AddRange(group.painted);
                return group.sprites.Count;
            }
            return 0;
        }

        private void OnEnable()
        {
            Bind();
        }

        // After the guardian has moved this frame.
        private void LateUpdate()
        {
            Apply();
        }

        private void OnDisable()
        {
            Restore();
            groups.Clear();
        }

        private static void Add(Group group, SpriteRenderer sprite)
        {
            if (sprite == null || group.sprites.Contains(sprite))
                return;
            group.sprites.Add(sprite);
            group.painted.Add(sprite.sortingOrder);
        }

        private void Set(Group group, bool inFront)
        {
            group.inFront = inFront;
            for (int i = 0; i < group.sprites.Count; i++)
                if (group.sprites[i] != null)
                    group.sprites[i].sortingOrder = group.painted[i] + (inFront ? frontOffset : 0);
        }
    }
}
