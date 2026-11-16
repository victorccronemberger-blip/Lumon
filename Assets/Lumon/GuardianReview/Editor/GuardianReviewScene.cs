using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumon.GuardianReview
{
    public static class GuardianReviewScene
    {
        private const string Root = "Assets/Lumon/GuardianReview/";
        private const string ScenePath = Root + "GuardianReview.unity";
        private const int ReviewHeight = 720;
        private const float GuardianHeight = 1.8f;
        private const float MinimumHeightInPixels = 120f;

        [MenuItem("Lumon/Open Guardian Review")]
        public static void Open()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("The review scene already exists.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(151, 163, 177, 255);

            var camera = new GameObject("Fixed gameplay camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 4.1f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(47, 51, 62, 255);
            camera.transform.SetPositionAndRotation(new Vector3(0f, 6.3f, -9.7f),
                Quaternion.Euler(30f, 0f, 0f));

            AddLight("Warm key", new Color32(255, 221, 180, 255), 1.1f, new Vector3(47, -32, 0));
            AddLight("Cool fill", new Color32(163, 196, 255, 255), 0.28f, new Vector3(35, 135, 0));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Neutral floor";
            floor.transform.position = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(11f, 0.3f, 7.5f);
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color32(110, 112, 107, 255);
            material.SetFloat("_Glossiness", 0.05f);
            AssetDatabase.CreateAsset(material, Root + "NeutralFloor.mat");
            floor.GetComponent<MeshRenderer>().sharedMaterial = material;

            var guardian = new GameObject("Guardian model pivot");
            guardian.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            guardian.AddComponent<GuardianMotion>();

            VerifyFraming(camera);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        public static void Verify()
        {
            Open();
            var guardian = UnityEngine.Object.FindAnyObjectByType<GuardianMotion>();
            if (guardian == null || Camera.main == null)
                throw new InvalidOperationException("The guardian pivot and fixed camera are required.");
            VerifyFraming(Camera.main);
        }

        private static void VerifyFraming(Camera camera)
        {
            camera.aspect = 16f / 9f;
            for (int depth = -1; depth <= 1; depth++)
            {
                for (int side = -1; side <= 1; side++)
                {
                    Vector3 feet = new Vector3(side * 3.7f, 0f, depth * 2f);
                    Vector3 bottom = camera.WorldToViewportPoint(feet);
                    Vector3 top = camera.WorldToViewportPoint(feet + Vector3.up * GuardianHeight);
                    float pixels = (top.y - bottom.y) * ReviewHeight;
                    if (pixels < MinimumHeightInPixels || bottom.y < 0.05f || top.y > 0.95f
                        || bottom.x < 0.05f || bottom.x > 0.95f)
                        throw new InvalidOperationException($"Guardian framing failed at {feet}: {pixels:F1}px.");
                }
            }

            float height = (camera.WorldToViewportPoint(Vector3.up * GuardianHeight).y
                - camera.WorldToViewportPoint(Vector3.zero).y) * ReviewHeight;
            Debug.Log($"Lumon review: nine anchor points passed; 1.8 m projects to {height:F1}px at 720p.");
        }

        private static void AddLight(string name, Color32 color, float intensity, Vector3 rotation)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.transform.rotation = Quaternion.Euler(rotation);
        }
    }
}
