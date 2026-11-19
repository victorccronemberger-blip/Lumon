using System;
using System.IO;
using System.Linq;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;

namespace Lumon.HubWorld.Editor
{
    public static class HubSplatBuilder
    {
        private const string ScenePath = "Assets/Lumon/HubWorld/HubWorld.unity";
        private const string UiPath = "Assets/Lumon/HubWorld/UI";
        private const string DataPath = "Assets/StreamingAssets/Lumon/Splats";
        private const string FontPath = "Assets/Resources/Lumon/Fonts";

        [Serializable] private sealed class StationData
        {
            public string id;
            public string label;
            public float[] position;
            public float[] approach;
            public float radius;
            public float obstacle;
        }

        [Serializable] private sealed class CameraData
        {
            public float yaw;
            public float pitch;
            public float distance;
            public float orthographicSize;
            public float[] target;
        }

        [Serializable] private sealed class ObstacleData
        {
            public string id;
            public float[] position;
            public float radius;
            public float[] axis;
            public float halfSpan;
        }

        [Serializable] private sealed class ArmillaryData
        {
            public float[] pivot;
            public float[] middleAxis;
            public float[] horizonAxis;
            public float middleSweep;
            public float horizonSweep;
            public float period;
        }

        [Serializable] private sealed class FlameData
        {
            public string id;
            public float[] position;
            public float phase;
            public int frequency;
        }

        [Serializable] private sealed class ForgeFireData
        {
            public float[] origin;
            public float yaw;
            public float period;
            public FlameData[] tongues;
        }

        [Serializable] private sealed class HubData
        {
            public string[] palette;
            public string background;
            public float[] spawn;
            public StationData[] stations;
            public ObstacleData[] obstacles;
            public ArmillaryData armillary;
            public ForgeFireData forgeFire;
            public bool paintedStations;
            public float worldRadius;
            public int pixelSize;
            public CameraData camera;
        }

        public static void InstallAndBuild()
        {
            Install();
            Build();
        }

        public static void Install()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFontAssets();
            string manifestPath = Path.Combine(Application.dataPath, "StreamingAssets/Lumon/Splats/hub-manifest.json");
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("Manifesto do HubWorld não encontrado.", manifestPath);
            HubData data = JsonUtility.FromJson<HubData>(File.ReadAllText(manifestPath));
            if (data?.stations == null || data.camera == null) throw new InvalidDataException("Manifesto do HubWorld incompleto.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject optionsObject = new("Gaussian Splat Options");
            GaussianSplatRenderingOptions options = optionsObject.AddComponent<GaussianSplatRenderingOptions>();
            options.m_Transparency = TransparencyMode.SortedBlended;
            options.m_TemporalFilter = StochasticTemporalFilter.None;
            options.m_RenderMode = GaussianSplatting.Runtime.RenderMode.Splats;

            GameObject worldObject = new("Lumon — Santuário");
            HubSplatWorld world = worldObject.AddComponent<HubSplatWorld>();
            GaussianSplatRenderer scenery = worldObject.AddComponent<GaussianSplatRenderer>();
            scenery.m_SourcePath = "Lumon/Splats/hub-world.ply";
            scenery.m_SHOrder = 0;
            if (data.paintedStations)
            {
                foreach (ObstacleData obstacle in data.obstacles.Where(o => o.id?.StartsWith("bench-", StringComparison.Ordinal) == true))
                {
                    GameObject bench = new(obstacle.id);
                    bench.transform.SetParent(worldObject.transform, false);
                    Vector3 axis = Vector(obstacle.axis);
                    float yaw = Mathf.Atan2(-axis.z, axis.x) * Mathf.Rad2Deg;
                    bench.transform.SetLocalPositionAndRotation(Vector(obstacle.position), Quaternion.Euler(0f, yaw, 0f));
                    GaussianSplatRenderer timber = bench.AddComponent<GaussianSplatRenderer>();
                    timber.m_SourcePath = "Lumon/Splats/bench-painted.ply";
                    timber.m_SHOrder = 0;
                }
            }
            foreach (ObstacleData fixture in data.obstacles ?? Array.Empty<ObstacleData>())
            {
                if (fixture.id == "crescent-pavilion")
                    PaintedObject(worldObject.transform, "Pavilhão da Lua", "crescent-pavilion", Vector(fixture.position));
                if (fixture.id?.StartsWith("coral-tree-", StringComparison.Ordinal) == true)
                {
                    int index = int.Parse(fixture.id.Substring("coral-tree-".Length));
                    var tree = PaintedObject(worldObject.transform, "Flamboyant", "coral-tree", Vector(fixture.position));
                    tree.localRotation = Quaternion.Euler(0, index * 119f - 36f, 0);
                    tree.localScale = Vector3.one * (1.12f - index * .08f);
                }
            }
            if (data.obstacles?.Any(o => o.id == "crescent-pavilion") == true)
            {
                // Arcades below the lip frame the approach without narrowing the playable disc.
                foreach (int bay in new[] { 0,1,2,3,6,7,8,10,11,12,13,16,17,18 })
                {
                    float angle = bay * 18f * Mathf.Deg2Rad;
                    Vector3 outward = new(Mathf.Sin(angle),0,Mathf.Cos(angle));
                    var arcade = PaintedObject(worldObject.transform, "Arcada do terraço", "terrace-bay", outward * 9.18f + Vector3.down * .20f);
                    arcade.localRotation = Quaternion.Euler(0,bay*18f,0);
                }
                PaintedObject(worldObject.transform, "Torre de vigia", "watchtower", new Vector3(7.66f,0,5.72f));
                PaintedObject(worldObject.transform, "Óptica de Lira e Cisne", "scope-fittings", Vector3.zero);
            }
            if (data.armillary != null)
            {
                ArmillaryData motion = data.armillary;
                GameObject movingRings = new("Anéis móveis — Esfera armilar");
                movingRings.transform.SetParent(worldObject.transform);
                movingRings.transform.localPosition = Vector(motion.pivot);
                GaussianSplatRenderer rings = movingRings.AddComponent<GaussianSplatRenderer>();
                rings.m_SourcePath = "Lumon/Splats/armillary-gimbals.ply";
                rings.m_SHOrder = 0;
                movingRings.AddComponent<SplatArmillary>().Configure(world, Vector(motion.middleAxis),
                    Vector(motion.horizonAxis), motion.middleSweep, motion.horizonSweep, motion.period);
            }
            if (data.forgeFire != null)
            {
                ForgeFireData fire = data.forgeFire;
                GameObject hearth = new("Fogo da forja");
                hearth.transform.SetParent(worldObject.transform, false);
                hearth.transform.SetLocalPositionAndRotation(Vector(fire.origin), Quaternion.Euler(0f, fire.yaw, 0f));
                GaussianSplatRenderer masonry = null;
                if (data.paintedStations)
                {
                    masonry = hearth.AddComponent<GaussianSplatRenderer>();
                    masonry.m_SourcePath = "Lumon/Splats/forge-painted.ply";
                    masonry.m_SHOrder = 0;
                    GameObject workshop = new("Oficina do pangolim");
                    workshop.transform.SetParent(worldObject.transform, false);
                    workshop.transform.localPosition = Vector(fire.origin) + new Vector3(-.33f,0,-1.26f);
                    var animal = PaintedObject(workshop.transform, "Pangolim de brasa", "dawn-pangolin", Vector3.zero);
                    var artisan = PaintedObject(workshop.transform, "Ferreira", "dawn-smith", Vector3.zero);
                    var meteor = PaintedObject(workshop.transform, "Meteorito", "forge-meteor", Vector3.zero);
                    var paws = PaintedObject(workshop.transform, "Sombra das patas", "workshop-shadow", Vector3.zero);
                    var boots = PaintedObject(workshop.transform, "Sombra da ferreira", "workshop-shadow", Vector3.zero);
                    boots.localScale = new Vector3(.64f,1,1);
                    workshop.AddComponent<DawnForgeWorkshop>().Configure(world,animal,artisan,meteor,paws,boots);
                }
                GaussianSplatRenderer[] flames = new GaussianSplatRenderer[fire.tongues.Length];
                for (int i = 0; i < fire.tongues.Length; i++)
                {
                    FlameData tongue = fire.tongues[i];
                    GameObject part = new($"Chama da forja — {tongue.id}");
                    part.transform.SetParent(hearth.transform, false);
                    part.transform.localPosition = Vector(tongue.position);
                    flames[i] = part.AddComponent<GaussianSplatRenderer>();
                    flames[i].m_SourcePath = $"Lumon/Splats/forge-{tongue.id}.ply";
                    flames[i].m_SHOrder = 0;
                }
                hearth.AddComponent<SplatForgeFire>().Configure(world, scenery, masonry, flames,
                    fire.tongues.Select(t => t.phase).ToArray(), fire.tongues.Select(t => t.frequency).ToArray(), fire.period);
            }

            GameObject guardianObject = new("Guardiã");
            guardianObject.transform.SetPositionAndRotation(Vector(data.spawn), Quaternion.Euler(0, data.camera.yaw + 142f, 0));
            SplatGuardian guardian = guardianObject.AddComponent<SplatGuardian>();
            GameObject guardianArt = new("Guardiã — Gaussian splat");
            guardianArt.transform.SetParent(guardianObject.transform, false);
            guardianArt.transform.localPosition = Vector3.down * 0.045f;
            GaussianSplatRenderer guardianSplat = guardianArt.AddComponent<GaussianSplatRenderer>();
            guardianSplat.m_SourcePath = "Lumon/Splats/guardian-painted.ply";
            guardianSplat.m_SHOrder = 0;
            guardianSplat.m_PaintedAtlas = LoadGuardianAtlas();
            PaintedObject(guardianObject.transform, "Sombra da guardiã", "workshop-shadow", Vector3.down * .05f);

            GameObject cameraObject = new("Câmera do Santuário");
            cameraObject.tag = "MainCamera";
            Camera view = cameraObject.AddComponent<Camera>();
            scenery.m_PaintedContourCamera = view;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = ParseColor(data.background);
            view.nearClipPlane = 0.05f;
            view.farClipPlane = 100f;
            view.allowMSAA = false;
            view.allowHDR = false;
            cameraObject.AddComponent<AudioListener>();
            SplatCameraRig cameraRig = cameraObject.AddComponent<SplatCameraRig>();
            cameraRig.Configure(view, Vector(data.camera.target), data.camera.yaw, data.camera.pitch, data.camera.orthographicSize);
            cameraRig.ConfigureInput(world, worldObject.GetComponentsInChildren<GaussianSplatRenderer>()
                .Where(item => item.m_SourcePath.EndsWith("coral-tree.ply") || item.m_SourcePath.EndsWith("crescent-pavilion.ply") ||
                    item.m_SourcePath.EndsWith("watchtower.ply")).ToArray());
            SplatPixelRender pixelRender = cameraObject.AddComponent<SplatPixelRender>();
            pixelRender.Configure(data.pixelSize, withSky: true);

            SplatStationSpot[] spots = data.stations.Select(s => new SplatStationSpot
            {
                id = (LumonStationId)Enum.Parse(typeof(LumonStationId), s.id),
                label = s.label,
                position = Vector(s.position),
                approach = Vector(s.approach),
                radius = s.radius,
                obstacle = s.obstacle
            }).Concat((data.obstacles ?? Array.Empty<ObstacleData>()).Where(o => o.id == "telescope-0" || o.id == "telescope-1").Select(o =>
            {
                Vector3 center = Vector(o.position);
                bool west = o.id == "telescope-0";
                return new SplatStationSpot { id = west ? LumonStationId.WestScope : LumonStationId.EastScope,
                    label = west ? "LIRA" : "CISNE", position = center, approach = center - center.normalized * 1.05f,
                    radius = 1.10f, obstacle = o.radius };
            })).ToArray();
            SplatObstacle[] blocks = data.obstacles?.Where(o => o.id != "telescope-0" && o.id != "telescope-1").Select(o => new SplatObstacle
            {
                position = Vector(o.position),
                radius = o.radius,
                axis = o.halfSpan > 0f ? Vector(o.axis).normalized : Vector3.zero,
                halfSpan = o.halfSpan
            }).ToArray() ?? new[] { new SplatObstacle { position = new Vector3(0.55f, 0, -0.55f), radius = 0.79f } };
            world.Configure(guardian, view, spots, blocks, data.worldRadius);
            guardian.Configure(world, view, guardianArt.transform);

            EnsureFolder(UiPath);
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPath + "/LumonPanelSettings.asset");
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, UiPath + "/LumonPanelSettings.asset");
            }
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1280, 720);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            string themePath = UiPath + "/LumonTheme.tss";
            File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(themePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            if (theme == null) throw new InvalidDataException("O tema da interface não foi importado.");
            panelSettings.themeStyleSheet = theme;
            EditorUtility.SetDirty(panelSettings);
            EditorUtility.SetDirty(theme);

            GameObject uiObject = new("Interface do Santuário");
            UIDocument document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiPath + "/Hub.uxml");
            if (document.visualTreeAsset == null) throw new InvalidDataException("Hub.uxml não foi importado.");
            uiObject.AddComponent<HubUiController>().Configure(world, view, data.palette.Select(ParseColor).ToArray());

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ParseColor(data.background);
            RenderSettings.fog = false;
            QualitySettings.antiAliasing = 0;
            QualitySettings.vSyncCount = 1;

            ConfigurePlayer();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Não foi possível salvar a cena.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"LUMON_BUILD: cena instalada; {spots.Length} estações; pixel={data.pixelSize}; API=D3D12.");
        }

        private static Transform PaintedObject(Transform parent, string title, string asset, Vector3 position)
        {
            string relative = "Lumon/Splats/" + asset + ".ply";
            if (!File.Exists(Path.Combine(Application.streamingAssetsPath,relative))) throw new FileNotFoundException(relative);
            GameObject part = new(title);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            var renderer = part.AddComponent<GaussianSplatRenderer>();
            renderer.m_SourcePath = relative; renderer.m_SHOrder = 0;
            return part.transform;
        }

        private static Texture2D LoadGuardianAtlas()
        {
            const string path = "Assets/Resources/Lumon/GuardianPainted.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                throw new FileNotFoundException("Folha de animação da guardiã ausente.", path);
            if (importer.sRGBTexture || importer.mipmapEnabled || importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.alphaIsTransparency ||
                importer.maxTextureSize < 4096)
            {
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (atlas == null || atlas.width != SplatGuardian.TileWidth * SplatGuardian.AtlasDirections * 2 ||
                atlas.height != SplatGuardian.TileHeight * SplatGuardian.AtlasBankRows)
                throw new InvalidDataException($"Dimensões de {path} incompatíveis com a grade de animação da guardiã.");
            return atlas;
        }

        public static void Build()
        {
            LoadGuardianAtlas();
            ConfigurePlayer();
            string output = Path.GetFullPath("Builds/HubWorld-Win64/Lumon.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? "Builds/HubWorld-Win64");
            BuildPlayerOptions options = new()
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Falha na build: {report.summary.result}; erros={report.summary.totalErrors}.");
            Debug.Log($"LUMON_BUILD: concluída em {output}; tamanho={report.summary.totalSize / (1024 * 1024)} MiB.");
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Lumon";
            PlayerSettings.companyName = "Lumon";
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12 });
            EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneWindows64;
        }

        private static void EnsureFontAssets()
        {
            EnsureFolder(FontPath);
            EnsureFontAsset("SourceSerif4-Semibold.otf", "SourceSerif4-Semibold");
            EnsureFontAsset("AtkinsonHyperlegible-Regular.ttf", "AtkinsonHyperlegible-Regular");
        }

        private static void EnsureFontAsset(string fontFile, string assetName)
        {
            string path = FontPath + "/" + assetName + ".asset";
            string previousPath = UiPath + "/Fonts/" + assetName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<FontAsset>(path) == null && AssetDatabase.LoadAssetAtPath<FontAsset>(previousPath) != null)
                AssetDatabase.MoveAsset(previousPath, path);
            FontAsset existing = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (existing != null && existing.characterTable.Count > 0 && existing.material != null) return;
            if (existing != null) AssetDatabase.DeleteAsset(path);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(UiPath + "/Fonts/" + fontFile);
            if (font == null) throw new FileNotFoundException("Fonte não importada", fontFile);
            FontAsset asset = FontAsset.CreateFontAsset(font);
            if (asset == null) throw new InvalidOperationException("Não foi possível criar FontAsset para " + fontFile);
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            string characters = string.Concat(Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96)).Select(i => (char)i)) + "•";
            if (!asset.TryAddCharacters(characters, out string missing))
                throw new InvalidOperationException($"Caracteres ausentes em {fontFile}: {missing}");
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                Texture2D atlas = asset.atlasTextures[i];
                if (atlas == null) continue;
                atlas.name = assetName + " Atlas " + i;
                atlas.Apply(false, false);
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
            }
            if (asset.material == null) throw new InvalidOperationException("Material de fonte não criado para " + fontFile);
            asset.material.name = assetName + " Material";
            if (!AssetDatabase.Contains(asset.material)) AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            Debug.Log($"LUMON_FONT: {assetName}; caracteres={asset.characterTable.Count}; atlas={asset.atlasTextures.Length}; material={asset.material.name}.");
        }

        private static Vector3 Vector(float[] values)
        {
            if (values == null || values.Length != 3) throw new InvalidDataException("Vetor 3D inválido no manifesto.");
            return new Vector3(values[0], values[1], values[2]);
        }

        private static Color ParseColor(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color color)) throw new InvalidDataException($"Cor inválida: {hex}");
            return color;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
