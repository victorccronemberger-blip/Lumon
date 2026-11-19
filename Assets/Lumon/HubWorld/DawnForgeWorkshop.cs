using UnityEngine;

namespace Lumon.HubWorld
{
    // The actors are painted reliefs: only their display plane tracks the viewer.
    // Their feet and the workbench stay anchored to the same world-space stage.
    public sealed class DawnForgeWorkshop : MonoBehaviour
    {
        [SerializeField] private HubSplatWorld world;
        [SerializeField] private Transform pangolin, smith, meteor, animalShadow, smithShadow;
        private Camera view;
        internal Vector3 AnimalCenter => pangolin.TransformPoint(new Vector3(0,.35f,0));
        internal Vector3 ArtisanCenter => smith.TransformPoint(new Vector3(-.22f,.66f,0));
        private static readonly Vector3 Approach = new(0.848f, 0, -0.53f);
        public void Configure(HubSplatWorld owner, Transform animal, Transform artisan, Transform stone,
            Transform creatureFoot, Transform artisanFoot)
        {
            world = owner; pangolin = animal; smith = artisan; meteor = stone;
            animalShadow = creatureFoot; smithShadow = artisanFoot;
        }
        private static float Ease(float start, float end, float t) =>
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,end,t));

        private void LateUpdate()
        {
            if (view == null) view = Camera.main;
            if (view == null || pangolin == null) return;
            Vector3 center = transform.position;
            Quaternion face = view.transform.rotation;
            Vector3 right = face * Vector3.right;
            float side = Vector3.Dot(right,Approach) >= 0 ? 1 : -1;
            float t = world != null && world.ReducedMotion ? .48f : Mathf.Repeat(Time.time / 8.2f,1f);
            float advance = Ease(.02f,.42f,t) - Ease(.64f,.98f,t);
            float lift = Ease(.50f,.64f,t) - Ease(.77f,.89f,t);
            float bob = Mathf.Sin(t * Mathf.PI * 22f) * Mathf.Sin(advance * Mathf.PI) * .034f;
            Vector3 smithFoot = center + Approach * .95f;
            Vector3 animalFoot = center - Approach * Mathf.Lerp(1.60f,.70f,advance);
            smith.SetPositionAndRotation(smithFoot + Vector3.up*.04f,face * Quaternion.Euler(0,0,-side*lift*10f));
            smith.localScale = new Vector3(side,1+.045f*lift,1);
            pangolin.SetPositionAndRotation(animalFoot + Vector3.up*(.04f+Mathf.Abs(bob)),face * Quaternion.Euler(0,0,side*bob*28f));
            pangolin.localScale = new Vector3(side*(1+bob*.4f),1-bob*.4f,1);
            animalShadow.position = animalFoot + Vector3.up*.027f;
            smithShadow.position = smithFoot + Vector3.up*.027f;
            Vector3 snout = pangolin.TransformPoint(new Vector3(.75f,.20f,0));
            Vector3 tongs = smith.TransformPoint(new Vector3(-.89f,.32f,0));
            Vector3 offered = Vector3.Lerp(snout,tongs,Ease(.34f,.48f,t));
            Vector3 hearth = transform.position + new Vector3(.33f,.66f,1.09f);
            meteor.position = Vector3.Lerp(offered,hearth,Ease(.65f,.77f,t));
            meteor.rotation = Quaternion.Euler(22,220f*advance,18f);
            float size = (1-Ease(.75f,.79f,t)) * Ease(0,.045f,t);
            meteor.localScale = Vector3.one * Mathf.Max(.001f,size);
        }
    }
}
