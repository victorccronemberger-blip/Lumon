using UnityEngine;

namespace Lumon.GuardianReview
{
    public sealed class GuardianMotion : MonoBehaviour
    {
        // The review camera looks along +Z, so left and forward match world X and Z.
        private const float TurnSpeed = 720f;

        // 2.8 m/s is the 3D review pace. Scenes saved before this field existed keep it.
        // The 2D scene stores the pace that matches its walk cycle.
        [SerializeField, Min(0f)] private float speed = 2.8f;
        [SerializeField] private bool turnTowardMovement = true;
        [SerializeField] private GuardianSpriteFacing directionalArt;
        [SerializeField] private Vector2 minPosition = new Vector2(-3.7f, -2f);
        [SerializeField] private Vector2 maxPosition = new Vector2(3.7f, 2f);

        // Optional floor supplied by a scene at run time (the Hub). It is never serialized:
        // without one the rectangle above applies and input is always read, as before.
        public interface IGround
        {
            bool InputPaused { get; }
            Vector3 Constrain(Vector3 from, Vector3 to);
        }

        public float Speed => speed;
        public IGround Ground { get; set; }

        private void Update()
        {
            float x = (Held(KeyCode.D, KeyCode.RightArrow) ? 1 : 0)
                    - (Held(KeyCode.A, KeyCode.LeftArrow) ? 1 : 0);
            float z = (Held(KeyCode.W, KeyCode.UpArrow) ? 1 : 0)
                    - (Held(KeyCode.S, KeyCode.DownArrow) ? 1 : 0);
            Tick(new Vector2(x, z), Time.deltaTime);
        }

        // One frame of player input. While the ground pauses input the guardian rests
        // where she is and keeps her facing.
        public void Tick(Vector2 input, float seconds)
        {
            Move(Ground != null && Ground.InputPaused ? Vector2.zero : input, seconds);
        }

        public void Move(Vector2 input, float seconds)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 direction = new Vector3(input.x, 0f, input.y);
            if (direction == Vector3.zero)
            {
                if (directionalArt != null)
                    directionalArt.SetMotion(Vector2.zero, 0f);
                return;
            }

            // A zero-length tick with a key held (paused time) must not read as a stop,
            // otherwise the stride would restart from its first frame.
            if (seconds <= 0f)
                return;

            if (turnTowardMovement)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), TurnSpeed * seconds);
            Vector3 origin = transform.position;
            Vector3 position = origin + direction * (speed * seconds);
            if (Ground != null)
                position = Ground.Constrain(origin, position);
            else
            {
                position.x = Mathf.Clamp(position.x, minPosition.x, maxPosition.x);
                position.z = Mathf.Clamp(position.z, minPosition.y, maxPosition.y);
            }
            transform.position = position;
            if (directionalArt != null)
            {
                Vector3 delta = position - origin;
                Vector2 actual = new Vector2(delta.x, delta.z);
                float travelled = actual.magnitude;
                // Against a limit the pressed direction decides the facing. A rounding
                // residue left on one axis must not turn the guardian for a frame.
                // On a scene floor a wall met almost head-on still lets her creep sideways;
                // that creep must not turn her either.
                bool moved = travelled > GuardianSpriteFacing.RestDistance
                    && (Ground == null || travelled >= 0.5f * speed * seconds);
                directionalArt.SetMotion(moved ? actual : input, travelled);
            }
        }

        private static bool Held(KeyCode primary, KeyCode alternate)
        {
            return Input.GetKey(primary) || Input.GetKey(alternate);
        }
    }
}
