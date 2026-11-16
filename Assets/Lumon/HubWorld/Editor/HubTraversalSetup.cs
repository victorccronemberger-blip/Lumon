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
    // Puts the floor, the stations and the depth bands into HubWorld.unity and proves them.
    //
    //   Unity -batchmode -quit -projectPath <project> -executeMethod Lumon.HubWorld.Editor.HubTraversalSetup.InstallAndVerify
    //
    // Only the "HubWorld traversal" object is created or updated. The stage, the guardian and
    // the camera are read, walked on and put back. The scene is saved only when every check
    // passed. Art/HubWorld/review/traversal.json is written either way.
    //
    // The guardian is walked with GuardianMotion.Tick, the call her Update makes each frame,
    // with the eight inputs a keyboard can give. Before each walk she is put back on the
    // spawn saved in the scene; no check places her anywhere else.
    public static class HubTraversalSetup
    {
        private const string HubScene = "Assets/Lumon/HubWorld/HubWorld.unity";
        private const string EvidencePath = "Art/HubWorld/review/traversal.json";
        private const string RootName = "HubWorld traversal";
        private static readonly string[] UntouchedScenes =
        {
            "Assets/Lumon/GuardianReview/Guardian2DPreview.unity",
            "Assets/Lumon/GuardianReview/GuardianReview.unity",
        };

        // ------------------------------------------------------------ layout, in plate pixels

        // The compass, where the scene already puts the guardian. Checked, never applied.
        private static readonly Vector2 SpawnPlate = new Vector2(762f, 663f);
        private const float SpawnTolerance = 2f;
        // Added around the painted footprint of an obstacle, so the feet stop short of it.
        private const float FootMargin = 6f;

        private static readonly Vector2[] Floor =
        {
            new Vector2(445, 565), new Vector2(484, 544), new Vector2(511, 512), new Vector2(565, 481),
            new Vector2(606, 478), new Vector2(637, 455), new Vector2(719, 455), new Vector2(745, 474),
            new Vector2(826, 445), new Vector2(884, 431), new Vector2(952, 430), new Vector2(992, 451),
            new Vector2(1028, 478), new Vector2(1046, 501), new Vector2(1031, 540), new Vector2(1083, 574),
            new Vector2(1094, 605), new Vector2(1021, 641), new Vector2(987, 698), new Vector2(969, 784),
            new Vector2(946, 864), new Vector2(524, 868), new Vector2(503, 808), new Vector2(502, 739),
            new Vector2(427, 678), new Vector2(414, 639),
        };

        private static HubGround.Outline[] Obstacles()
        {
            return new[]
            {
                // Ring of stones of the campfire.
                Ellipse("Campfire", new Vector2(931, 447), new Vector2(46, 13)),
                // Satchel and front legs of the expedition desk, margin already in the outline.
                new HubGround.Outline
                {
                    name = "Expedition desk",
                    points = new[]
                    {
                        new Vector2(1002, 530), new Vector2(1012, 497), new Vector2(1030, 472), new Vector2(1060, 462),
                        new Vector2(1210, 462), new Vector2(1210, 585), new Vector2(1090, 585), new Vector2(1042, 570),
                        new Vector2(1006, 555),
                    },
                },
            };
        }

        private static HubStationSpot[] Stations()
        {
            return new[]
            {
                new HubStationSpot
                {
                    station = HubStation.Team, anchor = new Vector2(390, 585), pointerArea = new Rect(300, 470, 130, 175),
                    approach = new Vector2(452, 612), reach = 60f, hudShortcut = true,
                },
                new HubStationSpot
                {
                    station = HubStation.Expeditions, anchor = new Vector2(1160, 548), pointerArea = new Rect(1012, 388, 290, 185),
                    approach = new Vector2(1058, 598), reach = 60f, hudShortcut = true,
                },
                // The Observatory door.
                new HubStationSpot
                {
                    station = HubStation.Settings, anchor = new Vector2(673, 443), pointerArea = new Rect(640, 345, 68, 112),
                    approach = new Vector2(678, 468), reach = 50f, hudShortcut = true,
                },
            };
        }

        private const int FrontOffset = 100;
        private const string ProbedOccluder = "Campfire";
        // Floor beside the stones, above their baseline, and floor below them.
        private static readonly Vector2 BehindProbe = new Vector2(868, 441);
        private static readonly Vector2 FrontProbe = new Vector2(930, 492);

        // Layers are named, not counted. Rename here if the art manifest renames them.
        private static HubOccluder[] Occluders()
        {
            return new[]
            {
                new HubOccluder { name = ProbedOccluder, layers = new[] { "CampfireHearth", "CampfireFlame", "CampfireGlow" }, baseline = 452f },
                new HubOccluder { name = "Plaza tree", layers = new[] { "PlazaTree" }, baseline = 420f },
            };
        }

        // ------------------------------------------------------------ evidence

        [Serializable]
        private sealed class Evidence
        {
            public string method = "Lumon.HubWorld.Editor.HubTraversalSetup.InstallAndVerify";
            public bool passed;
            public string failure = "";
            public string unity;
            public string generatedUtc;
            public string scene = HubScene;
            public bool sceneSaved;
            public string toolObject = RootName;
            public bool toolObjectCreated;
            public string coordinates = "Plate pixels from the top-left corner of the painting. Feet are the guardian's transform.";
            public string walking = "GuardianMotion.Tick with one of eight keyboard directions per frame. "
                + "Every walk starts on the spawn saved in the scene.";
            public int plateWidth;
            public int plateHeight;
            public Vector3 spawnWorld;
            public Vector2 spawnPlate;
            public Vector2 spawnPlateExpected;
            public float guardianSpeed;
            public float platePixelsPerSecondSideways;
            public float platePixelsPerSecondInDepth;
            public Vector3 cameraPosition;
            public Vector3 cameraEuler;
            public float cameraOrthographicSize;
            public List<string> cameraComponents = new List<string>();
            public float footMargin = FootMargin;
            public Vector2[] floor;
            public List<OutlineRecord> obstacles = new List<OutlineRecord>();
            public List<StationRecord> stations = new List<StationRecord>();
            public int frontOffset = FrontOffset;
            public int guardianLowestOrder;
            public int guardianHighestOrder;
            public List<OccluderRecord> occluders = new List<OccluderRecord>();
            public List<HoldRecord> holds = new List<HoldRecord>();
            public List<BlockRecord> blocks = new List<BlockRecord>();
            public List<CheckRecord> checks = new List<CheckRecord>();
            public List<string> notCovered = new List<string>
            {
                "Keyboard and mouse reading (Input.GetKey, GetKeyDown, GetMouseButtonDown) and the screen-to-plate "
                    + "conversion of a click need Play Mode. The calls they lead to are the ones exercised here.",
                "The plaza tree stands behind the whole floor, so no walk can put it in front. Its band is checked by rule.",
            };
        }

        [Serializable]
        private sealed class OutlineRecord
        {
            public string name;
            public Vector2[] points;
            public int pointsInsideFloor;
        }

        [Serializable]
        private sealed class StationRecord
        {
            public string station;
            public Vector2 anchor;
            public Rect pointerArea;
            public Vector2 approach;
            public float reach;
            public bool hudShortcut;
            public bool reached;
            public int frames;
            public float seconds;
            public Vector2 arrivedAt;
            public List<Vector2> path = new List<Vector2>();
        }

        [Serializable]
        private sealed class OccluderRecord
        {
            public string name;
            public float baseline;
            public string[] layers;
            public List<string> sprites = new List<string>();
            public List<int> paintedOrders = new List<int>();
        }

        [Serializable]
        private sealed class HoldRecord
        {
            public string direction;
            public float secondsPerStep;
            public int steps;
            public Vector2 endedAt;
            public bool stayedOnFloor;
        }

        [Serializable]
        private sealed class BlockRecord
        {
            public string obstacle;
            public Vector2 from;
            public Vector2 unobstructed;
            public Vector2 endedAt;
            public float secondsPerStep;
        }

        [Serializable]
        private sealed class CheckRecord
        {
            public string name;
            public bool passed;
            public string detail;
        }

        private struct Look
        {
            public Sprite sprite;
            public int order;
            public bool enabled;
        }

        private sealed class Rig
        {
            public Evidence evidence;
            public HubGround ground;
            public HubStations stations;
            public HubDepthSort sort;
            public GuardianMotion guardian;
            public Transform feet;
            public Vector3 spawn;
            public bool leftFloor;
        }

        private static readonly Vector2[] Directions =
        {
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(1, -1),
            new Vector2(0, -1), new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1),
        };
        private static readonly string[] DirectionNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        private const float Frame = 1f / 60f;

        // ------------------------------------------------------------ entry

        [MenuItem("Lumon/Install Hub Traversal And Verify")]
        public static void InstallAndVerify()
        {
            var evidence = new Evidence
            {
                unity = Application.unityVersion,
                generatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                spawnPlateExpected = SpawnPlate,
                floor = Floor,
            };
            CultureInfo culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Action putBack = null;
            try
            {
                var hashes = new string[UntouchedScenes.Length];
                for (int i = 0; i < hashes.Length; i++)
                    hashes[i] = File.Exists(UntouchedScenes[i]) ? HashFile(UntouchedScenes[i]) : "";

                Require(File.Exists(HubScene), HubScene + " exists. Build it first with HubWorldBuilder.BuildAndCapture");
                Scene scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
                Require(scene.path == HubScene, "the scene being edited is the Hub scene");

                List<HubStage> stages = All<HubStage>(scene);
                List<GuardianMotion> guardians = All<GuardianMotion>(scene);
                Camera camera = null;
                foreach (Camera candidate in All<Camera>(scene))
                    if (camera == null || candidate.CompareTag("MainCamera"))
                        camera = candidate;
                Require(stages.Count == 1 && stages[0].BaseRenderer != null, "the Hub scene has the one stage made by HubWorldBuilder");
                Require(guardians.Count == 1, "the Hub scene has exactly one GuardianMotion");
                Require(camera != null, "the Hub scene has its camera");

                var rig = new Rig { evidence = evidence, guardian = guardians[0], feet = guardians[0].transform };
                rig.spawn = rig.feet.position;
                Quaternion facing = rig.feet.rotation;
                Vector3 cameraPosition = camera.transform.position;
                Quaternion cameraRotation = camera.transform.rotation;
                float cameraSize = camera.orthographicSize;
                var looks = new Dictionary<SpriteRenderer, Look>();
                foreach (SpriteRenderer sprite in All<SpriteRenderer>(scene))
                    looks[sprite] = new Look { sprite = sprite.sprite, order = sprite.sortingOrder, enabled = sprite.enabled };

                Install(scene, stages[0], camera, rig);
                putBack = () =>
                {
                    rig.sort.Restore();
                    rig.ground.Release();
                    rig.feet.SetPositionAndRotation(rig.spawn, facing);
                    foreach (KeyValuePair<SpriteRenderer, Look> look in looks)
                        if (look.Key != null)
                            look.Key.sprite = look.Value.sprite;
                };

                Require(rig.ground.Bind(), "the floor binds to the stage and the guardian");
                Describe(rig, camera);
                CheckCameraAndSpawn(rig, camera, stages[0]);
                CheckStations(rig);
                CheckLimits(rig);
                CheckObstacles(rig);
                CheckDepth(rig);
                Check(evidence, "Feet never left the floor", !rig.leftFloor,
                    "After every frame of every walk above, the feet were inside the floor outline and outside every obstacle.");

                putBack();
                putBack = null;
                bool cameraStill = camera.transform.position == cameraPosition && camera.transform.rotation == cameraRotation
                    && camera.orthographicSize == cameraSize && !camera.transform.IsChildOf(rig.feet);
                Check(evidence, "Camera is fixed", cameraStill,
                    "Position, rotation and size are the same after all the walking, and the camera is not carried by the guardian.");
                bool untouched = rig.feet.position == rig.spawn && rig.feet.rotation == facing;
                foreach (KeyValuePair<SpriteRenderer, Look> look in looks)
                    untouched &= look.Key != null && look.Key.sprite == look.Value.sprite
                        && look.Key.sortingOrder == look.Value.order && look.Key.enabled == look.Value.enabled;
                Check(evidence, "Scene left as found", untouched && All<SpriteRenderer>(scene).Count == looks.Count,
                    "The guardian is back on the spawn, and every sprite keeps its image, order and state.");

                CheckRecord failed = evidence.checks.Find(check => !check.passed);
                Require(failed == null, failed != null ? failed.name + " (" + failed.detail + ")" : "");

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("HubTraversal: could not save " + HubScene + ".");
                evidence.sceneSaved = true;
                CheckSaved(evidence, rig.spawn);
                for (int i = 0; i < hashes.Length; i++)
                    Require(!File.Exists(UntouchedScenes[i]) || HashFile(UntouchedScenes[i]) == hashes[i],
                        UntouchedScenes[i] + " is byte-identical after the install");
                Check(evidence, "Only the Hub scene changed", true, string.Join(", ", UntouchedScenes) + " are byte-identical.");

                evidence.passed = true;
                Debug.Log($"Lumon HubTraversal: {evidence.checks.Count} checks passed. Scene saved.");
            }
            catch (Exception failure)
            {
                evidence.failure = failure.Message;
                throw;
            }
            finally
            {
                try
                {
                    putBack?.Invoke();
                }
                finally
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(EvidencePath));
                    File.WriteAllText(EvidencePath, JsonUtility.ToJson(evidence, true) + "\n");
                    CultureInfo.CurrentCulture = culture;
                }
            }
        }

        // ------------------------------------------------------------ install

        private static void Install(Scene scene, HubStage stage, Camera camera, Rig rig)
        {
            List<HubGround> grounds = All<HubGround>(scene);
            List<HubStations> stations = All<HubStations>(scene);
            List<HubDepthSort> sorts = All<HubDepthSort>(scene);
            Require(grounds.Count <= 1 && stations.Count <= 1 && sorts.Count <= 1, "the scene holds at most one of each traversal component");

            GameObject root = grounds.Count == 1 ? grounds[0].gameObject
                : stations.Count == 1 ? stations[0].gameObject
                : sorts.Count == 1 ? sorts[0].gameObject : null;
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
                rig.evidence.toolObjectCreated = true;
            }
            Require(root.scene == scene && root.GetComponent<HubStage>() == null && root.GetComponent<GuardianMotion>() == null
                && root.GetComponent<Camera>() == null, "the traversal components sit on an object of their own");
            Require(stations.Count == 0 || stations[0].gameObject == root, "HubStations sits with HubGround");
            Require(sorts.Count == 0 || sorts[0].gameObject == root, "HubDepthSort sits with HubGround");
            root.SetActive(true);

            rig.ground = grounds.Count == 1 ? grounds[0] : root.AddComponent<HubGround>();
            rig.stations = stations.Count == 1 ? stations[0] : root.AddComponent<HubStations>();
            rig.sort = sorts.Count == 1 ? sorts[0] : root.AddComponent<HubDepthSort>();
            rig.ground.enabled = rig.stations.enabled = rig.sort.enabled = true;

            rig.ground.Configure(stage, rig.guardian, (Vector2[])Floor.Clone(), Obstacles());
            rig.stations.Configure(rig.ground, camera, Stations());
            rig.sort.Configure(rig.ground, FrontOffset, Occluders());
            EditorUtility.SetDirty(rig.ground);
            EditorUtility.SetDirty(rig.stations);
            EditorUtility.SetDirty(rig.sort);
        }

        private static void Describe(Rig rig, Camera camera)
        {
            Evidence evidence = rig.evidence;
            HubGround ground = rig.ground;
            evidence.plateWidth = ground.Stage.PlateSize.x;
            evidence.plateHeight = ground.Stage.PlateSize.y;
            evidence.spawnWorld = rig.spawn;
            evidence.spawnPlate = ground.WorldToPlate(rig.spawn);
            evidence.guardianSpeed = rig.guardian.Speed;
            evidence.platePixelsPerSecondSideways =
                (ground.WorldToPlate(rig.spawn + Vector3.right * rig.guardian.Speed) - evidence.spawnPlate).magnitude;
            evidence.platePixelsPerSecondInDepth =
                (ground.WorldToPlate(rig.spawn + Vector3.forward * rig.guardian.Speed) - evidence.spawnPlate).magnitude;
            evidence.cameraPosition = camera.transform.position;
            evidence.cameraEuler = camera.transform.rotation.eulerAngles;
            evidence.cameraOrthographicSize = camera.orthographicSize;
            foreach (Component part in camera.GetComponents<Component>())
                evidence.cameraComponents.Add(part != null ? part.GetType().Name : "missing script");
            foreach (HubGround.Outline obstacle in ground.Obstacles)
            {
                int inside = 0;
                foreach (Vector2 point in obstacle.points)
                    if (Inside(Floor, point))
                        inside++;
                evidence.obstacles.Add(new OutlineRecord { name = obstacle.name, points = obstacle.points, pointsInsideFloor = inside });
            }
        }

        // ------------------------------------------------------------ checks

        private static void CheckCameraAndSpawn(Rig rig, Camera camera, HubStage stage)
        {
            Evidence evidence = rig.evidence;
            float tilt = Quaternion.Angle(camera.transform.rotation, stage.transform.rotation);
            Check(evidence, "Plate faces the fixed orthographic camera", camera.orthographic && tilt < 0.01f,
                $"Orthographic: {camera.orthographic}. Angle between camera and stage: {tilt:F4} degrees.");

            float off = (evidence.spawnPlate - SpawnPlate).magnitude;
            Check(evidence, "Spawn is the one already in the scene, on the compass", off <= SpawnTolerance,
                $"The saved guardian position reads as plate ({evidence.spawnPlate.x:F1}, {evidence.spawnPlate.y:F1}), "
                + $"{off:F2} px from ({SpawnPlate.x:F0}, {SpawnPlate.y:F0}). It was not moved.");
            Check(evidence, "Spawn is on the floor", rig.ground.IsWalkableWorld(rig.spawn), "Inside the outline, outside every obstacle.");

            bool bite = evidence.obstacles.Count > 0;
            foreach (OutlineRecord obstacle in evidence.obstacles)
                bite &= obstacle.pointsInsideFloor > 0;
            Check(evidence, "Every obstacle reaches into the floor", bite, "Each obstacle outline has at least one point inside the floor outline.");
        }

        private static void CheckStations(Rig rig)
        {
            Evidence evidence = rig.evidence;
            HubStations stations = rig.stations;
            var heard = new List<HubStation>();
            Action<HubStation> listener = heard.Add;
            var modal = new object();
            stations.OnStationRequested += listener;
            try
            {
                int reached = 0;
                foreach (HubStationSpot spot in stations.Spots)
                {
                    var record = new StationRecord
                    {
                        station = spot.station.ToString(), anchor = spot.anchor, pointerArea = spot.pointerArea,
                        approach = spot.approach, reach = spot.reach, hudShortcut = spot.hudShortcut,
                    };
                    evidence.stations.Add(record);
                    string name = record.station;

                    // Far away: on the spawn.
                    ToSpawn(rig);
                    stations.Refresh();
                    heard.Clear();
                    bool refusedFar = stations.NearbyStation != spot.station && !stations.CanRequest(spot.station, HubRequestSource.Key)
                        && !stations.RequestStation(spot.station, HubRequestSource.Key) && !stations.Interact()
                        && !stations.PointerAt(spot.pointerArea.center) && heard.Count == 0;
                    Check(evidence, name + ": refused from the spawn", refusedFar,
                        "F and a click on the station announce nothing while the guardian is out of reach.");

                    bool hud = stations.RequestStation(spot.station, HubRequestSource.Hud);
                    Check(evidence, name + ": HUD request from the spawn", hud == spot.hudShortcut
                        && heard.Count == (hud ? 1 : 0) && (!hud || (heard[0] == spot.station && stations.LastRequestSource == HubRequestSource.Hud)),
                        $"hudShortcut is {spot.hudShortcut}; the HUD request was {(hud ? "announced" : "refused")} without walking.");

                    // Near: walked there.
                    record.reached = Walk(rig, spot.approach, spot.reach * 0.8f, Frame, 1500, record.path, out record.frames);
                    record.seconds = record.frames * Frame;
                    record.arrivedAt = rig.ground.FeetPlate;
                    stations.Refresh();
                    bool near = record.reached && stations.NearbyStation == spot.station;
                    Check(evidence, name + ": reached on foot from the spawn", near,
                        $"{record.frames} frames ({record.seconds:F2} s) to plate ({record.arrivedAt.x:F0}, {record.arrivedAt.y:F0}); "
                        + $"NearbyStation is {stations.NearbyStation}.");
                    if (!near)
                        continue;
                    reached++;

                    heard.Clear();
                    bool accepted = stations.Interact() && stations.LastRequestSource == HubRequestSource.Key
                        && stations.PointerAt(spot.pointerArea.center) && stations.LastRequestSource == HubRequestSource.Pointer
                        && heard.Count == 2 && heard[0] == spot.station && heard[1] == spot.station;
                    Check(evidence, name + ": F and click accepted beside it", accepted, "Each announced the station once.");

                    // A modal is up: the world ignores input, the HUD still answers, the guardian stays put.
                    heard.Clear();
                    stations.SetWorldInputPaused(modal, true);
                    Vector3 held = rig.feet.position;
                    for (int i = 0; i < 30; i++)
                        rig.guardian.Tick(Directions[(i / 4) % Directions.Length], Frame);
                    bool paused = stations.WorldInputPaused && rig.feet.position == held
                        && !stations.Interact() && !stations.PointerAt(spot.pointerArea.center) && heard.Count == 0
                        && stations.RequestStation(spot.station, HubRequestSource.Hud) == spot.hudShortcut;
                    stations.Refresh();
                    paused &= stations.NearbyStation == spot.station;
                    stations.SetWorldInputPaused(modal, false);
                    heard.Clear();
                    rig.guardian.Tick(Directions[4], Frame);
                    bool resumed = !stations.WorldInputPaused && rig.feet.position != held;
                    Check(evidence, name + ": world input pauses and resumes", paused && resumed,
                        "Paused: 30 frames of held keys move nothing, F and click are ignored, the nearby station is still reported. "
                        + "Resumed: the next frame moves her.");
                }
                Check(evidence, "At least three stations reachable from the spawn", reached >= 3, $"{reached} of {stations.Spots.Count}.");
            }
            finally
            {
                stations.OnStationRequested -= listener;
                stations.SetWorldInputPaused(modal, false);
            }
        }

        // Each direction held for six seconds: frame by frame, in long frames, and in one single step.
        private static void CheckLimits(Rig rig)
        {
            Evidence evidence = rig.evidence;
            HubGround ground = rig.ground;
            bool held = true, agree = true, facesKey = true;
            float slide = 0f;
            var facing = rig.guardian.GetComponentInChildren<GuardianSpriteFacing>(true);
            var first = new Vector2[Directions.Length];
            foreach (float seconds in new[] { Frame, 0.25f, 0.5f, 6f })
            {
                int steps = Mathf.Max(1, Mathf.RoundToInt(6f / seconds));
                for (int d = 0; d < Directions.Length; d++)
                {
                    ToSpawn(rig);
                    bool onFloor = true;
                    for (int i = 0; i < steps; i++)
                    {
                        rig.guardian.Tick(Directions[d], seconds);
                        onFloor &= ground.IsWalkableWorld(rig.feet.position);
                    }
                    Vector2 end = ground.FeetPlate;
                    evidence.holds.Add(new HoldRecord
                    {
                        direction = DirectionNames[d], secondsPerStep = seconds, steps = steps, endedAt = end, stayedOnFloor = onFloor,
                    });
                    // Six seconds of free walking would end far outside the painting.
                    held &= onFloor && (end - evidence.spawnPlate).magnitude > 20f;
                    if (seconds == Frame)
                    {
                        first[d] = end;
                        // South meets the lower edge almost head-on and only creeps along it.
                        if (d == 4 && facing != null)
                            facesKey = facing.Current == GuardianSpriteFacing.Direction.South;
                        if (d == 0)
                            slide = Mathf.Abs(end.x - evidence.spawnPlate.x);
                    }
                    else
                        agree &= (end - first[d]).magnitude < 12f;
                }
            }
            Check(evidence, "The outline holds in all eight directions", held,
                "Six seconds per direction at 1/60, 0.25, 0.5 and 6 seconds per step. She left the spawn and never left the floor.");
            Check(evidence, "Long steps end where short steps end", agree,
                "For each direction the four runs stop within 12 px of each other, so a fast frame neither jumps an edge nor sticks early.");
            Check(evidence, "Pressed against an edge she keeps facing the key", facesKey,
                "After six seconds of south against the lower edge the guardian still faces south.");
            Check(evidence, "She slides along an edge", slide > 40f,
                $"Held north against the slanted upper edge she drifted {slide:F0} px sideways along it.");
        }

        // From the floor in front of each obstacle, one long frame straight into it.
        private static void CheckObstacles(Rig rig)
        {
            Evidence evidence = rig.evidence;
            HubGround ground = rig.ground;
            var starts = new[] { FrontProbe, Stations()[1].approach };
            for (int o = 0; o < ground.Obstacles.Count && o < starts.Length; o++)
            {
                HubGround.Outline obstacle = ground.Obstacles[o];
                foreach (float seconds in new[] { 0.25f, 0.5f })
                {
                    ToSpawn(rig);
                    bool there = Walk(rig, starts[o], 8f, Frame, 1500, null, out _);
                    Vector2 from = ground.FeetPlate;
                    // Walking in depth covers the rows she would cross with nothing in the way.
                    Vector2 free = ground.WorldToPlate(rig.feet.position + Vector3.forward * (rig.guardian.Speed * seconds));
                    if (seconds > 0.25f)
                        free = from + (free - from) * (0.25f / seconds);
                    rig.guardian.Tick(Directions[0], seconds);
                    Vector2 end = ground.FeetPlate;
                    evidence.blocks.Add(new BlockRecord
                    {
                        obstacle = obstacle.name, from = from, unobstructed = free, endedAt = end, secondsPerStep = seconds,
                    });
                    Check(evidence, $"{obstacle.name} blocks a {seconds:F2} s step", there && Inside(obstacle.points, free)
                        && !Inside(obstacle.points, end) && ground.IsWalkableWorld(rig.feet.position),
                        $"From ({from.x:F0}, {from.y:F0}) the step north passes through ({free.x:F0}, {free.y:F0}), inside the obstacle. "
                        + $"She ended at ({end.x:F0}, {end.y:F0}), outside it and on the floor.");
                }
            }
        }

        private static void CheckDepth(Rig rig)
        {
            Evidence evidence = rig.evidence;
            HubDepthSort sort = rig.sort;
            sort.Bind();

            int lowest = int.MaxValue, highest = int.MinValue;
            foreach (SpriteRenderer sprite in rig.guardian.GetComponentsInChildren<SpriteRenderer>(true))
            {
                lowest = Mathf.Min(lowest, sprite.sortingOrder);
                highest = Mathf.Max(highest, sprite.sortingOrder);
            }
            evidence.guardianLowestOrder = lowest;
            evidence.guardianHighestOrder = highest;

            float topRow = float.MaxValue;
            foreach (Vector2 point in Floor)
                topRow = Mathf.Min(topRow, point.y);

            bool found = lowest <= highest, bands = true, rule = true;
            HubOccluder probed = null;
            var sprites = new List<SpriteRenderer>();
            var painted = new List<int>();
            foreach (HubOccluder occluder in sort.Occluders)
            {
                sprites.Clear();
                painted.Clear();
                var record = new OccluderRecord { name = occluder.name, baseline = occluder.baseline, layers = occluder.layers };
                found &= sort.Resolved(occluder, sprites, painted) > 0;
                for (int i = 0; i < sprites.Count; i++)
                {
                    record.sprites.Add(sprites[i].name);
                    record.paintedOrders.Add(painted[i]);
                    bands &= painted[i] < lowest && painted[i] + sort.FrontOffset > highest;
                }
                evidence.occluders.Add(record);
                rule &= sort.InFront(occluder.baseline, occluder.baseline - 10f, false)
                    && !sort.InFront(occluder.baseline, occluder.baseline + 10f, true);
                if (occluder.name == ProbedOccluder)
                    probed = occluder;
                else
                    // Stated, not walked: this one never comes in front on the present floor.
                    rule &= !sort.InFront(occluder.baseline, topRow, false);
            }
            Check(evidence, "Every occluder finds its sprites by name", found,
                "Campfire and plaza tree each resolve at least one sprite under the stage. A failure here means a layer was renamed.");
            Check(evidence, "Three stable bands", found && bands,
                $"Painted orders are below the guardian ({lowest} to {highest}); painted + {sort.FrontOffset} is above her.");
            Check(evidence, "Baseline rule", rule,
                $"Feet above a baseline put the occluder in front, feet below put it behind. The plaza tree's baseline is above "
                + $"the top row of the floor ({topRow:F0}), so it stays in the painted band wherever she stands.");
            if (probed == null || !found)
                return;

            ToSpawn(rig);
            bool behind = Walk(rig, BehindProbe, 6f, Frame, 1500, null, out _);
            sort.Apply();
            Vector2 at = rig.ground.FeetPlate;
            bool front = behind && Orders(sort, probed, sort.FrontOffset) && Others(sort, probed);
            Check(evidence, "Campfire covers her when she stands behind it", front,
                $"Walked to ({at.x:F0}, {at.y:F0}), above the baseline at {probed.baseline:F0}: the campfire sprites are at painted + {sort.FrontOffset}.");

            bool before = Walk(rig, FrontProbe, 8f, Frame, 1500, null, out _);
            sort.Apply();
            at = rig.ground.FeetPlate;
            Check(evidence, "She covers the campfire when she stands before it", before && Orders(sort, probed, 0) && Others(sort, probed),
                $"Walked on to ({at.x:F0}, {at.y:F0}), below the baseline: the campfire sprites are back at their painted orders.");
            sort.Restore();
        }

        private static bool Orders(HubDepthSort sort, HubOccluder occluder, int offset)
        {
            var sprites = new List<SpriteRenderer>();
            var painted = new List<int>();
            sort.Resolved(occluder, sprites, painted);
            for (int i = 0; i < sprites.Count; i++)
                if (sprites[i].sortingOrder != painted[i] + offset)
                    return false;
            return sprites.Count > 0;
        }

        private static bool Others(HubDepthSort sort, HubOccluder except)
        {
            foreach (HubOccluder occluder in sort.Occluders)
                if (occluder != except && !Orders(sort, occluder, 0))
                    return false;
            return true;
        }

        private static void CheckSaved(Evidence evidence, Vector3 spawn)
        {
            Scene scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
            List<HubGround> grounds = All<HubGround>(scene);
            List<HubStations> stations = All<HubStations>(scene);
            List<HubDepthSort> sorts = All<HubDepthSort>(scene);
            List<GuardianMotion> guardians = All<GuardianMotion>(scene);
            bool stored = grounds.Count == 1 && stations.Count == 1 && sorts.Count == 1 && guardians.Count == 1
                && stations[0].gameObject == grounds[0].gameObject && sorts[0].gameObject == grounds[0].gameObject
                && grounds[0].enabled && stations[0].enabled && sorts[0].enabled && grounds[0].gameObject.activeInHierarchy
                && grounds[0].Stage != null && grounds[0].Guardian == guardians[0]
                && grounds[0].Walkable.Count == Floor.Length && grounds[0].Obstacles.Count == Obstacles().Length
                && stations[0].Ground == grounds[0] && stations[0].Spots.Count == Stations().Length
                && sorts[0].Occluders.Count == Occluders().Length
                && guardians[0].transform.position == spawn && All<HubStage>(scene).Count == 1;
            Require(stored, "the saved Hub scene reloads with one floor, one set of stations and one depth sorter, wired, and the spawn in place");
            Check(evidence, "Saved scene reloads complete", true,
                "One HubGround, HubStations and HubDepthSort on '" + grounds[0].name + "', wired to the stage and the guardian. Spawn unchanged.");
        }

        // ------------------------------------------------------------ walking

        private static void ToSpawn(Rig rig)
        {
            rig.feet.position = rig.spawn;
        }

        // Steers towards a point of the plate with keyboard directions until the feet are within
        // the radius. Greedy on purpose: a route that needs more than that is reported as not reached.
        private static bool Walk(Rig rig, Vector2 target, float radius, float seconds, int budget, List<Vector2> path, out int frames)
        {
            Vector3 goal = rig.ground.PlateToGround(target);
            for (frames = 0; frames < budget; frames++)
            {
                Vector2 feet = rig.ground.FeetPlate;
                if (path != null && frames % 6 == 0)
                    path.Add(feet);
                if ((feet - target).magnitude <= radius)
                    break;
                Vector3 to = goal - rig.feet.position;
                int octant = (Mathf.RoundToInt(Mathf.Atan2(to.x, to.z) / (Mathf.PI / 4f)) + 8) % 8;
                rig.guardian.Tick(Directions[octant], seconds);
                rig.leftFloor |= !rig.ground.IsWalkableWorld(rig.feet.position);
            }
            Vector2 end = rig.ground.FeetPlate;
            path?.Add(end);
            return (end - target).magnitude <= radius;
        }

        // ------------------------------------------------------------ helpers

        private static HubGround.Outline Ellipse(string name, Vector2 centre, Vector2 radii)
        {
            var points = new Vector2[12];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * 2f * Mathf.PI / points.Length;
                points[i] = centre + new Vector2((radii.x + FootMargin) * Mathf.Cos(angle), (radii.y + FootMargin) * Mathf.Sin(angle));
            }
            return new HubGround.Outline { name = name, points = points };
        }

        private static bool Inside(IReadOnlyList<Vector2> loop, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
            {
                Vector2 a = loop[i], b = loop[j];
                if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static List<T> All<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<T>(true));
            return found;
        }

        private static string HashFile(string path)
        {
            using (SHA256 signature = SHA256.Create())
                return BitConverter.ToString(signature.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private static void Check(Evidence evidence, string name, bool passed, string detail)
        {
            evidence.checks.Add(new CheckRecord { name = name, passed = passed, detail = detail });
            if (passed)
                Debug.Log("Lumon HubTraversal: " + name + ". " + detail);
            else
                Debug.LogWarning("Lumon HubTraversal FAILED: " + name + ". " + detail);
        }

        private static void Require(bool condition, string expectation)
        {
            if (!condition)
                throw new InvalidOperationException("HubTraversal failed: " + expectation + ".");
        }
    }
}
