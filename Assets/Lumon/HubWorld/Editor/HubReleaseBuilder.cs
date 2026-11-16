using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld.Editor
{
    public static class HubReleaseBuilder
    {
        private const string Folder = "Assets/Lumon/HubWorld";
        private const string ScenePath = Folder + "/HubWorld.unity";
        private const string PanelPath = Folder + "/UI/HubPanelSettings.asset";

        public static void InstallAndBuild()
        {
            Install();
            Build();
        }

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = Camera.main;
            var stations = UnityEngine.Object.FindFirstObjectByType<HubStations>();
            var ground = UnityEngine.Object.FindFirstObjectByType<HubGround>();
            if (camera == null || stations == null || ground == null)
                throw new InvalidOperationException("The scene needs its fixed camera, floor and stations before installing the interface.");

            var asset = Require<VisualTreeAsset>(Folder + "/UI/Hub.uxml");
            var theme = Require<ThemeStyleSheet>(Folder + "/UI/HubTheme.tss");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.Expand;
            EditorUtility.SetDirty(panel);

            var tree = asset.CloneTree();
            foreach (string key in new[] { "hubCanvas", "plazaHud", "openSettings", "teamPanel", "atlasPanel", "settingsPanel" })
                if (tree.Q(key) == null)
                    throw new InvalidOperationException($"Required UI element missing: {key}");

            GameObject interfaceRoot = FindOrCreateRoot("Hub Interface");
            var document = GetOrAdd<UIDocument>(interfaceRoot);
            document.panelSettings = panel;
            document.visualTreeAsset = asset;
            var controller = GetOrAdd<HubUiController>(interfaceRoot);
            const string uiArt = Folder + "/UI/Art/";
            var glyphAtlas = RequireUncompressed(uiArt + "aurora-glyphs.png");
            var bookAtlas = RequireUncompressed(uiArt + "field-book-glyphs.png");
            var fabric = RequireUncompressed(uiArt + "team-cloth.png");
            SetReferences(controller, ("hubCamera", camera), ("stations", stations),
                ("inscriptionAtlas", glyphAtlas), ("inscriptionMetrics", Require<TextAsset>(uiArt + "aurora-glyphs.json")),
                ("fieldBookAtlas", bookAtlas), ("fieldBookMetrics", Require<TextAsset>(uiArt + "field-book-glyphs.json")),
                ("teamFabric", fabric));

            GameObject audioRoot = FindOrCreateRoot("Hub Ambience");
            InstallLoop(audioRoot.transform, "Wind and birds", "Observatory-Breeze", 0.78f);
            var fire = InstallLoop(audioRoot.transform, "Campfire", "Observatory-Campfire", 0f);
            SetReferences(GetOrAdd<HubAmbience>(audioRoot), ("ground", ground), ("fire", fire));

            if (camera.GetComponent<AudioListener>() == null)
                camera.gameObject.AddComponent<AudioListener>();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save the assembled Hub scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("Installed Hub interface and two ambient sources.");
        }

        public static void Build()
        {
            const string output = "Builds/HubWorld-Win64/Lumon.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Hub Windows build {report.summary.result} ({report.summary.totalErrors} errors).");
            Debug.Log($"Built {output} ({report.summary.totalSize} bytes).");
        }

        private static AudioSource InstallLoop(Transform parent, string name, string asset, float volume)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            AudioSource source = GetOrAdd<AudioSource>(child.gameObject);
            source.clip = Require<AudioClip>($"{Folder}/Audio/{asset}.wav");
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = volume;
            return source;
        }

        private static Texture2D RequireUncompressed(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException($"Missing Hub texture {path}.", path);
            if (importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled ||
                importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            return Require<Texture2D>(path);
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException($"Missing Hub asset {path}.", path);
            return asset;
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            return roots.FirstOrDefault(root => root.name == name) ?? new GameObject(name);
        }

        private static T GetOrAdd<T>(GameObject root) where T : Component
        {
            return root.TryGetComponent(out T component) ? component : root.AddComponent<T>();
        }

        private static void SetReferences(Component target, params (string field, UnityEngine.Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in references)
                serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
