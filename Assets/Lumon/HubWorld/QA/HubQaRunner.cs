using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    // Virtual input is active only inside a player started with -qaSteps / -qaStepsFile.
    // Normal play always uses the project's existing legacy Input Manager.
    internal static class HubQaInput
    {
        internal static bool Enabled;
        internal static Vector2 Axis;
        private static readonly HashSet<KeyCode> Held = new();
        private static readonly Dictionary<KeyCode, int> Pressed = new();

        internal static Vector2 Move => Enabled ? Axis : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        internal static bool GetKey(KeyCode key) => Enabled ? Held.Contains(key) : Input.GetKey(key);
        internal static bool GetKeyDown(KeyCode key) => Enabled ? Pressed.TryGetValue(key, out int frame) && frame == Time.frameCount : Input.GetKeyDown(key);
        internal static bool GetMouseButtonDown(int button) => !Enabled && Input.GetMouseButtonDown(button);
        internal static bool GetMouseButton(int button) => !Enabled && Input.GetMouseButton(button);
        internal static bool GetMouseButtonUp(int button) => !Enabled && Input.GetMouseButtonUp(button);
        internal static Vector2 MousePosition => Enabled ? Vector2.zero : Input.mousePosition;

        internal static void Hold(KeyCode key, bool down) { if (down) Held.Add(key); else Held.Remove(key); }
        internal static void Tap(KeyCode key) { Pressed[key] = Time.frameCount + 1; }
        internal static void Reset() { Axis = Vector2.zero; Held.Clear(); Pressed.Clear(); }
    }

    public sealed class HubQaRunner : MonoBehaviour
    {
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        private StreamWriter log;
        private StreamWriter telemetry;
        private HubSplatWorld world;
        private HubUiController ui;
        private SplatGuardian guardian;
        private SplatCameraRig cameraRig;
        private SplatArmillary armillary;
        private SplatForgeFire forgeFire;
        private string output;
        private int shotNumber;
        private int errorCount;
        private float startTime;
        private float timeout;
        private int startFrame;
        private float worstFrame;
        private float lastFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareProfile()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Arg(args, "-qaSteps") == null && Arg(args, "-qaStepsFile") == null) return;
            HubQaInput.Enabled = true;
            HubQaInput.Reset();
            HubPreferences.ActiveKeyPrefix = "Lumon.Hub.QA." + DateTime.UtcNow.Ticks.ToString(IC);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            string steps = Arg(args, "-qaSteps");
            string file = Arg(args, "-qaStepsFile");
            if (steps == null && file != null && File.Exists(file)) steps = File.ReadAllText(file);
            if (steps == null) return;
            GameObject host = new("Lumon QA — virtual input");
            DontDestroyOnLoad(host);
            HubQaRunner runner = host.AddComponent<HubQaRunner>();
            runner.output = Arg(args, "-qaOut") ?? Path.Combine(Application.persistentDataPath, "QA");
            float.TryParse(Arg(args, "-qaTimeout") ?? "180", NumberStyles.Float, IC, out runner.timeout);
            runner.StartCoroutine(runner.Run(steps));
        }

        private static string Arg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private void Awake()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += OnLog;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (lastFrame > 0) worstFrame = Mathf.Max(worstFrame, (now - lastFrame) * 1000f);
            lastFrame = now;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            HubQaInput.Reset();
            log?.Dispose();
            telemetry?.Dispose();
        }

        private void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errorCount++;
            Write(log, "QA SEEN-" + type.ToString().ToUpperInvariant() + " " + condition.Replace('\n', ' '));
        }

        private IEnumerator Run(string script)
        {
            Directory.CreateDirectory(output);
            log = new StreamWriter(Path.Combine(output, "qa.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            telemetry = new StreamWriter(Path.Combine(output, "tel.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            world = FindAnyObjectByType<HubSplatWorld>();
            ui = FindAnyObjectByType<HubUiController>();
            guardian = FindAnyObjectByType<SplatGuardian>();
            cameraRig = FindAnyObjectByType<SplatCameraRig>();
            armillary = FindAnyObjectByType<SplatArmillary>();
            forgeFire = FindAnyObjectByType<SplatForgeFire>();
            startTime = Time.realtimeSinceStartup;
            startFrame = Time.frameCount;
            Write(log, $"QA READY unity={Application.unityVersion} scene={SceneManager.GetActiveScene().name} screen={Screen.width}x{Screen.height} input=virtual-legacy out={output}");
            yield return new WaitForSecondsRealtime(1f);
            string[] steps = script.Replace('\r', '\n').Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < steps.Length; i++)
            {
                string step = steps[i].Trim();
                if (step.Length == 0 || step.StartsWith("#")) continue;
                if (Time.realtimeSinceStartup - startTime > timeout) { Write(log, "QA TIMEOUT"); break; }
                Write(log, $"QA STEP {i + 1}/{steps.Length} t={Time.realtimeSinceStartup - startTime:F2} frame={Time.frameCount} :: {step}");
                IEnumerator action = null;
                try { action = Execute(step); }
                catch (Exception ex) { Write(log, "QA ERROR " + ex.Message); }
                if (action != null) while (action.MoveNext()) yield return action.Current;
            }
            float elapsed = Time.realtimeSinceStartup - startTime;
            Write(log, $"QA PERF avg_fps={(Time.frameCount - startFrame) / Mathf.Max(0.01f, elapsed):F1} worst_frame_ms={worstFrame:F1} errors={errorCount}");
            Write(log, "QA DONE");
            yield return null;
            Application.Quit(errorCount == 0 ? 0 : 2);
        }

        private IEnumerator Execute(string step)
        {
            string[] parts = step.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string command = parts[0].ToLowerInvariant();
            switch (command)
            {
                case "wait": yield return new WaitForSecondsRealtime(Number(parts, 1, 1f)); break;
                case "key": yield return Tap(ParseKey(parts[1])); break;
                case "hold":
                {
                    KeyCode key = ParseKey(parts[1]);
                    Vector3 before = guardian.transform.position;
                    HubQaInput.Hold(key, true);
                    yield return new WaitForSecondsRealtime(Number(parts, 2, 1f));
                    HubQaInput.Hold(key, false);
                    Write(log, $"QA MOVE key={key} from={V(before)} to={V(guardian.transform.position)}");
                    Tel();
                    break;
                }
                case "room-address":
                    ui.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("partyAddress").value = parts[1];
                    break;
                case "room-count":
                case "room-charts":
                {
                    var room = world.GetComponent<HubLanSession>();
                    int expected = (int)Number(parts, 1);
                    float until = Time.realtimeSinceStartup + Number(parts,2,45);
                    int Value() => command == "room-count" ? room.Count : room.Charts;
                    while (Value() != expected && Time.realtimeSinceStartup < until) yield return null;
                    Write(log,$"QA ROOM value={Value()} expected={expected} slot={room.LocalSlot} notice={room.Notice}");
                    if (Value() != expected) errorCount++;
                    break;
                }
                case "room-state":
                {
                    var room = world.GetComponent<HubLanSession>();
                    for (int i = 0; i < HubLanSession.Capacity; i++)
                        if(room.Occupied(i)) Write(log,$"QA SLOT {i} pos={V(room.Position(i))} ready={room.Ready(i)}");
                    Write(log,$"QA ROOM local={room.LocalSlot} count={room.Count} charts={room.Charts} notice={room.Notice}");
                    break;
                }
                case "walk-to":
                {
                    Vector3 target = new(Number(parts,1),guardian.transform.position.y,Number(parts,2));
                    float until = Time.realtimeSinceStartup + Number(parts,3,10);
                    while (Time.realtimeSinceStartup < until)
                    {
                        Vector3 delta = target - guardian.transform.position;
                        if (delta.magnitude < 0.13f) break;
                        Vector3 direction = Vector3.ClampMagnitude(delta * 2f, 1f);
                        Vector3 forward = Camera.main.transform.forward; forward.y = 0; forward.Normalize();
                        Vector3 right = Camera.main.transform.right;
                        HubQaInput.Axis = new(Vector3.Dot(direction,right),Vector3.Dot(direction,forward));
                        yield return null;
                    }
                    HubQaInput.Axis = Vector2.zero;
                    bool reached = Vector3.Distance(target,guardian.transform.position) < 0.2f;
                    if (!reached) errorCount++;
                    Write(log,$"QA ARRIVAL reached={reached} target={V(target)} at={V(guardian.transform.position)} nearby={world.Nearby}");
                    break;
                }
                case "walk":
                case "stride":
                {
                    Vector3 direction = new Vector3(Number(parts, 1), 0, Number(parts, 2)).normalized;
                    Camera camera = Camera.main;
                    Vector3 forward = camera.transform.forward; forward.y = 0; forward.Normalize();
                    Vector3 right = camera.transform.right; right.y = 0; right.Normalize();
                    HubQaInput.Axis = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
                    Vector3 before = guardian.transform.position;
                    float seconds = Number(parts, 3, 1f);
                    if (command == "stride")
                    {
                        int samples = Mathf.Clamp((int)Number(parts, 4, 4), 2, 12);
                        string name = parts.Length > 5 ? parts[5] : "stride";
                        float start = Time.realtimeSinceStartup;
                        for (int i = 0; i < samples; i++)
                        {
                            float moment = start + seconds * (i + 1) / samples;
                            yield return new WaitUntil(() => Time.realtimeSinceStartup >= moment);
                            yield return Shot(name + "-" + i.ToString("D2", IC));
                        }
                    }
                    else yield return new WaitForSecondsRealtime(seconds);
                    HubQaInput.Axis = Vector2.zero;
                    Write(log, $"QA MOVE walk from={V(before)} to={V(guardian.transform.position)}");
                    Tel();
                    break;
                }
                case "button":
                {
                    string label = step.Substring(parts[0].Length).Trim();
                    bool activated = ui.QaActivate(label);
                    Write(log, $"QA CLICK text={label} activated={activated}");
                    yield return new WaitForSecondsRealtime(0.35f);
                    Tel();
                    break;
                }
                case "focus":
                {
                    string label = step.Substring(parts[0].Length).Trim();
                    bool focused = ui.QaFocus(label);
                    Write(log, $"QA FOCUS name={label} focused={focused}");
                    yield return new WaitForSecondsRealtime(0.35f);
                    break;
                }
                case "probe-move":
                {
                    Vector3 from = new(Number(parts, 1), 0.08f, Number(parts, 2));
                    Vector3 target = new(Number(parts, 3), 0.08f, Number(parts, 4));
                    Write(log, $"QA COLLISION from={V(from)} target={V(target)} actual={V(world.Constrain(from, target))}");
                    break;
                }
                case "yaw":
                    cameraRig.Orbit(Number(parts, 1, cameraRig.Yaw) - cameraRig.Yaw);
                    yield return new WaitForSecondsRealtime(0.12f);
                    break;
                case "pitch":
                    cameraRig.Orbit(0f, Number(parts, 1, cameraRig.Pitch) - cameraRig.Pitch);
                    yield return new WaitForSecondsRealtime(0.12f);
                    break;
                case "occlusion":
                    if (parts.Length != 2 || parts[1] is not ("on" or "off"))
                    {
                        Write(log, "QA WARN use occlusion on or occlusion off");
                        break;
                    }
                    guardian.SetOcclusionOutline(parts[1] == "on");
                    break;
                case "actor-light":
                    if (parts.Length != 2 || parts[1] is not ("on" or "off"))
                    {
                        Write(log, "QA WARN use actor-light on or actor-light off");
                        break;
                    }
                    guardian.SceneLightingEnabled = parts[1] == "on";
                    yield return new WaitForSecondsRealtime(0.20f);
                    break;
                case "armillary-phase":
                    if (parts.Length != 2 || armillary == null)
                    {
                        Write(log, "QA WARN use armillary-phase <0..1|auto> with an animated armillary");
                        break;
                    }
                    armillary.SetReviewPhase(parts[1] == "auto" ? null : Number(parts, 1));
                    yield return new WaitForSecondsRealtime(0.15f);
                    break;
                case "forge-phase":
                    if (parts.Length != 2 || forgeFire == null)
                    {
                        Write(log, "QA WARN use forge-phase <0..1|auto> with an animated forge");
                        break;
                    }
                    forgeFire.SetReviewPhase(parts[1] == "auto" ? null : Number(parts, 1));
                    yield return new WaitForSecondsRealtime(0.15f);
                    break;
                case "forge-glow":
                    if (parts.Length != 2 || parts[1] is not ("on" or "off") || forgeFire == null)
                    {
                        Write(log, "QA WARN use forge-glow on or forge-glow off");
                        break;
                    }
                    forgeFire.GlowEnabled = parts[1] == "on";
                    yield return new WaitForSecondsRealtime(0.15f);
                    break;
                case "audit-orbit": yield return AuditOrbit(); break;
                case "shot": yield return Shot(parts.Length > 1 ? parts[1] : "frame"); break;
                case "shots":
                {
                    int count = Mathf.Clamp((int)Number(parts, 1, 4), 1, 20);
                    float interval = Number(parts, 2, 0.1f);
                    for (int i = 0; i < count; i++) { yield return Shot(parts[3] + "-" + i); yield return new WaitForSecondsRealtime(interval); }
                    break;
                }
                case "audit": AuditHud(); break;
                case "world": AuditWorld(); break;
                case "tel": Tel(); break;
                case "quit": Application.Quit(); break;
                default: Write(log, "QA WARN unknown step=" + step); break;
            }
        }

        private IEnumerator Tap(KeyCode key)
        {
            HubQaInput.Tap(key);
            yield return new WaitForSecondsRealtime(0.2f);
            Tel();
        }

        private static KeyCode ParseKey(string key)
        {
            if (key.Equals("esc", StringComparison.OrdinalIgnoreCase)) key = "Escape";
            return (KeyCode)Enum.Parse(typeof(KeyCode), key, true);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(output, $"shot_{++shotNumber:D2}_{name}.png");
            Camera camera = Camera.main;
            string state = $"QA SHOT {path} player={V(guardian.transform.position)} yaw={cameraRig.Yaw:F1} ui={ui.QaState()} " +
                $"heading={guardian.transform.eulerAngles.y.ToString("F1", IC)} pitch={camera.transform.eulerAngles.x.ToString("F2", IC)} size={camera.orthographicSize.ToString("F3", IC)} " +
                guardian.CapturePose();
            if (armillary != null) state += " " + armillary.CaptureMotion();
            if (forgeFire != null) state += " " + forgeFire.CaptureMotion();
            if (!ui.IsModalOpen)
            {
                var (visible, hidden, covering, collisions) = ui.QaMarkerLayout();
                state += $" markers={visible}/{visible + hidden} covering={covering} collisions={collisions}";
            }
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return new WaitForEndOfFrame();
            Write(log, state);
        }

        private IEnumerator AuditOrbit()
        {
            if (ui.IsModalOpen) yield break;
            var endOfFrame = new WaitForEndOfFrame();
            int frames = 0, mostHidden = 0, hiddenFrames = 0, coveredFrames = 0, collisionFrames = 0;
            float startYaw = cameraRig.Yaw;
            float startTime = Time.realtimeSinceStartup;
            HubQaInput.Hold(KeyCode.R, true);
            while (Time.realtimeSinceStartup - startTime < 7.5f)
            {
                yield return endOfFrame;
                var (_, hidden, covering, collisions) = ui.QaMarkerLayout();
                frames++;
                mostHidden = Mathf.Max(mostHidden, hidden);
                if (hidden > 0) hiddenFrames++;
                if (covering > 0) coveredFrames++;
                if (collisions > 0) collisionFrames++;
            }
            HubQaInput.Hold(KeyCode.R, false);
            Write(log, $"QA MARKER ORBIT frames={frames} hidden_frames={hiddenFrames} max_hidden={mostHidden} covered_frames={coveredFrames} collision_frames={collisionFrames} " +
                $"start_yaw={startYaw.ToString("F1", IC)} end_yaw={cameraRig.Yaw.ToString("F1", IC)}");
        }

        private void AuditHud()
        {
            var labels = UnityEngine.Object.FindAnyObjectByType<UnityEngine.UIElements.UIDocument>().rootVisualElement.Query<UnityEngine.UIElements.Label>().ToList();
            int visible = 0;
            foreach (var label in labels)
            {
                if (label.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.None || string.IsNullOrWhiteSpace(label.text)) continue;
                visible++;
                var r = label.worldBound;
                if (r.xMin < -1 || r.yMin < -1 || r.xMax > Screen.width + 1 || r.yMax > Screen.height + 1) Write(log, $"QA HUD OFFSCREEN \"{label.text}\" {r}");
                if (label.resolvedStyle.fontSize < 12) Write(log, $"QA HUD TINY \"{label.text}\" size={label.resolvedStyle.fontSize}");
                Write(log, $"QA HUD \"{label.text}\" at {r.x:F0},{r.y:F0} {r.width:F0}x{r.height:F0} definition={label.resolvedStyle.unityFontDefinition.font?.name}");
            }
            Write(log, $"QA HUD visible_texts={visible}");
        }

        private void AuditWorld()
        {
            foreach (GaussianSplatRenderer splat in FindObjectsByType<GaussianSplatRenderer>(FindObjectsSortMode.None))
                Write(log, $"QA WORLD splat={splat.name} count={splat.splatCount} valid={splat.HasValidRenderSetup} error={splat.loadError} bounds={splat.localBounds}");
        }

        private void Tel()
        {
            string line = $"TEL t={Time.realtimeSinceStartup - startTime:F2} who=Guardian x={guardian.transform.position.x:F3} y={guardian.transform.position.y:F3} z={guardian.transform.position.z:F3} nearby={world.Nearby} reduced={world.ReducedMotion} moving={guardian.Moving} {ui.QaState()}";
            Write(telemetry, line);
            Write(log, "QA TEL " + line);
        }

        private static string V(Vector3 value) => value.ToString("F3", IC);
        private static float Number(string[] parts, int index, float fallback = 0) => parts.Length > index && float.TryParse(parts[index], NumberStyles.Float, IC, out float result) ? result : fallback;
        private static void Write(StreamWriter writer, string line) { writer?.WriteLine(line); writer?.Flush(); }
    }
}
