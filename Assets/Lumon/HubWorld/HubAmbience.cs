using UnityEngine;

namespace Lumon.HubWorld
{
    public sealed class HubAmbience : MonoBehaviour
    {
        private static readonly Vector2 FirePosition = new Vector2(930f, 445f);
        [SerializeField] private HubGround ground;
        [SerializeField] private AudioSource fire;

        private void LateUpdate()
        {
            if (!ground.Ready) return;
            float distance = Vector2.Distance(ground.FeetPlate, FirePosition);
            float proximity = 1f - Mathf.InverseLerp(28f, 260f, distance);
            fire.volume = 0.8f * proximity * proximity;
            fire.panStereo = Mathf.Clamp((FirePosition.x - ground.FeetPlate.x) / 400f, -0.38f, 0.38f);
        }
    }
}
