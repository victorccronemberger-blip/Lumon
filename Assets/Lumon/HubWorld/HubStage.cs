using UnityEngine;

namespace Lumon.HubWorld
{
    // Marks the root that HubWorldBuilder generates and refreshes. Its children are the clean
    // base and one sprite per layer of Art/HubWorld/hub-layers.json, in plate pixels / 100.
    public sealed class HubStage : MonoBehaviour
    {
        [SerializeField] private Vector2Int plateSize;
        [SerializeField] private SpriteRenderer baseRenderer;

        public Vector2Int PlateSize => plateSize;
        public SpriteRenderer BaseRenderer => baseRenderer;

        public void Configure(Vector2Int size, SpriteRenderer plate)
        {
            plateSize = size;
            baseRenderer = plate;
        }
    }
}
