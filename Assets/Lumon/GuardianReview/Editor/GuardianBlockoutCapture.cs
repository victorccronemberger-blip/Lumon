using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lumon.GuardianReview
{
    public static class GuardianBlockoutCapture
    {
        private const string ReviewFolder = "Art/Guardian/review/02-blockout";
        private const string TemporaryModel = "Assets/Lumon/GuardianReview/CH_Guardian_review.fbx";

        // Loads the review geometry into the real fixed camera, then leaves the serialized scene clean.
        public static void Capture()
        {
            string folder = Path.GetFullPath(ReviewFolder);
            string source = Path.Combine(folder, "CH_Guardian_review.fbx");
            if (File.Exists(TemporaryModel))
                throw new InvalidOperationException($"Reserved review path already exists at {TemporaryModel}.");

            GuardianReviewScene.Open();
            GuardianMotion pivot = UnityEngine.Object.FindAnyObjectByType<GuardianMotion>();
            Camera camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var previousActive = RenderTexture.active;
            var matte = new Material(Shader.Find("Standard"));
            matte.color = new Color32(130, 144, 153, 255);
            matte.SetFloat("_Glossiness", 0.16f);
            GameObject model = null;
            try
            {
                File.Copy(source, TemporaryModel);
                AssetDatabase.ImportAsset(TemporaryModel, ImportAssetOptions.ForceSynchronousImport);
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>(TemporaryModel);
                if (imported == null)
                    throw new InvalidOperationException("Could not import the review FBX.");
                model = UnityEngine.Object.Instantiate(imported, pivot.transform, false);
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterial = matte;
                Light key = GameObject.Find("Warm key").GetComponent<Light>();
                key.shadows = LightShadows.Soft;
                key.shadowStrength = 0.75f;

                camera.targetTexture = target;
                camera.aspect = (float)target.width / target.height;
                for (int i = 0; i < 8; i++)
                {
                    pivot.transform.rotation = Quaternion.Euler(0f, 180f + i * 45f, 0f);
                    VerifyGeometry(model, camera, i);

                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(folder, $"unity-yaw-{i * 45:000}.png"), pixels.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(matte);
                if (model != null)
                    UnityEngine.Object.DestroyImmediate(model);
                AssetDatabase.DeleteAsset(TemporaryModel);
            }
        }

        private static void VerifyGeometry(GameObject model, Camera camera, int view)
        {
            var filters = model.GetComponentsInChildren<MeshFilter>();
            if (filters.Length != 13)
                throw new InvalidOperationException($"Expected 13 review meshes, imported {filters.Length}.");

            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            foreach (var filter in filters)
            {
                foreach (var localVertex in filter.sharedMesh.vertices)
                {
                    Vector3 vertex = filter.transform.TransformPoint(localVertex);
                    low = Mathf.Min(low, vertex.y);
                    high = Mathf.Max(high, vertex.y);
                    float projected = camera.WorldToScreenPoint(vertex).y;
                    bottom = Mathf.Min(bottom, projected);
                    top = Mathf.Max(top, projected);
                }
            }

            if (Mathf.Abs(low) > 0.005f || Mathf.Abs(high - 1.8f) > 0.005f
                || top - bottom < 120f || bottom < 36f || top > 684f)
                throw new InvalidOperationException($"View {view}: unexpected bounds {low:F3}–{high:F3} m, {bottom:F1}–{top:F1} px.");
            Debug.Log($"Lumon blockout {view * 45:000}°: height {high - low:F3} m, visible height {top - bottom:F1} px.");
        }
    }
}
