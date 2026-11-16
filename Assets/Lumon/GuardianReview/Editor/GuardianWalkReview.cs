using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumon.GuardianReview
{
    // UX-002. Installs the southwest walk cycle in the 2D review scene and proves it through
    // GuardianMotion.Move, the same call the keyboard makes. The scene is saved only when
    // every check passes.
    public static class GuardianWalkReview
    {
        private const GuardianSpriteFacing.Direction Rumo = GuardianSpriteFacing.Direction.SouthWest;
        private const string ScenePath = "Assets/Lumon/GuardianReview/Guardian2DPreview.unity";
        private const string SpriteRoot = "Assets/Lumon/GuardianReview/Art2D";
        private const string WalkFolder = "Walk/SouthWest";
        private const string ClipDirectory = "Art/Guardian2D/walk/southwest/";
        private const string ReviewDirectory = "Art/Guardian2D/review/walk-southwest";
        private const string GroundingWalkFolder = "Walk/SouthWestGrounding";
        private const string GroundingClipDirectory = "Art/Guardian2D/walk/southwest-grounding/";
        private const string GroundingReviewDirectory = "Art/Guardian2D/review/walk-southwest-grounding";
        private const string SubframesWalkFolder = "Walk/SouthWestSubframes";
        private const string SubframesClipDirectory = "Art/Guardian2D/walk/southwest-subframes/";
        private const string SubframesReviewDirectory = "Art/Guardian2D/review/walk-southwest-subframes";
        private const string LayeredWalkFolder = "Walk/SouthWestLayered";
        private const string LayeredClipDirectory = "Art/Guardian2D/walk/southwest-layered/";
        private const string LayeredReviewDirectory = "Art/Guardian2D/review/walk-southwest-layered";
        private const string SelfTestDirectory = "Logs/ux002-walk-selftest";
        private const string PlatePath = "Temp/ux002-walk-plate.png";

        private const float BlockoutSpeed = 2.8f;
        private const float MinimumSpeed = 0.4f;
        private const float MinimumFrameMs = 40f;
        private const float MaximumFrameMs = 250f;
        private const float Slack = 0.0001f;
        private const float Diagonal = 0.70710678f;
        private const int ReviewHeight = 720;
        private const float ReferenceHeight = 165f;

        private static readonly Vector2Int[] Sizes = { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) };
        private static readonly Vector2[] Keys = {
            Vector2.up, new Vector2(1, 1), Vector2.right, new Vector2(1, -1),
            Vector2.down, new Vector2(-1, -1), Vector2.left, new Vector2(-1, 1),
        };

        [Serializable]
        private sealed class Clip
        {
            public string direction;
            public float referenceHeightPx;
            public float cadenceMs;
            public float suggestedDistanceMeters;
            public ClipFrame[] frames;
            public string status;
        }

        [Serializable]
        private sealed class ClipFrame
        {
            public string file;
            public float pivotX;
            public float pivotY;
            public string supportFoot;
        }

        [Serializable]
        private sealed class Evidence
        {
            public string task = "UX-002";
            public string method;
            public bool passed;
            public string failure = "";
            public bool syntheticSelfTest;
            public string note = "";
            public string unity;
            public string generatedUtc;
            public string scene = ScenePath;
            public bool sceneSaved;
            public string clipSource;
            public string clipSha256 = "";
            public string clipStatus = "";
            public string direction;
            public float referenceHeightPx;
            public float cadenceMs;
            public float suggestedDistanceMeters;
            public string interpretation;
            public float cycleSeconds;
            public float speedMetersPerSecond;
            public float blockoutSpeedMetersPerSecond;
            public float pixelsPerUnit;
            public Vector3 spawn;
            public Vector2 minPosition;
            public Vector2 maxPosition;
            public Vector3 cameraPosition;
            public Vector3 cameraEuler;
            public float cameraOrthographicSize;
            public List<FrameRecord> frames = new List<FrameRecord>();
            public List<string> checks = new List<string>();
            public List<CaptureRecord> captures = new List<CaptureRecord>();
            public List<TickRecord> ticks = new List<TickRecord>();
        }

        [Serializable]
        private sealed class FrameRecord
        {
            public int index;
            public string file;
            public string asset = "";
            public string guid = "";
            public string guidBeforeImport = "";
            public string sha256 = "";
            public string supportFoot;
            public float pivotX;
            public float pivotY;
            public int widthPx;
            public int heightPx;
        }

        [Serializable]
        private sealed class TickRecord
        {
            public int tick;
            public string leg;
            public float inputX;
            public float inputZ;
            public float seconds;
            public float x;
            public float z;
            public float dx;
            public float dz;
            public float distance;
            public string direction;
            public int frame;
            public string sprite;
            public float screenX;
            public float screenY;
            public float screenDx;
            public float screenDy;
        }

        [Serializable]
        private sealed class CaptureRecord
        {
            public string label;
            public int afterTick;
            public string direction;
            public int frame;
            public string sprite;
            public float x;
            public float z;
            public float strideMeters;
            public string file720;
            public string file1080;
            public Silhouette at720;
            public Silhouette at1080;
        }

        // Pixels that differ from the same render without the guardian sprite, measured
        // from the top-left corner like Art/Guardian2D/measure_captures.py.
        [Serializable]
        private sealed class Silhouette
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
            public int height;
            public float anchorX;
            public float anchorY;
            public float bottomMinusAnchor;
            public string sha256;
        }

        private sealed class Stride
        {
            public int frame = -1;
            public int advances;
            public float metres;
            public Vector2 screenStep;
        }

        // Batch entry point. Reads Art/Guardian2D/walk/southwest/clip.json and the PNGs beside it.
        public static void InstallAndVerifySouthWest()
        {
            string path = ClipDirectory + "clip.json";
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"UX-002: {path} has not been delivered. Nothing was imported and the scene is untouched.");
            byte[] manifest = File.ReadAllBytes(path);
            Clip clip = JsonUtility.FromJson<Clip>(System.Text.Encoding.UTF8.GetString(manifest));
            var evidence = NewEvidence(nameof(InstallAndVerifySouthWest), path);
            using (var signature = SHA256.Create())
                evidence.clipSha256 = Hex(signature.ComputeHash(manifest));
            // The art keeps its own acceptance state. This run proves the integration only.
            evidence.clipStatus = clip != null && clip.status != null ? clip.status : "";
            Validate(clip, evidence, checkFiles: true);
            Run(clip, evidence, ReviewDirectory, synthetic: false);
        }

        // Diagnostic batch entry point for the grounding pass. Reads
        // Art/Guardian2D/walk/southwest-grounding/clip.json and the PNGs beside it, imports them
        // into their own sprite folder and never saves the scene, whatever the manifest status says.
        public static void ReviewSouthWestGrounding()
        {
            ReviewDiagnostic(nameof(ReviewSouthWestGrounding), GroundingClipDirectory, GroundingReviewDirectory,
                GroundingWalkFolder);
        }

        // Diagnostic batch entry point for the third proof, the 18-subframe cycle. Reads
        // Art/Guardian2D/walk/southwest-subframes/clip.json and the PNGs beside it, imports them
        // into their own sprite folder and never saves the scene, whatever the manifest status says.
        public static void ReviewSouthWestSubframes()
        {
            ReviewDiagnostic(nameof(ReviewSouthWestSubframes), SubframesClipDirectory, SubframesReviewDirectory,
                SubframesWalkFolder);
        }

        // Diagnostic batch entry point for the layered cycle. Reads
        // Art/Guardian2D/walk/southwest-layered/clip.json and the PNGs beside it, imports them
        // into their own sprite folder and never saves the scene, whatever the manifest status says.
        public static void ReviewSouthWestLayered()
        {
            ReviewDiagnostic(nameof(ReviewSouthWestLayered), LayeredClipDirectory, LayeredReviewDirectory,
                LayeredWalkFolder);
        }

        // Shared by the diagnostic entries. Same validation and proof as the install, with its own
        // clip, sprite folder and output, and the scene left unsaved.
        private static void ReviewDiagnostic(string method, string clipDirectory, string reviewDirectory, string walkFolder)
        {
            string path = clipDirectory + "clip.json";
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"UX-002: {path} has not been delivered. Nothing was imported and the scene is untouched.");
            byte[] manifest = File.ReadAllBytes(path);
            Clip clip = JsonUtility.FromJson<Clip>(System.Text.Encoding.UTF8.GetString(manifest));
            var evidence = NewEvidence(method, path);
            evidence.note = "Diagnostic only. The scene is never saved by this entry.";
            using (var signature = SHA256.Create())
                evidence.clipSha256 = Hex(signature.ComputeHash(manifest));
            evidence.clipStatus = clip != null && clip.status != null ? clip.status : "";
            Validate(clip, evidence, checkFiles: true, clipDirectory);
            Run(clip, evidence, reviewDirectory, synthetic: false, clipDirectory, walkFolder, diagnostic: true);
        }

        // Logic rehearsal for the same checks with in-memory stand-in sprites. It imports nothing,
        // never saves the scene and deletes its renders, so it is not visual evidence.
        public static void SelfTest()
        {
            var clip = new Clip
            {
                direction = Rumo.ToString(), referenceHeightPx = 200f, cadenceMs = 110f,
                suggestedDistanceMeters = 1.2f, frames = new ClipFrame[8],
            };
            for (int i = 0; i < clip.frames.Length; i++)
                clip.frames[i] = new ClipFrame
                {
                    file = $"frame-{i:00}.png", pivotX = 0.5f, pivotY = 10f / 240f,
                    supportFoot = i < 4 ? "left" : "right",
                };
            var evidence = NewEvidence(nameof(SelfTest), "in-memory stand-ins");
            evidence.syntheticSelfTest = true;
            evidence.note = "Logic rehearsal with generated rectangles. Not the delivered art, not acceptance evidence.";
            Validate(clip, evidence, checkFiles: false);
            Run(clip, evidence, SelfTestDirectory, synthetic: true);
        }

        private static Evidence NewEvidence(string method, string source)
        {
            return new Evidence
            {
                method = "Lumon.GuardianReview.GuardianWalkReview." + method,
                unity = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                clipSource = source,
                interpretation = "cadenceMs is the time one frame stays on screen. suggestedDistanceMeters is the ground "
                    + "covered by one full cycle. Scene speed = suggestedDistanceMeters / (frames * cadenceMs / 1000). "
                    + "referenceHeightPx is the standing height in texture pixels that must read 165 px at 720p.",
            };
        }

        private static void Validate(Clip clip, Evidence evidence, bool checkFiles, string clipDirectory = ClipDirectory)
        {
            if (clip == null || clip.frames == null || clip.frames.Length < 4)
                throw new InvalidOperationException("UX-002: clip.json needs at least four frames.");
            if (clip.direction != Rumo.ToString())
                throw new InvalidOperationException($"UX-002: expected direction {Rumo}, received '{clip.direction}'.");
            if (clip.referenceHeightPx < ReferenceHeight)
                throw new InvalidOperationException(
                    $"UX-002: referenceHeightPx {clip.referenceHeightPx} is below the {ReferenceHeight} px shown at 720p.");
            if (clip.cadenceMs < MinimumFrameMs || clip.cadenceMs > MaximumFrameMs)
                throw new InvalidOperationException(
                    $"UX-002: cadenceMs {clip.cadenceMs} is read as the time per frame and must stay within "
                    + $"{MinimumFrameMs}-{MaximumFrameMs} ms. The source plays at 110 ms per frame. Confirm the unit.");

            float cycleSeconds = clip.frames.Length * clip.cadenceMs / 1000f;
            float speed = clip.suggestedDistanceMeters / cycleSeconds;
            if (!(speed >= MinimumSpeed && speed <= BlockoutSpeed))
                throw new InvalidOperationException(
                    $"UX-002: {clip.suggestedDistanceMeters} m per cycle over {cycleSeconds:F3} s gives {speed:F3} m/s, "
                    + $"outside {MinimumSpeed}-{BlockoutSpeed} m/s. suggestedDistanceMeters is read as the ground covered "
                    + "by one full cycle. Confirm the unit.");

            evidence.direction = clip.direction;
            evidence.referenceHeightPx = clip.referenceHeightPx;
            evidence.cadenceMs = clip.cadenceMs;
            evidence.suggestedDistanceMeters = clip.suggestedDistanceMeters;
            evidence.cycleSeconds = cycleSeconds;
            evidence.speedMetersPerSecond = speed;
            evidence.blockoutSpeedMetersPerSecond = BlockoutSpeed;
            evidence.pixelsPerUnit = GuardianSpriteReview.PixelsPerUnit(clip.referenceHeightPx);

            var fingerprints = new HashSet<string>();
            using var signature = SHA256.Create();
            for (int i = 0; i < clip.frames.Length; i++)
            {
                ClipFrame frame = clip.frames[i];
                if (frame == null || frame.file != $"frame-{i:00}.png")
                    throw new InvalidOperationException($"UX-002: frame {i} must be named frame-{i:00}.png, in order.");
                if (!(frame.pivotX > 0f && frame.pivotX < 1f && frame.pivotY > 0f && frame.pivotY < 1f))
                    throw new InvalidOperationException(
                        $"UX-002: {frame.file} needs a normalized pivot inside the image, from the bottom-left corner.");
                if (string.IsNullOrWhiteSpace(frame.supportFoot))
                    throw new InvalidOperationException($"UX-002: {frame.file} has no supportFoot.");

                var record = new FrameRecord
                {
                    index = i, file = frame.file, supportFoot = frame.supportFoot,
                    pivotX = frame.pivotX, pivotY = frame.pivotY,
                };
                if (checkFiles)
                {
                    string source = clipDirectory + frame.file;
                    if (!File.Exists(source))
                        throw new FileNotFoundException($"UX-002: {source} is listed in clip.json and is missing.");
                    byte[] bytes = File.ReadAllBytes(source);
                    if (bytes.Length < 24 || bytes[0] != 0x89 || bytes[1] != 'P' || bytes[2] != 'N' || bytes[3] != 'G')
                        throw new InvalidOperationException($"UX-002: {source} is not a PNG.");
                    record.widthPx = bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19];
                    record.heightPx = bytes[20] << 24 | bytes[21] << 16 | bytes[22] << 8 | bytes[23];
                    record.sha256 = Hex(signature.ComputeHash(bytes));
                    if (!fingerprints.Add(record.sha256))
                        throw new InvalidOperationException($"UX-002: {frame.file} repeats the image of an earlier frame.");
                }
                evidence.frames.Add(record);
            }
        }

        private static void Run(Clip clip, Evidence evidence, string output, bool synthetic,
            string clipDirectory = ClipDirectory, string walkFolder = WalkFolder, bool diagnostic = false)
        {
            var standIns = new List<UnityEngine.Object>();
            // Numbers in the log and in the evidence read the same on every machine.
            CultureInfo culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                VerifyBlockoutPace(evidence);
                Sprite[] frames = synthetic ? CreateStandIns(clip, evidence, standIns)
                    : ImportFrames(clip, evidence, clipDirectory, walkFolder);

                GuardianSpriteReview.Open();
                var proof = new Proof(clip, frames, evidence, output);
                Directory.CreateDirectory(output);
                Directory.CreateDirectory(Path.GetDirectoryName(PlatePath));
                proof.Configure();
                proof.VerifyRestPoses();
                proof.WalkToNorthEastCorner();
                proof.VerifyStride();
                proof.VerifyCornerArrival();
                proof.Restore();

                if (!synthetic && !diagnostic && string.Equals(clip.status, "accepted", StringComparison.Ordinal))
                {
                    proof.Save();
                    VerifySavedScene(frames, clip.suggestedDistanceMeters, evidence);
                }
                evidence.passed = true;
                Debug.Log($"Lumon UX-002: {evidence.checks.Count} checks passed, {evidence.captures.Count} captures, "
                    + $"{evidence.ticks.Count} ticks. Scene saved: {evidence.sceneSaved}.");
            }
            catch (Exception failure)
            {
                evidence.failure = failure.Message;
                throw;
            }
            finally
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(output + "/evidence.json", JsonUtility.ToJson(evidence, true) + "\n");
                if (File.Exists(PlatePath))
                    File.Delete(PlatePath);
                if (synthetic)
                    foreach (string render in Directory.GetFiles(output, "*.png"))
                        File.Delete(render);
                // Whatever was not saved is dropped from memory as well.
                if (!evidence.sceneSaved)
                    GuardianSpriteReview.Open();
                foreach (UnityEngine.Object standIn in standIns)
                    UnityEngine.Object.DestroyImmediate(standIn);
                CultureInfo.CurrentCulture = culture;
            }
        }

        // The 3D review scene has no stored speed, so it must still move and turn at its original pace.
        private static void VerifyBlockoutPace(Evidence evidence)
        {
            GuardianReviewScene.Open();
            GuardianMotion pivot = UnityEngine.Object.FindAnyObjectByType<GuardianMotion>();
            if (pivot == null)
                throw new InvalidOperationException("UX-002: the 3D review scene lost its GuardianMotion.");
            Vector3 start = pivot.transform.position;
            Quaternion facing = pivot.transform.rotation;
            pivot.Move(Vector2.up, 1f / 30f);
            float travelled = (pivot.transform.position - start).magnitude;
            float turned = Quaternion.Angle(facing, pivot.transform.rotation);
            if (pivot.Speed != BlockoutSpeed || Mathf.Abs(travelled - BlockoutSpeed / 30f) > Slack
                || Mathf.Abs(turned - 720f / 30f) > 0.01f)
                throw new InvalidOperationException(
                    $"UX-002: the 3D review pace changed. Speed {pivot.Speed}, moved {travelled:F5} m, turned {turned:F2} degrees.");
            evidence.checks.Add($"3D review scene keeps {BlockoutSpeed} m/s and its 720 degrees per second turn. Not saved.");
        }

        private static Sprite[] ImportFrames(Clip clip, Evidence evidence, string clipDirectory, string walkFolder)
        {
            if (!AssetDatabase.IsValidFolder(SpriteRoot + "/Walk"))
                AssetDatabase.CreateFolder(SpriteRoot, "Walk");
            if (!AssetDatabase.IsValidFolder(SpriteRoot + "/" + walkFolder))
                AssetDatabase.CreateFolder(SpriteRoot + "/Walk", Path.GetFileName(walkFolder));

            var sprites = new Sprite[clip.frames.Length];
            int kept = 0;
            for (int i = 0; i < sprites.Length; i++)
            {
                ClipFrame frame = clip.frames[i];
                FrameRecord record = evidence.frames[i];
                string asset = SpriteRoot + "/" + walkFolder + "/" + frame.file;
                record.asset = asset;
                record.guidBeforeImport = MetaGuid(asset);

                // Overwrite the bytes in place. The .meta beside the file is never removed,
                // so references already stored in the scene keep pointing at the same asset.
                byte[] bytes = File.ReadAllBytes(clipDirectory + frame.file);
                if (!File.Exists(asset) || !Same(bytes, File.ReadAllBytes(asset)))
                    File.WriteAllBytes(asset, bytes);
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);

                Sprite sprite = GuardianSpriteReview.Import(walkFolder + "/" + frame.file, evidence.pixelsPerUnit,
                    new Vector2(frame.pivotX, frame.pivotY), true);
                record.guid = AssetDatabase.AssetPathToGUID(asset);
                if (sprite == null || record.guid != MetaGuid(asset))
                    throw new InvalidOperationException($"UX-002: could not import {asset}.");
                if (record.guidBeforeImport.Length > 0)
                {
                    if (record.guidBeforeImport != record.guid)
                        throw new InvalidOperationException($"UX-002: {asset} changed GUID on reimport.");
                    kept++;
                }
                if (Mathf.RoundToInt(sprite.rect.width) != record.widthPx
                    || Mathf.RoundToInt(sprite.rect.height) != record.heightPx
                    || Mathf.Abs(sprite.pixelsPerUnit - evidence.pixelsPerUnit) > 0.01f
                    || Mathf.Abs(sprite.pivot.x - frame.pivotX * record.widthPx) > 0.6f
                    || Mathf.Abs(sprite.pivot.y - frame.pivotY * record.heightPx) > 0.6f)
                    throw new InvalidOperationException(
                        $"UX-002: {asset} imported as {sprite.rect.size} at {sprite.pixelsPerUnit} px per unit with pivot "
                        + $"{sprite.pivot}. Expected {record.widthPx}x{record.heightPx} at {evidence.pixelsPerUnit}.");
                sprites[i] = sprite;
            }

            foreach (string stale in Directory.GetFiles(SpriteRoot + "/" + walkFolder, "*.png"))
                if (Array.FindIndex(clip.frames, frame => frame.file == Path.GetFileName(stale)) < 0)
                    Debug.LogWarning($"Lumon UX-002: {stale} is no longer listed in clip.json and was left in place.");
            evidence.checks.Add($"Imported {sprites.Length} distinct PNGs at {evidence.pixelsPerUnit:F2} px per unit with their "
                + $"own pivots. {kept} already existed and kept their GUIDs.");
            return sprites;
        }

        private static Sprite[] CreateStandIns(Clip clip, Evidence evidence, List<UnityEngine.Object> owned)
        {
            const int width = 120, height = 240, sole = 10;
            var sprites = new Sprite[clip.frames.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[width * height];
                Color32 tint = Color.HSVToRGB(i / (float)sprites.Length, 0.9f, 1f);
                for (int y = sole; y < sole + (int)clip.referenceHeightPx; y++)
                    for (int x = 40 + i * 2; x < 80; x++)
                        pixels[y * width + x] = tint;
                texture.SetPixels32(pixels);
                texture.Apply();
                sprites[i] = Sprite.Create(texture, new Rect(0, 0, width, height),
                    new Vector2(clip.frames[i].pivotX, clip.frames[i].pivotY), evidence.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = "stand-in-" + clip.frames[i].file;
                sprites[i].hideFlags = HideFlags.HideAndDontSave;
                owned.Add(sprites[i]);
                owned.Add(texture);
                evidence.frames[i].widthPx = width;
                evidence.frames[i].heightPx = height;
            }
            evidence.checks.Add($"Created {sprites.Length} in-memory stand-ins. No file was imported.");
            return sprites;
        }

        private static void VerifySavedScene(Sprite[] frames, float distancePerCycle, Evidence evidence)
        {
            GuardianSpriteReview.Open();
            GuardianMotion actor = UnityEngine.Object.FindAnyObjectByType<GuardianMotion>();
            GuardianSpriteFacing facing = UnityEngine.Object.FindAnyObjectByType<GuardianSpriteFacing>();
            SerializedProperty cycle = FindCycle(new SerializedObject(facing).FindProperty("walkCycles"));
            bool stored = cycle != null && actor != null
                && Mathf.Approximately(cycle.FindPropertyRelative("distancePerCycle").floatValue, distancePerCycle)
                && Mathf.Approximately(actor.Speed, evidence.speedMetersPerSecond)
                && cycle.FindPropertyRelative("frames").arraySize == frames.Length;
            for (int i = 0; stored && i < frames.Length; i++)
                stored = cycle.FindPropertyRelative("frames").GetArrayElementAtIndex(i).objectReferenceValue == frames[i];
            if (!stored)
                throw new InvalidOperationException("UX-002: the saved scene did not reload with the southwest cycle.");
            evidence.checks.Add("Saved scene reloads with one southwest cycle, the imported frames in order and the calibrated speed.");
        }

        private static SerializedProperty FindCycle(SerializedProperty cycles)
        {
            SerializedProperty found = null;
            for (int i = 0; i < cycles.arraySize; i++)
            {
                SerializedProperty cycle = cycles.GetArrayElementAtIndex(i);
                if (cycle.FindPropertyRelative("direction").intValue != (int)Rumo)
                    continue;
                if (found != null)
                    throw new InvalidOperationException($"UX-002: the scene stores more than one {Rumo} walk cycle.");
                found = cycle;
            }
            return found;
        }

        private static string MetaGuid(string asset)
        {
            if (!File.Exists(asset + ".meta"))
                return "";
            Match match = Regex.Match(File.ReadAllText(asset + ".meta"), @"^guid:\s*([0-9a-f]{32})", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : "";
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        private static string Hex(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // One opened review scene, driven only through GuardianMotion.Move.
        private sealed class Proof
        {
            private readonly Clip clip;
            private readonly Sprite[] frames;
            private readonly Evidence evidence;
            private readonly string output;
            private readonly Camera camera;
            private readonly GuardianMotion actor;
            private readonly GuardianSpriteFacing facing;
            private readonly SpriteRenderer renderer;
            private readonly Sprite[] idles = new Sprite[8];
            private readonly Vector3 spawn;
            private readonly Vector3 artOffset;
            private readonly Vector2 min;
            private readonly Vector2 max;
            private readonly int startingDirection;
            private readonly float speed;
            private readonly float distancePerCycle;
            private readonly Vector3 cameraPosition;
            private readonly Quaternion cameraRotation;
            private readonly float cameraSize;
            private readonly Matrix4x4 cameraView;
            private Vector2 screen;
            private int ticks;

            public Proof(Clip clip, Sprite[] frames, Evidence evidence, string output)
            {
                this.clip = clip;
                this.frames = frames;
                this.evidence = evidence;
                this.output = output;
                speed = evidence.speedMetersPerSecond;
                distancePerCycle = clip.suggestedDistanceMeters;

                camera = Camera.main;
                actor = UnityEngine.Object.FindAnyObjectByType<GuardianMotion>();
                GameObject art = GameObject.Find("Guardian illustration");
                renderer = art != null ? art.GetComponent<SpriteRenderer>() : null;
                facing = art != null ? art.GetComponent<GuardianSpriteFacing>() : null;
                if (camera == null || actor == null || renderer == null || facing == null)
                    throw new InvalidOperationException("UX-002: the 2D review scene needs its camera, GuardianMotion and "
                        + "GuardianSpriteFacing. Run GuardianDirectionalReview.InstallAndVerify first.");

                var motion = new SerializedObject(actor);
                min = motion.FindProperty("minPosition").vector2Value;
                max = motion.FindProperty("maxPosition").vector2Value;
                Require(motion.FindProperty("directionalArt").objectReferenceValue == facing
                    && !motion.FindProperty("turnTowardMovement").boolValue,
                    "the 2D actor drives the sprite facing and does not rotate");

                var visual = new SerializedObject(facing);
                startingDirection = visual.FindProperty("startingDirection").intValue;
                SerializedProperty poses = visual.FindProperty("idleByDirection");
                Require(poses.arraySize == idles.Length, "eight rest poses are stored");
                for (int i = 0; i < idles.Length; i++)
                {
                    idles[i] = poses.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    string expected = $"{SpriteRoot}/Directions/{(GuardianSpriteFacing.Direction)i}.png";
                    Require(idles[i] != null && AssetDatabase.GetAssetPath(idles[i]) == expected
                        && Array.IndexOf(frames, idles[i]) < 0, $"the rest pose of rumo {i} is still {expected}");
                }

                camera.aspect = 16f / 9f;
                cameraPosition = camera.transform.position;
                cameraRotation = camera.transform.rotation;
                cameraSize = camera.orthographicSize;
                cameraView = camera.worldToCameraMatrix;
                spawn = actor.transform.position;
                artOffset = renderer.transform.localPosition;
                screen = ScreenPoint();

                evidence.spawn = spawn;
                evidence.minPosition = min;
                evidence.maxPosition = max;
                evidence.cameraPosition = cameraPosition;
                evidence.cameraEuler = cameraRotation.eulerAngles;
                evidence.cameraOrthographicSize = cameraSize;
            }

            // Writes the cycle and the pace into the loaded components, as the Inspector would.
            public void Configure()
            {
                var visual = new SerializedObject(facing);
                SerializedProperty cycles = visual.FindProperty("walkCycles");
                SerializedProperty cycle = FindCycle(cycles);
                if (cycle == null)
                {
                    cycles.arraySize++;
                    cycle = cycles.GetArrayElementAtIndex(cycles.arraySize - 1);
                }
                cycle.FindPropertyRelative("direction").intValue = (int)Rumo;
                cycle.FindPropertyRelative("distancePerCycle").floatValue = distancePerCycle;
                SerializedProperty stored = cycle.FindPropertyRelative("frames");
                stored.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    stored.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                visual.ApplyModifiedPropertiesWithoutUndo();

                var motion = new SerializedObject(actor);
                motion.FindProperty("speed").floatValue = speed;
                motion.ApplyModifiedPropertiesWithoutUndo();
                Require(Mathf.Approximately(actor.Speed, speed), "the 2D scene stores the calibrated speed");
                Pass($"2D scene walks at {speed:F4} m/s: {distancePerCycle} m per cycle, {frames.Length} frames of "
                    + $"{clip.cadenceMs} ms. Only {Rumo} has a walk cycle.");
            }

            public void VerifyRestPoses()
            {
                GuardianDirectionalReview.VerifyIdles(actor, facing, renderer, camera, idles, null);
                Pass("GuardianDirectionalReview.VerifyIdles, the check inside InstallAndVerify, passes at the new speed.");

                const float seconds = 1f / 60f;
                for (int i = 0; i < Keys.Length; i++)
                {
                    if (i == (int)Rumo)
                        continue;
                    actor.transform.position = spawn;
                    screen = ScreenPoint();
                    for (int n = 0; n < 3; n++)
                    {
                        TickRecord moving = Step("rest-pose-" + (GuardianSpriteFacing.Direction)i, Keys[i], seconds);
                        Require(moving.frame < 0 && renderer.sprite == idles[i]
                            && Mathf.Abs(moving.distance - speed * seconds) < Slack,
                            $"rumo {(GuardianSpriteFacing.Direction)i} keeps its rest pose while moving at full speed");
                    }
                    TickRecord stopped = Step("rest-pose-" + (GuardianSpriteFacing.Direction)i, Vector2.zero, seconds);
                    Require(renderer.sprite == idles[i] && stopped.distance == 0f,
                        $"rumo {(GuardianSpriteFacing.Direction)i} keeps its rest pose after stopping");
                }
                actor.transform.position = spawn;
                screen = ScreenPoint();
                Pass("The other seven rumos show their rest pose while moving and after stopping. Diagonals cover the same ground per tick.");
            }

            // Reaches the start of the proof on foot, which also exercises the opposite corner.
            public void WalkToNorthEastCorner()
            {
                int blocked = 0;
                for (int guard = 0; blocked < 2; guard++)
                {
                    Require(guard < 5000, "the guardian reaches the north-east corner");
                    TickRecord tick = Step("to-north-east-corner", Keys[1], 1f / 60f);
                    Require(tick.frame < 0, "no walk frame appears on the way north-east");
                    blocked = tick.distance <= GuardianSpriteFacing.RestDistance ? blocked + 1 : 0;
                }
                Require(AtCorner(max) && renderer.sprite == idles[1],
                    "pressing north-east in the north-east corner rests facing north-east");
                Pass("Walked from the spawn to the north-east corner with rest poses only, then stayed blocked there.");
            }

            // One continuous southwest walk: start, full cycle, wrap, pause, release, resume, limit, corner.
            public void VerifyStride()
            {
                const float seconds = 1f / 60f;
                float step = speed * seconds;
                float room = Mathf.Min(max.x - min.x, max.y - min.y) / Diagonal;
                Require(room >= 2.6f * distancePerCycle,
                    $"the plaza offers {room:F2} m toward the southwest, enough for this walk with cycles of {distancePerCycle} m");
                Require(step < distancePerCycle / frames.Length, "a 60 Hz tick is shorter than one frame of the cycle");

                Capture("rest-before-stride", 0f);
                var stride = new Stride();
                var order = new List<int>();
                Vector3 lastCapture = actor.transform.position;
                for (int guard = 0; order.Count <= frames.Length + 1 || stride.frame != frames.Length / 2; guard++)
                {
                    Require(guard < 5000, "the stride reaches its second cycle");
                    TickRecord tick = Step("stride", Keys[(int)Rumo], seconds);
                    Require(IsFree(tick, step), "the first two cycles run clear of the limits");
                    int before = stride.frame;
                    ObserveFree(stride, tick, step);
                    if (stride.frame == before)
                        continue;
                    order.Add(stride.frame);
                    if (order.Count > frames.Length + 1)
                        continue;
                    Require((actor.transform.position - lastCapture).magnitude > step * 0.5f,
                        "each captured frame is taken at a new place on the plaza");
                    lastCapture = actor.transform.position;
                    Capture(order.Count > frames.Length ? $"wrap-frame-{stride.frame:00}" : $"frame-{stride.frame:00}",
                        stride.metres);
                }
                for (int i = 0; i <= frames.Length; i++)
                    Require(order[i] == i % frames.Length, "frames play once each, in order, and wrap to the first");
                Pass($"Stride starts on frame 0 at the first tick, shows frames 0-{frames.Length - 1} once each in order and wraps "
                    + $"to frame 0 after {distancePerCycle} m. Each change lands within one tick of its {clip.cadenceMs} ms slot.");

                TickRecord paused = Step("zero-length-tick", Keys[(int)Rumo], 0f);
                Require(paused.frame == stride.frame && paused.distance == 0f && facing.Current == Rumo,
                    "a zero-length tick with the key held keeps the stride frame");
                Pass("A zero-length tick with the key held does not reset the stride.");

                for (int n = 0; n < 2; n++)
                {
                    TickRecord released = Step("release", Vector2.zero, seconds);
                    Require(renderer.sprite == idles[(int)Rumo] && released.distance == 0f && facing.Current == Rumo,
                        "releasing mid-stride returns to the southwest rest pose in place");
                }
                Capture("rest-after-release", 0f);
                Pass($"Releasing on frame {stride.frame} returns to the southwest rest pose without moving.");

                // Resume, then turn mid-stride onto a rumo without walk art and back, key never released.
                stride = new Stride();
                for (int guard = 0; stride.frame != frames.Length / 2; guard++)
                {
                    Require(guard < 5000, "the resumed stride reaches its middle");
                    TickRecord tick = Step("resume", Keys[(int)Rumo], seconds);
                    Require(IsFree(tick, step), "the resumed stride runs clear of the limits");
                    ObserveFree(stride, tick, step);
                }
                const int west = (int)GuardianSpriteFacing.Direction.West;
                for (int n = 0; n < 2; n++)
                {
                    TickRecord turned = Step("turn-west", Keys[west], seconds);
                    Require(turned.frame < 0 && renderer.sprite == idles[west] && Mathf.Abs(turned.distance - step) < Slack,
                        "west has no walk art yet, so it moves at full speed on its rest pose");
                }
                stride = new Stride();
                TickRecord back = Step("turn-back-southwest", Keys[(int)Rumo], seconds);
                Require(IsFree(back, step), "the turn back to southwest runs clear of the limits");
                ObserveFree(stride, back, step);
                Pass($"Resumed from frame 0. Turning west on frame {frames.Length / 2} without releasing showed the west rest pose, "
                    + "and turning back to southwest started the stride on frame 0.");

                int blocked = 0, slides = 0, wraps = 0;
                bool limit = false;
                for (int guard = 0; blocked < 5; guard++)
                {
                    Require(guard < 5000, "the guardian reaches the south-west corner");
                    TickRecord tick = Step("to-south-west-corner", Keys[(int)Rumo], seconds);
                    if (IsFree(tick, step))
                    {
                        Require(!limit, "free movement does not resume after touching a limit");
                        int before = stride.frame;
                        ObserveFree(stride, tick, step);
                        if (before == frames.Length - 1 && stride.frame == 0)
                            wraps++;
                        continue;
                    }

                    limit = true;
                    if (tick.distance <= GuardianSpriteFacing.RestDistance)
                    {
                        blocked++;
                        Require(AtCorner(min) && renderer.sprite == idles[(int)Rumo] && facing.Current == Rumo,
                            "pressing southwest in the south-west corner rests facing southwest, without moving");
                        continue;
                    }
                    Require(blocked == 0, "the guardian does not leave the corner while southwest is held");
                    if (tick.dx == 0f || tick.dz == 0f)
                    {
                        GuardianSpriteFacing.Direction along = tick.dx == 0f
                            ? GuardianSpriteFacing.Direction.South : GuardianSpriteFacing.Direction.West;
                        Require(facing.Current == along && renderer.sprite == idles[(int)along],
                            "sliding along a limit faces the real displacement with its rest pose");
                        slides++;
                    }
                }
                Require(slides > 0, "the walk meets one limit before the corner");
                Capture("rest-corner-blocked", 0f);
                Pass($"Kept walking with {wraps} more wrap(s) and the key held, slid {slides} ticks along the limit with a rest "
                    + "pose and stayed blocked in the south-west corner on the southwest rest pose.");
            }

            // Leaves the corner along the exact diagonal and returns at 30 Hz, so the walk itself meets the corner.
            public void VerifyCornerArrival()
            {
                const float seconds = 1f / 30f;
                float step = speed * seconds;
                Require(step < distancePerCycle / frames.Length, "a 30 Hz tick is shorter than one frame of the cycle");
                int away = Mathf.CeilToInt(0.75f * distancePerCycle / step);
                for (int n = 0; n < away; n++)
                {
                    TickRecord tick = Step("leave-corner", Keys[1], seconds);
                    Require(tick.frame < 0 && renderer.sprite == idles[1] && Mathf.Abs(tick.distance - step) < Slack,
                        "north-east out of the corner moves on its rest pose");
                }
                Step("leave-corner", Vector2.zero, seconds);
                Require(renderer.sprite == idles[1], "north-east rest pose after release");

                var stride = new Stride();
                int free = 0;
                for (int guard = 0; ; guard++)
                {
                    Require(guard < away + 5, "the return reaches the corner");
                    TickRecord tick = Step("return-to-corner", Keys[(int)Rumo], seconds);
                    Require(facing.Current == Rumo, "the return never turns away from southwest");
                    if (tick.distance <= GuardianSpriteFacing.RestDistance)
                        break;
                    if (IsFree(tick, step))
                    {
                        ObserveFree(stride, tick, step);
                        free++;
                    }
                }
                Require(free >= away - 1 && stride.advances >= frames.Length / 2,
                    "the return plays the stride at 30 Hz with the same distance per frame");
                Require(AtCorner(min) && renderer.sprite == idles[(int)Rumo],
                    "walking straight into the corner ends on the southwest rest pose");

                for (int n = 0; n < 3; n++)
                {
                    TickRecord released = Step("corner-release", Vector2.zero, seconds);
                    TickRecord pressed = Step("corner-press", Keys[(int)Rumo], seconds);
                    Require(released.distance == 0f && pressed.distance == 0f && AtCorner(min)
                        && renderer.sprite == idles[(int)Rumo] && facing.Current == Rumo,
                        "pressing again while stopped at the limit keeps the rest pose");
                }
                Pass($"At 30 Hz the stride advanced {stride.advances} frames over {free} ticks straight into the corner, rested "
                    + "there, and pressing southwest again three times kept the rest pose with no displacement.");

                // Test setup only: leave a rounding-sized gap on one axis, then press through Move.
                actor.transform.position = new Vector3(min.x + 0.000001f, actor.transform.position.y, min.y);
                screen = ScreenPoint();
                TickRecord residue = Step("corner-residue", Keys[(int)Rumo], seconds);
                Require(residue.distance > 0f && residue.distance <= GuardianSpriteFacing.RestDistance && AtCorner(min)
                    && facing.Current == Rumo && renderer.sprite == idles[(int)Rumo],
                    "a rounding residue on one axis does not turn the guardian away from the pressed rumo");
                Pass("A rounding-sized gap to the limit closes without turning the guardian west for a frame.");
            }

            public void Restore()
            {
                actor.transform.position = spawn;
                facing.Face(Keys[startingDirection]);
                RequireCameraStill();
                Require(actor.transform.position == spawn && renderer.sprite == idles[startingDirection]
                    && renderer.enabled && camera.targetTexture == null,
                    "the scene returns to its spawn point and starting rest pose");
                Pass($"Camera never moved across {ticks} ticks and {evidence.captures.Count} captures. Spawn and starting pose restored.");
            }

            public void Save()
            {
                EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
                if (!EditorSceneManager.SaveScene(actor.gameObject.scene))
                    throw new IOException("UX-002: could not save the 2D review scene.");
                evidence.sceneSaved = true;
            }

            private TickRecord Step(string leg, Vector2 input, float seconds)
            {
                Vector3 before = actor.transform.position;
                actor.Move(input, seconds);
                Vector3 after = actor.transform.position;
                Vector2 now = ScreenPoint();
                var tick = new TickRecord
                {
                    tick = ++ticks, leg = leg, inputX = input.x, inputZ = input.y, seconds = seconds,
                    x = after.x, z = after.z, dx = after.x - before.x, dz = after.z - before.z,
                    direction = facing.Current.ToString(),
                    frame = Array.IndexOf(frames, renderer.sprite),
                    sprite = renderer.sprite != null ? renderer.sprite.name : "",
                    screenX = now.x, screenY = now.y, screenDx = now.x - screen.x, screenDy = now.y - screen.y,
                };
                tick.distance = new Vector2(tick.dx, tick.dz).magnitude;
                screen = now;
                evidence.ticks.Add(tick);

                RequireCameraStill();
                Require(after.y == before.y && renderer.transform.localPosition == artOffset
                    && actor.transform.rotation == Quaternion.identity,
                    "the actor stays on the ground plane, unrotated, with the sprite on its anchor");
                Require(after.x >= min.x && after.x <= max.x && after.z >= min.y && after.z <= max.y,
                    "the actor stays inside the plaza limits");
                Require(tick.frame >= 0
                    ? facing.Current == Rumo && (tick.distance > GuardianSpriteFacing.RestDistance || seconds <= 0f)
                    : renderer.sprite == idles[(int)facing.Current],
                    "the renderer shows a southwest walk frame only while moving southwest, otherwise the rest pose of the current rumo");
                return tick;
            }

            private static bool IsFree(TickRecord tick, float step)
            {
                return Mathf.Abs(tick.dx + step * Diagonal) < Slack && Mathf.Abs(tick.dz + step * Diagonal) < Slack;
            }

            // A full southwest tick: normalized displacement, a steady step on screen and
            // the frame that the ground covered since the last rest calls for.
            private void ObserveFree(Stride stride, TickRecord tick, float step)
            {
                var onScreen = new Vector2(tick.screenDx, tick.screenDy);
                Require(facing.Current == Rumo && tick.frame >= 0 && Mathf.Abs(tick.distance - step) < Slack,
                    "a free southwest tick covers speed times seconds on a walk frame");
                float slot = distancePerCycle / frames.Length;
                if (stride.frame < 0)
                {
                    Require(tick.frame == 0, "the stride starts on its first frame");
                    stride.screenStep = onScreen;
                    stride.metres = tick.distance;
                }
                else
                {
                    stride.metres += tick.distance;
                    Require((onScreen - stride.screenStep).magnitude < 0.02f,
                        "the anchor advances by the same step on screen every tick, across frame changes and the wrap");
                    if (tick.frame != stride.frame)
                    {
                        stride.advances++;
                        Require(tick.frame == (stride.frame + 1) % frames.Length,
                            "frames advance one at a time and never restart while the key is held");
                        Require(stride.metres >= stride.advances * slot - 2f * Slack
                            && stride.metres <= stride.advances * slot + step + 2f * Slack,
                            "each frame change lands within one tick of the calibrated distance");
                    }
                    else
                    {
                        Require(stride.metres < (stride.advances + 1) * slot + 2f * Slack,
                            "no frame outstays its calibrated distance");
                    }
                }
                stride.frame = tick.frame;
            }

            private void Capture(string label, float strideMetres)
            {
                Vector3 position = actor.transform.position;
                string stem = $"{output}/{evidence.captures.Count:00}-{label}";
                var record = new CaptureRecord
                {
                    label = label, afterTick = ticks, direction = facing.Current.ToString(),
                    frame = Array.IndexOf(frames, renderer.sprite), sprite = renderer.sprite.name,
                    x = position.x, z = position.z, strideMeters = strideMetres,
                    file720 = $"{stem}-1280x720.png", file1080 = $"{stem}-1920x1080.png",
                };
                record.at720 = Measure(Sizes[0], record.file720);
                record.at1080 = Measure(Sizes[1], record.file1080);
                Require(Mathf.Abs(record.at1080.height - 1.5f * record.at720.height) <= 3f,
                    $"{label}: the 1080p render shows the same pose at 1.5 times the 720p size");
                RequireCameraStill();
                Require(actor.transform.position == position && renderer.enabled && camera.targetTexture == null,
                    $"{label}: capturing does not move the actor");
                evidence.captures.Add(record);
            }

            // Renders the current state, then the same state without the guardian sprite, and
            // measures the difference. Pose and position are never set here.
            private Silhouette Measure(Vector2Int size, string path)
            {
                GuardianSpriteReview.CaptureFrame(camera, size, path);
                renderer.enabled = false;
                try
                {
                    GuardianSpriteReview.CaptureFrame(camera, size, PlatePath);
                }
                finally
                {
                    renderer.enabled = true;
                }

                byte[] file = File.ReadAllBytes(path);
                Color32[] with = Pixels(file, size);
                Color32[] without = Pixels(File.ReadAllBytes(PlatePath), size);
                int left = size.x, right = -1, low = size.y, high = -1;
                for (int y = 0; y < size.y; y++)
                    for (int x = 0; x < size.x; x++)
                    {
                        Color32 a = with[y * size.x + x], b = without[y * size.x + x];
                        if (Math.Abs(a.r - b.r) <= 18 && Math.Abs(a.g - b.g) <= 18 && Math.Abs(a.b - b.b) <= 18)
                            continue;
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < low) low = y;
                        high = y;
                    }
                Require(right >= 0, $"{path}: the guardian is visible");

                Vector3 anchor = camera.WorldToViewportPoint(actor.transform.position);
                float scale = size.y / (float)ReviewHeight;
                using var signature = SHA256.Create();
                var silhouette = new Silhouette
                {
                    left = left, right = right + 1, top = size.y - 1 - high, bottom = size.y - low,
                    height = high - low + 1, anchorX = anchor.x * size.x, anchorY = (1f - anchor.y) * size.y,
                    sha256 = Hex(signature.ComputeHash(file)),
                };
                silhouette.bottomMinusAnchor = silhouette.bottom - silhouette.anchorY;
                Require(Mathf.Abs(silhouette.height - ReferenceHeight * scale) <= 20f * scale,
                    $"{path}: the guardian is {silhouette.height} px tall, target {ReferenceHeight * scale:F0} px");
                Require(Mathf.Abs(silhouette.bottomMinusAnchor) <= 25f * scale
                    && silhouette.anchorX >= silhouette.left - 2f && silhouette.anchorX <= silhouette.right + 2f,
                    $"{path}: the soles sit {silhouette.bottomMinusAnchor:F1} px from the ground anchor, so the pivot is off");
                return silhouette;
            }

            private static Color32[] Pixels(byte[] png, Vector2Int size)
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try
                {
                    if (!texture.LoadImage(png) || texture.width != size.x || texture.height != size.y)
                        throw new InvalidOperationException("UX-002: unexpected capture size.");
                    return texture.GetPixels32();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            private Vector2 ScreenPoint()
            {
                Vector3 point = camera.WorldToViewportPoint(actor.transform.position);
                return new Vector2(point.x * Sizes[0].x, (1f - point.y) * Sizes[0].y);
            }

            private bool AtCorner(Vector2 corner)
            {
                return actor.transform.position.x == corner.x && actor.transform.position.z == corner.y;
            }

            private void RequireCameraStill()
            {
                Require(camera.transform.position == cameraPosition && camera.transform.rotation == cameraRotation
                    && camera.orthographic && camera.orthographicSize == cameraSize
                    && camera.worldToCameraMatrix == cameraView,
                    "the fixed camera keeps its position, rotation and size");
            }

            private void Pass(string check)
            {
                evidence.checks.Add(check);
                Debug.Log("Lumon UX-002: " + check);
            }

            private void Require(bool condition, string expectation)
            {
                if (!condition)
                    throw new InvalidOperationException($"UX-002 failed after tick {ticks}: {expectation}.");
            }
        }
    }
}
