using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumon.GuardianReview
{
    public static class GuardianSpriteReview
    {
        private const string DirectoryName = "Assets/Lumon/GuardianReview/";
        private const string ScenePath = DirectoryName + "Guardian2DPreview.unity";
        private const string SpriteDirectory = DirectoryName + "Art2D/";
        private const string ArtRoot = "Art/Guardian2D/";
        private const float VerticalSpan = 8.2f;
        private const float SpriteHeightAt720 = 165f;

        [Serializable]
        private sealed class Layout
        {
            public float pivotX;
            public float pivotY;
            public int opaqueHeight;
        }

        [MenuItem("Lumon/Open 2D Guardian Review")]
        public static void Open()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("The 2D review scene already exists.");

            Sprite guardianSprite = ImportGuardian();
            Sprite shadowSprite = Import("GuardianContactShadow.png", 164f, Vector2.one * 0.5f, false);
            Sprite plateSprite = Import("ObservatoryReviewPlate.png", 100f, Vector2.one * 0.5f, false);

            GuardianReviewScene.Open();
            Camera camera = Camera.main;
            camera.aspect = 16f / 9f;
            GameObject.DestroyImmediate(GameObject.Find("Neutral floor"));

            var plate = AddSprite("Observatory art reference", plateSprite, -10);
            plate.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * 23f,
                camera.transform.rotation);
            plate.transform.localScale = new Vector3(VerticalSpan * camera.aspect / plateSprite.bounds.size.x,
                VerticalSpan / plateSprite.bounds.size.y, 1f);

            var ground = new Plane(Vector3.up, Vector3.zero);
            Ray spawn = camera.ViewportPointToRay(new Vector3(0.456f, 0.295f));
            if (!ground.Raycast(spawn, out float distance))
                throw new InvalidOperationException("The camera could not place the guardian's boots.");

            GuardianMotion control = UnityEngine.Object.FindAnyObjectByType<GuardianMotion>();
            control.name = "2D Guardian boot anchor";
            control.transform.SetPositionAndRotation(spawn.GetPoint(distance), Quaternion.identity);
            var settings = new SerializedObject(control);
            settings.FindProperty("turnTowardMovement").boolValue = false;
            settings.FindProperty("minPosition").vector2Value = new Vector2(-2.45f, -3.5f);
            settings.FindProperty("maxPosition").vector2Value = new Vector2(0.8f, 1.4f);
            settings.ApplyModifiedPropertiesWithoutUndo();

            var shadow = AddSprite("Contact shadow", shadowSprite, -1);
            shadow.transform.SetParent(control.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var guardian = AddSprite("Guardian illustration", guardianSprite, 0);
            guardian.transform.SetParent(control.transform, false);
            guardian.transform.localRotation = camera.transform.rotation;
            if (!EditorSceneManager.SaveScene(camera.gameObject.scene, ScenePath, saveAsCopy: true))
                throw new IOException("Could not save the 2D review scene.");
        }

        public static void RefreshAndCapture()
        {
            if (ImportGuardian() == null)
                throw new InvalidOperationException("The guardian sprite could not be refreshed.");
            Capture();
        }

        private static Sprite ImportGuardian()
        {
            Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(ArtRoot + "sprite-layout.json"));
            if (layout.opaqueHeight <= 0 || layout.pivotX <= 0 || layout.pivotY <= 0)
                throw new InvalidOperationException("Invalid 2D sprite layout.");
            return Import("GuardianIdleThreeQuarter.png", PixelsPerUnit(layout.opaqueHeight),
                new Vector2(layout.pivotX, layout.pivotY), true);
        }

        // Scale at which a figure that is heightInPixels tall in its texture reads 165 px at 720p.
        internal static float PixelsPerUnit(float heightInPixels)
        {
            return heightInPixels / (VerticalSpan * SpriteHeightAt720 / 720f);
        }

        internal static Sprite Import(string name, float ppu, Vector2 pivot, bool mipmaps)
        {
            string path = SpriteDirectory + name;
            var settings = AssetImporter.GetAtPath(path) as TextureImporter;
            if (settings == null)
                throw new InvalidOperationException($"Missing image {path}.");
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteImportMode = SpriteImportMode.Single;
            settings.spritePixelsPerUnit = ppu;
            settings.spritePivot = pivot;
            var spriteSettings = new TextureImporterSettings();
            settings.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
            settings.SetTextureSettings(spriteSettings);
            settings.mipmapEnabled = mipmaps;
            settings.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            settings.alphaIsTransparency = true;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static SpriteRenderer AddSprite(string name, Sprite sprite, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        public static void Capture()
        {
            Open();
            Camera camera = Camera.main;
            var guardian = GameObject.Find("Guardian illustration").GetComponent<SpriteRenderer>();
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                string stem = ArtRoot + $"review/unity-{size.x}x{size.y}";
                CaptureFrame(camera, size, stem + ".png");
                guardian.enabled = false;
                try
                {
                    CaptureFrame(camera, size, stem + "-background.png");
                }
                finally
                {
                    guardian.enabled = true;
                }
            }
        }

        internal static void CaptureFrame(Camera camera, Vector2Int size, string path)
        {
            var target = new RenderTexture(size.x, size.y, 24);
            var frame = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)size.x / size.y;
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                frame.Apply();
                File.WriteAllBytes(path, frame.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
