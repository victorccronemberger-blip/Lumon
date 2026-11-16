using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Lumon.GuardianReview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumon.HubWorld.Editor
{
    // Builds Assets/Lumon/HubWorld/HubWorld.unity from the 2D guardian review scene and
    // Art/HubWorld/hub-layers.json, then renders the proof into Art/HubWorld/review.
    //
    //   Unity -batchmode -quit -projectPath <project> -executeMethod Lumon.HubWorld.Editor.HubWorldBuilder.BuildAndCapture
    //
    // Needs a graphics device (no -nographics). The source scene is opened and never saved.
    // On a second run only the generated stage (base and layers) is refreshed.
    public static class HubWorldBuilder
    {
        private const string SourceScene = "Assets/Lumon/GuardianReview/Guardian2DPreview.unity";
        private const string HubFolder = "Assets/Lumon/HubWorld";
        private const string HubScene = HubFolder + "/HubWorld.unity";
        private const string ArtFolder = HubFolder + "/Art";
        private const string ShaderPath = HubFolder + "/HubSway.shader";
        private const string MaterialPath = HubFolder + "/HubSway.mat";
        private const string ManifestPath = "Art/HubWorld/hub-layers.json";
        private const string ReviewDirectory = "Art/HubWorld/review";
        private const string OriginalPlateName = "Observatory art reference";
        private const string GuardianArtName = "Guardian illustration";
        private const string StageName = "HubWorld stage";
        private const string BaseName = "Base";

        private const float PixelsPerUnit = 100f;
        private const float FallbackDepth = 23f;
        private const int BaseOrder = -100;
        private const float AlignmentTolerancePx = 0.5f;
        // Layers that share an order are drawn in manifest sequence: each sits a hair nearer the camera.
        private const float LayerDepthStep = 0.001f;
        private static readonly Vector2Int CaptureSize = new Vector2Int(1280, 720);
        private static readonly float[] SampleTimes = { 0f, 0.5f, 1f };
        private static readonly string[] SampleSuffixes = { "000", "050", "100" };
        private static readonly string[] MotionNames = { "Static", "Flame", "Lantern", "Banner", "Tree", "Glow" };

        [Serializable]
        private sealed class Manifest
        {
            public int width;
            public int height;
            public string baseImage;
            public ManifestLayer[] layers;
        }

        [Serializable]
        private sealed class ManifestLayer
        {
            public string id;
            public string image;
            public float x;
            public float y;
            public float pivotX;
            public float pivotY;
            public int order;
            public string motion;
            public float amplitude;
            public float period;
            public float phase;
        }

        [Serializable]
        private sealed class Evidence
        {
            public string method = "Lumon.HubWorld.Editor.HubWorldBuilder.BuildAndCapture";
            public bool passed;
            public string failure = "";
            public string unity;
            public string generatedUtc;
            public string manifest = ManifestPath;
            public string manifestSha256 = "";
            public string sourceScene = SourceScene;
            public string sourceSceneSha256Before = "";
            public string sourceSceneSha256After = "";
            public string scene = HubScene;
            public bool sceneCreated;
            public bool sceneSaved;
            public int plateWidth;
            public int plateHeight;
            public string baseImage = "";
            public string baseSha256 = "";
            public float pixelsPerUnit = PixelsPerUnit;
            public float stageScale;
            public float platePixelToCapturePixel;
            public float overscanXPx;
            public float overscanYPx;
            public bool originalPlateFound;
            public bool originalPlateActive;
            public Vector3 stagePosition;
            public Vector3 stageEuler;
            public Vector3 cameraPosition;
            public Vector3 cameraEuler;
            public float cameraOrthographicSize;
            public Vector3 spawn;
            public bool cameraMatchesSource;
            public bool spawnMatchesSource;
            public int captureWidth = CaptureSize.x;
            public int captureHeight = CaptureSize.y;
            public string anchorConvention = "Plate and capture pixels are measured from the top-left corner. "
                + "The anchor is the sprite pivot. Capture values are at 1280x720.";
            public List<LayerRecord> layers = new List<LayerRecord>();
            public List<SampleRecord> samples = new List<SampleRecord>();
            public string baseOnlyCapture = "";
            public string baseOnlySha256 = "";
            public int baseOnlyPixelsChangedFromT0;
            public List<string> checks = new List<string>();
            public List<string> warnings = new List<string>();
        }

        [Serializable]
        private sealed class LayerRecord
        {
            public string id;
            public string image;
            public string asset;
            public string sha256;
            public string motion;
            public int order;
            public float amplitude;
            public float period;
            public float phase;
            public float x;
            public float y;
            public int widthPx;
            public int heightPx;
            public float pivotX;
            public float pivotY;
            public float anchorPlateX;
            public float anchorPlateY;
            public float anchorCaptureExpectedX;
            public float anchorCaptureExpectedY;
            public float anchorCaptureX;
            public float anchorCaptureY;
            public float rectCaptureLeft;
            public float rectCaptureTop;
            public float rectCaptureRight;
            public float rectCaptureBottom;
            public float worstCornerErrorPx;
            public Vector3 anchorWorld;
            public int pixelsOverBaseAtT0;
            public float[] waveAtSamples;
        }

        [Serializable]
        private sealed class SampleRecord
        {
            public float seconds;
            public string file;
            public string sha256;
            public int pixelsChangedFromT0;
        }

        private struct Snapshot
        {
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float cameraSize;
            public Vector3 spawn;
        }

        [MenuItem("Lumon/Build HubWorld And Capture")]
        public static void BuildAndCapture()
        {
            var evidence = new Evidence
            {
                unity = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            };
            CultureInfo culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Camera camera = null;
            var motions = new List<HubLayerMotion>();
            var hidden = new List<SpriteRenderer>();
            try
            {
                evidence.sourceSceneSha256Before = HashFile(SourceScene);
                AssetDatabase.Refresh();

                // Everything that can be wrong with the delivery is found before a scene is touched.
                Manifest manifest = ReadManifest(evidence);
                Material sway = SwayMaterial();
                Sprite baseSprite = Import(ArtFolder + "/" + manifest.baseImage, new Vector2(0.5f, 0.5f),
                    new Vector2Int(manifest.width, manifest.height));
                var sprites = new Sprite[manifest.layers.Length];
                for (int i = 0; i < sprites.Length; i++)
                {
                    LayerRecord record = evidence.layers[i];
                    sprites[i] = Import(record.asset, new Vector2(record.pivotX, record.pivotY),
                        new Vector2Int(record.widthPx, record.heightPx));
                }
                Pass(evidence, $"Manifest valid: {manifest.width}x{manifest.height}, base {manifest.baseImage}, "
                    + $"{sprites.Length} layers imported 1:1 at {PixelsPerUnit} px per unit with their own pivots.");

                Snapshot source = Read(EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single), out _);
                if (!File.Exists(HubScene))
                {
                    Scene origin = SceneManager.GetActiveScene();
                    Require(origin.path == SourceScene, "the source scene is the one being copied");
                    if (!EditorSceneManager.SaveScene(origin, HubScene, true))
                        throw new IOException("HubWorld: could not create " + HubScene + ".");
                    evidence.sceneCreated = true;
                }
                Scene scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
                Require(scene.path == HubScene, "the scene being edited is the Hub scene");

                Snapshot hub = Read(scene, out camera);
                evidence.cameraPosition = hub.cameraPosition;
                evidence.cameraEuler = hub.cameraRotation.eulerAngles;
                evidence.cameraOrthographicSize = hub.cameraSize;
                evidence.spawn = hub.spawn;
                evidence.cameraMatchesSource = hub.cameraPosition == source.cameraPosition
                    && hub.cameraRotation == source.cameraRotation && hub.cameraSize == source.cameraSize;
                evidence.spawnMatchesSource = hub.spawn == source.spawn;
                if (evidence.sceneCreated)
                    Require(evidence.cameraMatchesSource && evidence.spawnMatchesSource,
                        "a new Hub scene starts with the camera and spawn of the source scene");
                else if (!evidence.cameraMatchesSource || !evidence.spawnMatchesSource)
                    evidence.warnings.Add("Camera or spawn of the existing Hub scene differ from the source scene. Left as found.");

                HubStage stage = BuildStage(scene, camera, manifest, baseSprite, sprites, sway, evidence, motions);
                camera.aspect = 16f / 9f;
                VerifyAlignment(camera, stage, manifest, evidence, motions);
                Capture(scene, camera, stage, evidence, motions, hidden);

                foreach (HubLayerMotion motion in motions)
                    motion.ResetNeutral();
                camera.ResetAspect();
                Require(Same(Read(scene, out _), hub), "building and capturing leave the camera and the spawn where they were");
                Require(scene.path == HubScene, "only the Hub scene is saved");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("HubWorld: could not save " + HubScene + ".");
                evidence.sceneSaved = true;
                VerifySavedScene(manifest, hub, evidence);

                evidence.sourceSceneSha256After = HashFile(SourceScene);
                Require(evidence.sourceSceneSha256After == evidence.sourceSceneSha256Before,
                    "Guardian2DPreview.unity is byte-identical after the build");
                Pass(evidence, "Guardian2DPreview.unity is byte-identical before and after.");
                evidence.passed = true;
                Debug.Log($"Lumon HubWorld: {evidence.checks.Count} checks passed, {evidence.layers.Count} layers, "
                    + $"{evidence.warnings.Count} warnings. Scene created: {evidence.sceneCreated}.");
            }
            catch (Exception failure)
            {
                evidence.failure = failure.Message;
                throw;
            }
            finally
            {
                foreach (SpriteRenderer renderer in hidden)
                    if (renderer != null)
                        renderer.enabled = true;
                foreach (HubLayerMotion motion in motions)
                    if (motion != null)
                        motion.ResetNeutral();
                if (camera != null)
                {
                    camera.targetTexture = null;
                    camera.ResetAspect();
                }
                if (evidence.sourceSceneSha256After.Length == 0 && File.Exists(SourceScene))
                    evidence.sourceSceneSha256After = HashFile(SourceScene);
                Directory.CreateDirectory(ReviewDirectory);
                File.WriteAllText(ReviewDirectory + "/evidence.json", JsonUtility.ToJson(evidence, true) + "\n");
                CultureInfo.CurrentCulture = culture;
            }
        }

        // ---------------------------------------------------------------- manifest

        private static Manifest ReadManifest(Evidence evidence)
        {
            if (!File.Exists(ManifestPath))
                throw new FileNotFoundException($"HubWorld: {ManifestPath} has not been delivered. Nothing was built.");
            byte[] bytes = File.ReadAllBytes(ManifestPath);
            evidence.manifestSha256 = Hash(bytes);
            Manifest manifest = JsonUtility.FromJson<Manifest>(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿'));
            if (manifest == null || manifest.width <= 0 || manifest.height <= 0)
                throw new InvalidOperationException("HubWorld: the manifest needs a positive width and height.");
            if (manifest.layers == null || manifest.layers.Length == 0)
                throw new InvalidOperationException("HubWorld: the manifest lists no layers.");
            RequireFileName(manifest.baseImage, "baseImage");

            evidence.plateWidth = manifest.width;
            evidence.plateHeight = manifest.height;
            evidence.baseImage = manifest.baseImage;
            Vector2Int baseSize = PngSize(ArtFolder + "/" + manifest.baseImage, out string baseHash);
            evidence.baseSha256 = baseHash;
            if (baseSize.x != manifest.width || baseSize.y != manifest.height)
                throw new InvalidOperationException(
                    $"HubWorld: {manifest.baseImage} is {baseSize.x}x{baseSize.y}, the manifest says {manifest.width}x{manifest.height}.");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var images = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manifest.layers.Length; i++)
            {
                ManifestLayer layer = manifest.layers[i];
                if (layer == null || string.IsNullOrWhiteSpace(layer.id))
                    throw new InvalidOperationException($"HubWorld: layer {i} has no id.");
                string where = $"layer '{layer.id}'";
                if (layer.id.IndexOfAny(new[] { '/', '\\' }) >= 0 || layer.id == BaseName)
                    throw new InvalidOperationException($"HubWorld: {where} has an id that cannot name a scene object.");
                if (!ids.Add(layer.id))
                    throw new InvalidOperationException($"HubWorld: {where} repeats an earlier id.");
                RequireFileName(layer.image, where + " image");
                if (string.Equals(layer.image, manifest.baseImage, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"HubWorld: {where} uses the base image.");
                if (!images.Add(layer.image))
                    throw new InvalidOperationException(
                        $"HubWorld: {where} reuses {layer.image}. Each layer needs its own PNG, because the pivot is stored in the image import.");
                // Exact names only. Enum.Parse would also accept numbers and other casing.
                if (Array.IndexOf(MotionNames, layer.motion) < 0)
                    throw new InvalidOperationException(
                        $"HubWorld: {where} has motion '{layer.motion}'. Expected one of {string.Join(", ", MotionNames)}.");
                foreach (float number in new[] { layer.x, layer.y, layer.pivotX, layer.pivotY, layer.amplitude, layer.period, layer.phase })
                    if (float.IsNaN(number) || float.IsInfinity(number))
                        throw new InvalidOperationException($"HubWorld: {where} has a number that is not finite.");
                if (layer.pivotX < 0f || layer.pivotX > 1f || layer.pivotY < 0f || layer.pivotY > 1f)
                    throw new InvalidOperationException(
                        $"HubWorld: {where} needs a pivot normalized to 0-1 from the bottom-left corner.");
                if (layer.order <= BaseOrder)
                    throw new InvalidOperationException($"HubWorld: {where} has order {layer.order}, behind the base at {BaseOrder}.");
                if (layer.amplitude < 0f)
                    throw new InvalidOperationException($"HubWorld: {where} has a negative amplitude.");
                bool moves = layer.motion != "Static";
                if (moves && !(layer.period > 0f))
                    throw new InvalidOperationException($"HubWorld: {where} moves and needs a period above zero, in seconds.");
                if ((layer.motion == "Flame" || layer.motion == "Glow") && layer.amplitude > 1f)
                    throw new InvalidOperationException($"HubWorld: {where} is {layer.motion}; its amplitude is a fraction from 0 to 1.");
                if (layer.motion == "Lantern" && layer.amplitude > 90f)
                    throw new InvalidOperationException($"HubWorld: {where} is a Lantern; its amplitude is in degrees, at most 90.");

                string asset = ArtFolder + "/" + layer.image;
                Vector2Int size = PngSize(asset, out string hash);
                if (layer.x >= manifest.width || layer.y >= manifest.height || layer.x + size.x <= 0f || layer.y + size.y <= 0f)
                    throw new InvalidOperationException($"HubWorld: {where} lies outside the {manifest.width}x{manifest.height} plate.");
                if ((layer.motion == "Banner" || layer.motion == "Tree") && layer.amplitude > size.x * 0.5f)
                    throw new InvalidOperationException(
                        $"HubWorld: {where} sways {layer.amplitude} px, more than half its {size.x} px width. Amplitude is in plate pixels.");

                var record = new LayerRecord
                {
                    id = layer.id, image = layer.image, asset = asset, sha256 = hash, motion = layer.motion,
                    order = layer.order, amplitude = layer.amplitude, period = layer.period, phase = layer.phase,
                    x = layer.x, y = layer.y, widthPx = size.x, heightPx = size.y,
                    pivotX = layer.pivotX, pivotY = layer.pivotY,
                    anchorPlateX = layer.x + layer.pivotX * size.x,
                    anchorPlateY = layer.y + (1f - layer.pivotY) * size.y,
                };
                evidence.layers.Add(record);
                if (moves && layer.amplitude == 0f)
                    evidence.warnings.Add($"{layer.id}: motion {layer.motion} with amplitude 0 will not move.");
                if (layer.motion == "Banner" || layer.motion == "Tree")
                    WarnIfSwayWouldClip(asset, layer, evidence);
            }
            return manifest;
        }

        private static void RequireFileName(string name, string what)
        {
            if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name)
                || !name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"HubWorld: {what} must be a PNG file name inside {ArtFolder}, received '{name}'.");
        }

        private static Vector2Int PngSize(string path, out string hash)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"HubWorld: {path} is listed in the manifest and is missing.");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 24 || bytes[0] != 0x89 || bytes[1] != 'P' || bytes[2] != 'N' || bytes[3] != 'G')
                throw new InvalidOperationException($"HubWorld: {path} is not a PNG.");
            hash = Hash(bytes);
            return new Vector2Int(bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19],
                bytes[20] << 24 | bytes[21] << 16 | bytes[22] << 8 | bytes[23]);
        }

        // The sway shifts pixels sideways inside the cut-out rectangle, so paint that reaches the
        // left or right edge would be cut while it moves.
        private static void WarnIfSwayWouldClip(string path, ManifestLayer layer, Evidence evidence)
        {
            int margin = Mathf.CeilToInt(layer.amplitude);
            if (margin <= 0)
                return;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    return;
                Color32[] pixels = texture.GetPixels32();
                int width = texture.width, height = texture.height;
                margin = Mathf.Min(margin, width);
                for (int row = 0; row < height; row++)
                    for (int column = 0; column < margin; column++)
                        if (pixels[row * width + column].a > 16 || pixels[row * width + width - 1 - column].a > 16)
                        {
                            evidence.warnings.Add($"{layer.id}: paint within {margin} px of the left or right edge of {layer.image}. "
                                + $"A {layer.motion} of {layer.amplitude} px needs that much transparent margin on both sides or it will be cut while moving.");
                            return;
                        }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ---------------------------------------------------------------- assets

        private static Sprite Import(string path, Vector2 pivot, Vector2Int expected)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"HubWorld: {path} was not imported as a texture.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = pivot;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            // A full rectangle keeps the UV sway inside the mesh and the layer aligned to its crop.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(expected.x, expected.y)), 2048, 16384);
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new InvalidOperationException($"HubWorld: {path} produced no sprite.");
            if (Mathf.RoundToInt(sprite.rect.width) != expected.x || Mathf.RoundToInt(sprite.rect.height) != expected.y
                || sprite.texture.width != expected.x || sprite.texture.height != expected.y
                || Mathf.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > 0.001f
                || Mathf.Abs(sprite.pivot.x - pivot.x * expected.x) > 0.6f
                || Mathf.Abs(sprite.pivot.y - pivot.y * expected.y) > 0.6f)
                throw new InvalidOperationException(
                    $"HubWorld: {path} imported as {sprite.rect.size} (texture {sprite.texture.width}x{sprite.texture.height}) at "
                    + $"{sprite.pixelsPerUnit} px per unit with pivot {sprite.pivot}. Expected {expected.x}x{expected.y} at {PixelsPerUnit}.");
            return sprite;
        }

        private static Material SwayMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
                throw new InvalidOperationException($"HubWorld: {ShaderPath} is missing.");
            // A broken shader would not stop the batch, it would only draw magenta.
            if (ShaderUtil.ShaderHasError(shader) || !shader.isSupported)
                throw new InvalidOperationException($"HubWorld: {ShaderPath} did not compile for this graphics device.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "HubSway" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            return material;
        }

        // ---------------------------------------------------------------- scene

        private static Snapshot Read(Scene scene, out Camera camera)
        {
            camera = null;
            foreach (Camera candidate in All<Camera>(scene))
                if (camera == null || candidate.CompareTag("MainCamera"))
                    camera = candidate;
            List<GuardianMotion> actors = All<GuardianMotion>(scene);
            Require(camera != null && camera.orthographic, $"{scene.path} has its fixed orthographic camera");
            Require(actors.Count == 1, $"{scene.path} has exactly one GuardianMotion");
            return new Snapshot
            {
                cameraPosition = camera.transform.position,
                cameraRotation = camera.transform.rotation,
                cameraSize = camera.orthographicSize,
                spawn = actors[0].transform.position,
            };
        }

        private static HubStage BuildStage(Scene scene, Camera camera, Manifest manifest, Sprite baseSprite, Sprite[] sprites,
            Material sway, Evidence evidence, List<HubLayerMotion> motions)
        {
            Material plain = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            int sortingLayer = 0;

            // The composed painting stays in the scene, switched off, and gives the stage its place.
            Vector3 centre = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, FallbackDepth));
            Transform original = Find(scene, OriginalPlateName);
            evidence.originalPlateFound = original != null;
            if (original != null)
            {
                centre = original.position;
                var painted = original.GetComponent<SpriteRenderer>();
                if (painted != null)
                {
                    sortingLayer = painted.sortingLayerID;
                    if (plain == null)
                        plain = painted.sharedMaterial;
                }
                original.gameObject.SetActive(false);
            }
            else
                evidence.warnings.Add($"'{OriginalPlateName}' is not in the scene. The stage was placed {FallbackDepth} units in front of the camera.");

            List<HubStage> stages = All<HubStage>(scene);
            Require(stages.Count <= 1, "the scene holds at most one generated stage");
            HubStage stage = stages.Count == 1 ? stages[0] : new GameObject(StageName).AddComponent<HubStage>();
            Require(stage.gameObject.scene == scene, "the stage belongs to the Hub scene");

            // One uniform scale for base and layers: the plate covers the 16:9 view without stretching.
            float viewHeight = 2f * camera.orthographicSize;
            float viewWidth = viewHeight * 16f / 9f;
            float scale = Mathf.Max(viewWidth * PixelsPerUnit / manifest.width, viewHeight * PixelsPerUnit / manifest.height);
            stage.gameObject.SetActive(true);
            stage.transform.SetParent(null, false);
            stage.transform.SetPositionAndRotation(centre, camera.transform.rotation);
            stage.transform.localScale = new Vector3(scale, scale, 1f);
            evidence.stageScale = scale;
            evidence.stagePosition = centre;
            evidence.stageEuler = stage.transform.rotation.eulerAngles;

            SpriteRenderer plate = stage.BaseRenderer;
            if (plate == null)
            {
                plate = new GameObject(BaseName).AddComponent<SpriteRenderer>();
                plate.transform.SetParent(stage.transform, false);
            }
            ResetChild(plate.transform, stage.transform, Vector3.zero, 0);
            plate.sprite = baseSprite;
            plate.sortingLayerID = sortingLayer;
            plate.sortingOrder = BaseOrder;
            plate.color = Color.white;
            plate.flipX = plate.flipY = false;
            plate.drawMode = SpriteDrawMode.Simple;
            plate.enabled = true;
            if (plain != null)
                plate.sharedMaterial = plain;
            stage.Configure(new Vector2Int(manifest.width, manifest.height), plate);

            var existing = new Dictionary<string, HubLayerMotion>(StringComparer.OrdinalIgnoreCase);
            foreach (HubLayerMotion found in stage.GetComponentsInChildren<HubLayerMotion>(true))
            {
                if (found.LayerId.Length > 0 && !existing.ContainsKey(found.LayerId))
                    existing.Add(found.LayerId, found);
                else
                    UnityEngine.Object.DestroyImmediate(found.gameObject);
            }

            int created = 0;
            for (int i = 0; i < manifest.layers.Length; i++)
            {
                ManifestLayer layer = manifest.layers[i];
                LayerRecord record = evidence.layers[i];
                if (existing.TryGetValue(layer.id, out HubLayerMotion motion))
                    existing.Remove(layer.id);
                else
                {
                    motion = new GameObject(layer.id).AddComponent<HubLayerMotion>();
                    motion.transform.SetParent(stage.transform, false);
                    created++;
                }

                var kind = (HubMotion)Array.IndexOf(MotionNames, layer.motion);
                var local = new Vector3((record.anchorPlateX - manifest.width * 0.5f) / PixelsPerUnit,
                    (manifest.height * 0.5f - record.anchorPlateY) / PixelsPerUnit, -LayerDepthStep * (i + 1));
                motion.name = layer.id;
                motion.gameObject.SetActive(true);
                motion.enabled = true;
                ResetChild(motion.transform, stage.transform, local, i + 1);

                var renderer = motion.GetComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                renderer.sortingLayerID = sortingLayer;
                renderer.sortingOrder = layer.order;
                renderer.flipX = renderer.flipY = false;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.enabled = true;
                bool sways = kind == HubMotion.Banner || kind == HubMotion.Tree;
                if (sways)
                    renderer.sharedMaterial = sway;
                else if (plain != null)
                    renderer.sharedMaterial = plain;
                motion.Configure(layer.id, kind, layer.amplitude, layer.period, layer.phase, local);
                EditorUtility.SetDirty(motion);
                EditorUtility.SetDirty(renderer);
                motions.Add(motion);
            }

            // Generated layers that left the manifest go with it. Nothing else in the scene is removed.
            foreach (HubLayerMotion stale in existing.Values)
            {
                Debug.Log($"Lumon HubWorld: removed generated layer '{stale.LayerId}', no longer in the manifest.");
                UnityEngine.Object.DestroyImmediate(stale.gameObject);
            }

            Require(original == null || !original.gameObject.activeInHierarchy, "the composed painting is switched off");
            evidence.originalPlateActive = original != null && original.gameObject.activeInHierarchy;
            Pass(evidence, $"Stage at uniform scale {scale:F6}: base at order {BaseOrder} and {motions.Count} layers "
                + $"({created} new, {motions.Count - created} refreshed, {existing.Count} removed). Composed painting switched off.");
            return stage;
        }

        private static void ResetChild(Transform child, Transform parent, Vector3 localPosition, int index)
        {
            if (child.parent != parent)
                child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.SetSiblingIndex(index);
        }

        // ---------------------------------------------------------------- proof

        // Compares where the camera really draws each sprite with where the manifest says it belongs.
        // The expected values come from the manifest and the 16:9 frame alone, not from the transforms.
        private static void VerifyAlignment(Camera camera, HubStage stage, Manifest manifest, Evidence evidence,
            List<HubLayerMotion> motions)
        {
            float k = Mathf.Max(CaptureSize.x / (float)manifest.width, CaptureSize.y / (float)manifest.height);
            float offsetX = (manifest.width * k - CaptureSize.x) * 0.5f;
            float offsetY = (manifest.height * k - CaptureSize.y) * 0.5f;
            evidence.platePixelToCapturePixel = k;
            evidence.overscanXPx = offsetX;
            evidence.overscanYPx = offsetY;

            Rect drawn = CaptureRect(camera, stage.BaseRenderer, out float depth);
            float worst = Mathf.Max(Mathf.Abs(drawn.xMin + offsetX), Mathf.Abs(drawn.yMin + offsetY),
                Mathf.Abs(drawn.xMax - (manifest.width * k - offsetX)), Mathf.Abs(drawn.yMax - (manifest.height * k - offsetY)));
            Require(worst <= AlignmentTolerancePx, $"the base fills the 16:9 frame at one scale (off by {worst:F3} px)");
            Require(depth > camera.nearClipPlane && depth < camera.farClipPlane, "the stage sits between the camera clip planes");

            float worstLayer = 0f;
            for (int i = 0; i < motions.Count; i++)
            {
                LayerRecord record = evidence.layers[i];
                motions[i].ResetNeutral();
                var renderer = motions[i].GetComponent<SpriteRenderer>();
                drawn = CaptureRect(camera, renderer, out _);
                float left = record.x * k - offsetX, top = record.y * k - offsetY;
                float right = (record.x + record.widthPx) * k - offsetX, bottom = (record.y + record.heightPx) * k - offsetY;
                record.rectCaptureLeft = drawn.xMin;
                record.rectCaptureTop = drawn.yMin;
                record.rectCaptureRight = drawn.xMax;
                record.rectCaptureBottom = drawn.yMax;
                record.worstCornerErrorPx = Mathf.Max(Mathf.Abs(drawn.xMin - left), Mathf.Abs(drawn.yMin - top),
                    Mathf.Abs(drawn.xMax - right), Mathf.Abs(drawn.yMax - bottom));

                Vector2 anchor = CapturePoint(camera, renderer.transform.position, out _);
                record.anchorWorld = renderer.transform.position;
                record.anchorCaptureX = anchor.x;
                record.anchorCaptureY = anchor.y;
                record.anchorCaptureExpectedX = record.anchorPlateX * k - offsetX;
                record.anchorCaptureExpectedY = record.anchorPlateY * k - offsetY;
                float anchorError = Mathf.Max(Mathf.Abs(anchor.x - record.anchorCaptureExpectedX),
                    Mathf.Abs(anchor.y - record.anchorCaptureExpectedY));
                Require(record.worstCornerErrorPx <= AlignmentTolerancePx && anchorError <= AlignmentTolerancePx,
                    $"layer '{record.id}' sits on its plate rectangle in the neutral pose (corners off by "
                    + $"{record.worstCornerErrorPx:F3} px, anchor by {anchorError:F3} px)");
                Require(renderer.sprite != null && renderer.enabled && renderer.gameObject.activeInHierarchy
                    && renderer.sortingOrder == record.order && motions[i].Motion.ToString() == record.motion,
                    $"layer '{record.id}' is present with its sprite, order and motion");
                worstLayer = Mathf.Max(worstLayer, record.worstCornerErrorPx);

                record.waveAtSamples = new float[SampleTimes.Length];
                bool still = true;
                for (int s = 0; s < SampleTimes.Length; s++)
                {
                    record.waveAtSamples[s] = motions[i].Wave(SampleTimes[s]);
                    still &= Mathf.Abs(record.waveAtSamples[s] - record.waveAtSamples[0]) < 0.001f;
                }
                if (still && record.motion != "Static" && record.amplitude > 0f)
                    evidence.warnings.Add($"{record.id}: period {record.period} s and phase {record.phase} put the three samples "
                        + "at the same point of the cycle, so the captures cannot show this layer moving.");
            }
            Pass(evidence, $"Neutral pose: base fills 1280x720 within {worst:F3} px (overscan {offsetX:F2} x {offsetY:F2} px) and all "
                + $"{motions.Count} layers match their manifest rectangles within {worstLayer:F3} px, one plate pixel = {k:F5} capture px.");
        }

        private static Rect CaptureRect(Camera camera, SpriteRenderer renderer, out float depth)
        {
            Bounds bounds = renderer.sprite.bounds;
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            depth = 0f;
            for (int corner = 0; corner < 4; corner++)
            {
                var local = new Vector3(corner % 2 == 0 ? bounds.min.x : bounds.max.x, corner < 2 ? bounds.min.y : bounds.max.y, 0f);
                Vector2 point = CapturePoint(camera, renderer.transform.TransformPoint(local), out depth);
                left = Mathf.Min(left, point.x);
                right = Mathf.Max(right, point.x);
                top = Mathf.Min(top, point.y);
                bottom = Mathf.Max(bottom, point.y);
            }
            return Rect.MinMaxRect(left, top, right, bottom);
        }

        private static Vector2 CapturePoint(Camera camera, Vector3 world, out float depth)
        {
            Vector3 point = camera.WorldToViewportPoint(world);
            depth = point.z;
            return new Vector2(point.x * CaptureSize.x, (1f - point.y) * CaptureSize.y);
        }

        private static void Capture(Scene scene, Camera camera, HubStage stage, Evidence evidence,
            List<HubLayerMotion> motions, List<SpriteRenderer> hidden)
        {
            Directory.CreateDirectory(ReviewDirectory);
            Color32[] first = null;
            for (int s = 0; s < SampleTimes.Length; s++)
            {
                foreach (HubLayerMotion motion in motions)
                    motion.Sample(SampleTimes[s]);
                string file = $"{ReviewDirectory}/unity-{CaptureSize.x}x{CaptureSize.y}-{SampleSuffixes[s]}.png";
                Color32[] pixels = Render(camera, file);
                if (first == null)
                    first = pixels;
                evidence.samples.Add(new SampleRecord
                {
                    seconds = SampleTimes[s], file = file, sha256 = HashFile(file),
                    pixelsChangedFromT0 = Differences(first, pixels, new RectInt(0, 0, CaptureSize.x, CaptureSize.y)),
                });
            }

            // Same framing with nothing but the clean base: what is left when every sprite is taken away.
            foreach (HubLayerMotion motion in motions)
                motion.Sample(SampleTimes[0]);
            foreach (SpriteRenderer renderer in All<SpriteRenderer>(scene))
                if (renderer != stage.BaseRenderer && renderer.enabled)
                {
                    renderer.enabled = false;
                    hidden.Add(renderer);
                }
            evidence.baseOnlyCapture = $"{ReviewDirectory}/unity-{CaptureSize.x}x{CaptureSize.y}-base-only.png";
            Color32[] bare = Render(camera, evidence.baseOnlyCapture);
            foreach (SpriteRenderer renderer in hidden)
                renderer.enabled = true;
            hidden.Clear();
            evidence.baseOnlySha256 = HashFile(evidence.baseOnlyCapture);
            evidence.baseOnlyPixelsChangedFromT0 = Differences(first, bare, new RectInt(0, 0, CaptureSize.x, CaptureSize.y));
            Require(evidence.baseOnlyPixelsChangedFromT0 > 0, "the capture with sprites differs from the base alone");

            foreach (LayerRecord record in evidence.layers)
            {
                int left = Mathf.Clamp(Mathf.FloorToInt(record.rectCaptureLeft), 0, CaptureSize.x);
                int right = Mathf.Clamp(Mathf.CeilToInt(record.rectCaptureRight), 0, CaptureSize.x);
                int top = Mathf.Clamp(Mathf.FloorToInt(record.rectCaptureTop), 0, CaptureSize.y);
                int bottom = Mathf.Clamp(Mathf.CeilToInt(record.rectCaptureBottom), 0, CaptureSize.y);
                record.pixelsOverBaseAtT0 = Differences(first, bare, new RectInt(left, top, right - left, bottom - top));
                if (record.pixelsOverBaseAtT0 == 0)
                    evidence.warnings.Add($"{record.id}: no pixel inside its rectangle differs from the base alone at t=0. "
                        + "The cut-out is empty, hidden behind another layer, or equal to the base underneath.");
            }
            foreach (HubLayerMotion motion in motions)
                motion.ResetNeutral();
            Pass(evidence, $"Captured t = 0, 0.5 and 1.0 s at {CaptureSize.x}x{CaptureSize.y} and the base alone in the same framing. "
                + $"{evidence.baseOnlyPixelsChangedFromT0} pixels belong to sprites at t=0; "
                + $"{evidence.samples[1].pixelsChangedFromT0} changed by 0.5 s and {evidence.samples[2].pixelsChangedFromT0} by 1.0 s.");
        }

        // Rows of the returned pixels run bottom-up, as Unity reads them.
        private static Color32[] Render(Camera camera, string path)
        {
            var target = new RenderTexture(CaptureSize.x, CaptureSize.y, 24);
            var frame = new Texture2D(CaptureSize.x, CaptureSize.y, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)CaptureSize.x / CaptureSize.y;
                camera.Render();
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, CaptureSize.x, CaptureSize.y), 0, 0);
                frame.Apply();
                File.WriteAllBytes(path, frame.EncodeToPNG());
                return frame.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        // Counts pixels that differ inside an area given from the top-left corner.
        private static int Differences(Color32[] a, Color32[] b, RectInt area)
        {
            int count = 0;
            for (int y = area.yMin; y < area.yMax; y++)
            {
                int row = (CaptureSize.y - 1 - y) * CaptureSize.x;
                for (int x = area.xMin; x < area.xMax; x++)
                {
                    Color32 p = a[row + x], q = b[row + x];
                    if (Math.Abs(p.r - q.r) > 2 || Math.Abs(p.g - q.g) > 2 || Math.Abs(p.b - q.b) > 2)
                        count++;
                }
            }
            return count;
        }

        private static void VerifySavedScene(Manifest manifest, Snapshot before, Evidence evidence)
        {
            Scene scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
            Snapshot after = Read(scene, out Camera camera);
            Require(Same(after, before), "the saved Hub scene keeps the fixed camera and the spawn");

            List<GuardianMotion> actors = All<GuardianMotion>(scene);
            Transform guardian = Find(scene, GuardianArtName);
            var guardianArt = guardian != null ? guardian.GetComponent<SpriteRenderer>() : null;
            Require(actors[0].enabled && actors[0].gameObject.activeInHierarchy && guardianArt != null && guardianArt.enabled
                && guardianArt.gameObject.activeInHierarchy && guardianArt.sortingOrder == 0,
                "the guardian keeps its keyboard movement component and its sprite at order 0");

            Transform original = Find(scene, OriginalPlateName);
            Require(original == null || !original.gameObject.activeInHierarchy, "the composed painting is off in the saved scene");

            List<HubStage> stages = All<HubStage>(scene);
            Require(stages.Count == 1 && stages[0].BaseRenderer != null && stages[0].BaseRenderer.sprite != null
                && stages[0].BaseRenderer.sortingOrder == BaseOrder
                && AssetDatabase.GetAssetPath(stages[0].BaseRenderer.sprite) == ArtFolder + "/" + manifest.baseImage,
                "the saved scene has one stage with the clean base");

            List<HubLayerMotion> stored = All<HubLayerMotion>(scene);
            Require(stored.Count == manifest.layers.Length, $"the saved scene holds {manifest.layers.Length} layers");
            foreach (ManifestLayer layer in manifest.layers)
            {
                HubLayerMotion motion = stored.Find(candidate => candidate.LayerId == layer.id);
                var renderer = motion != null ? motion.GetComponent<SpriteRenderer>() : null;
                Require(renderer != null && renderer.sprite != null && renderer.enabled && motion.enabled && motion.gameObject.activeInHierarchy
                    && AssetDatabase.GetAssetPath(renderer.sprite) == ArtFolder + "/" + layer.image
                    && renderer.sortingOrder == layer.order && motion.Motion.ToString() == layer.motion
                    && Mathf.Approximately(motion.Amplitude, layer.amplitude) && Mathf.Approximately(motion.Phase, layer.phase)
                    && (layer.motion == "Static" || Mathf.Approximately(motion.Period, layer.period))
                    && motion.transform.localRotation == Quaternion.identity && motion.transform.localScale == Vector3.one
                    && motion.transform.localPosition.z < 0f,
                    $"layer '{layer.id}' is stored with its sprite, order, motion and neutral pose");
            }
            Pass(evidence, $"Saved scene reloads with the fixed camera, the spawn, GuardianMotion, the guardian at order 0, "
                + $"the composed painting off, the clean base and {stored.Count} layers in neutral pose.");
        }

        // ---------------------------------------------------------------- helpers

        private static List<T> All<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }

        // Finds by name among inactive objects as well, which GameObject.Find does not.
        private static Transform Find(Scene scene, string name)
        {
            foreach (Transform candidate in All<Transform>(scene))
                if (candidate.name == name)
                    return candidate;
            return null;
        }

        private static bool Same(Snapshot a, Snapshot b)
        {
            return a.cameraPosition == b.cameraPosition && a.cameraRotation == b.cameraRotation
                && Mathf.Approximately(a.cameraSize, b.cameraSize) && a.spawn == b.spawn;
        }

        private static string HashFile(string path)
        {
            return Hash(File.ReadAllBytes(path));
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 signature = SHA256.Create())
                return BitConverter.ToString(signature.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void Pass(Evidence evidence, string check)
        {
            evidence.checks.Add(check);
            Debug.Log("Lumon HubWorld: " + check);
        }

        private static void Require(bool condition, string expectation)
        {
            if (!condition)
                throw new InvalidOperationException("HubWorld failed: " + expectation + ".");
        }
    }
}
