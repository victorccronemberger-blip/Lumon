using System;
using System.IO;
using UnityEngine;

namespace Lumon.HubWorld
{
    internal sealed class SplatSceneLight
    {
        private const int HeaderSize = 16;
        private const uint Magic = 0x314C4D4C; // LML1
        private readonly byte[] samples;
        private readonly int side;
        private readonly float minimum;
        private readonly float spacing;

        private SplatSceneLight(byte[] samples, int side, float minimum, float spacing)
        {
            this.samples = samples;
            this.side = side;
            this.minimum = minimum;
            this.spacing = spacing;
        }

        public static SplatSceneLight Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("Lumon/ObservatoryLight");
            if (asset == null) return null;
            byte[] bytes = asset.bytes;
            if (bytes.Length < HeaderSize) return null;
            using BinaryReader reader = new(new MemoryStream(bytes, false));
            if (reader.ReadUInt32() != Magic) return null;
            int side = reader.ReadInt32();
            float minimum = reader.ReadSingle();
            float spacing = reader.ReadSingle();
            if (side < 2 || side > 512 || bytes.Length != HeaderSize + side * side ||
                !float.IsFinite(minimum) || !float.IsFinite(spacing) || spacing <= 0f) return null;
            return new SplatSceneLight(bytes, side, minimum, spacing);
        }

        public float Sample(Vector3 position)
        {
            float x = Mathf.Clamp((position.x - minimum) / spacing, 0f, side - 1f);
            float z = Mathf.Clamp((position.z - minimum) / spacing, 0f, side - 1f);
            int column = Mathf.Min((int)x, side - 2);
            int row = Mathf.Min((int)z, side - 2);
            int index = HeaderSize + row * side + column;
            float near = Mathf.Lerp(samples[index], samples[index + 1], x - column);
            float far = Mathf.Lerp(samples[index + side], samples[index + side + 1], x - column);
            return Mathf.Lerp(near, far, z - row) / byte.MaxValue;
        }
    }
}
