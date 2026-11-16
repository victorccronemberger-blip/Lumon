using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    // Opt-in review instrumentation. A normal launch never creates this component.
    internal sealed class HubPlayerReview : MonoBehaviour
    {
        private const string TestKey = "Lumon.Hub.Review.v1";
        private const string WorkshopKey = TestKey + ".Workshop";
        private bool workshopReview;
        private string output;
        private bool readOnly;
        private bool wideReview;
        private bool inputReview;
        private bool designReview;
        private bool motionReview;
        private readonly System.Collections.Generic.List<string> designIssues = new();
        private VisualElement root;
        private HubGround ground;
        private HubStations stations;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartReview()
        {
            string[] args = Environment.GetCommandLineArgs();
            int next = Array.IndexOf(args, "-lumon-hub-review-write");
            bool reading = next < 0;
            if (reading) next = Array.IndexOf(args, "-lumon-hub-review-reload");
            if (next < 0 || next + 1 >= args.Length) return;
            bool workshop = args.Contains("-lumon-hub-review-workshop");
            string profile = workshop ? WorkshopKey : TestKey;
            HubUiController.PreferencesKey = profile;
            if (!reading)
            {
                PlayerPrefs.DeleteKey(HubRosterState.StorageKey(profile));
                new HubPreferences().Save(profile);
            }
            var review = new GameObject("Hub Player Review").AddComponent<HubPlayerReview>();
            review.output = Path.GetFullPath(args[next + 1]);
            review.readOnly = reading;
            review.wideReview = args.Contains("-lumon-hub-review-wide");
            review.workshopReview = workshop;
            review.inputReview = args.Contains("-lumon-hub-review-input");
            review.designReview = args.Contains("-lumon-hub-review-design");
            review.motionReview = args.Contains("-lumon-hub-review-motion");
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            yield return null;
            yield return null;
            root = FindFirstObjectByType<UIDocument>().rootVisualElement;
            ground = FindFirstObjectByType<HubGround>();
            stations = FindFirstObjectByType<HubStations>();
            IEnumerator run = workshopReview ? (readOnly ? VerifyWorkshopReload() : VerifyWorkshop()) : designReview ? VerifyDesign() : motionReview ? CaptureMotion() : inputReview ? VerifyInputs() : readOnly ? VerifyReload() : VerifyPlay();
            while (true)
            {
                object instruction;
                try
                {
                    if (!run.MoveNext()) break;
                    instruction = run.Current;
                }
                catch (Exception error)
                {
                    File.WriteAllText(ReportPath(), "FAILED\n" + error);
                    Debug.LogException(error);
                    Application.Quit(1);
                    yield break;
                }
                yield return instruction;
            }
            File.WriteAllText(ReportPath(), workshopReview ? "PASS: workshop purchase, incubation, hatch, party, fusion, upgrade and persistence." : designReview ? "PASS: localized plaza, atlas, settings and four workshop tabs fit the UI containers." : motionReview ? "PASS: captured the ambient scene at 10 frames per second and in reduced motion." : inputReview ? "PASS: Windows movement, settings, atlas, E at the team stall, egg purchase and hatch, tab navigation, and focus return." : readOnly
                ? $"PASS: standalone reload restored locale, team, route, volume and reduced motion at {Screen.width} x {Screen.height}."
                : "PASS: standalone plaza movement, collision, depth, station actions, three locales, UI actions and ambient sources.");
            Application.Quit(0);
        }

        private IEnumerator VerifyPlay()
        {
            Require(ground.Ready && ground.IsWalkablePlate(ground.FeetPlate), "Playable spawn");
            Require(Text("location") == "Observatório da Aurora", "Portuguese initial title");
            yield return new WaitForEndOfFrame();
            CheckHudLayout();
            Capture("player-01-praca-pt");

            Vector2 start = ground.FeetPlate;
            Require(!stations.Interact(), "No station interaction from spawn");
            WalkTo(new Vector2(452, 612));
            Require(Vector2.Distance(start, ground.FeetPlate) > 240f, "Keeper moved across the plaza");
            Require(stations.Interact() && stations.WorldInputPaused, "Team opens from nearby station action");
            Vector2 stopped = ground.FeetPlate;
            ground.Guardian.Tick(Vector2.right, 0.5f);
            Require(Vector2.Distance(stopped, ground.FeetPlate) < 0.1f, "Modal pauses walking");
            yield return null;
            yield return new WaitForEndOfFrame();
            Capture("player-02-equipe-pt");
            Press("slotLeaf");
            Press("teamSave");
            Require(!stations.WorldInputPaused && HubPreferences.Load(TestKey).TeamMask == 3, "Team saved and walking restored");

            ground.Guardian.transform.position = ground.PlateToGround(start);
            WalkTo(new Vector2(1058, 598));
            Require(stations.PointerAt(stations.Spot(HubStation.Expeditions).pointerArea.center), "Painted desk opens atlas");
            yield return null;
            yield return new WaitForEndOfFrame();
            Capture("player-03-atlas-pt");
            Press("planRoute");
            Require(HubPreferences.Load(TestKey).ExpeditionPlanned, "Atlas route marked");
            Press("atlasBack");

            ground.Guardian.transform.position = ground.PlateToGround(start);
            WalkTo(new Vector2(868, 441));
            yield return null;
            yield return new WaitForEndOfFrame();
            var flame = ground.Stage.transform.Find("CampfireFlame").GetComponent<SpriteRenderer>();
            int frontOrder = flame.sortingOrder;
            Require(frontOrder > 0 && frontOrder > 50, "Campfire in foreground above baseline");
            Capture("player-04-fogueira-oclusao");
            WalkTo(new Vector2(902, 490));
            yield return null;
            yield return new WaitForEndOfFrame();
            Require(flame.sortingOrder < frontOrder, "Campfire returns to painted order below baseline");
            ground.Guardian.Tick(Vector2.up, 3f);
            Require(ground.IsWalkablePlate(ground.FeetPlate), "Large movement stays on walkable ground");
            Require(!ground.IsWalkablePlate(new Vector2(931, 447)), "Fire centre blocks the floor");

            AudioSource[] audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            Require(audio.Length == 2 && audio.All(source => source.clip != null && source.loop && source.isPlaying),
                "Wind and campfire sources playing");
            AudioSource campfire = audio.Single(source => source.gameObject.name == "Campfire");
            AudioSource breeze = audio.Single(source => source.gameObject.name == "Wind and birds");
            Require(campfire.volume > 0.1f, "Approaching fire increases audio level");

            const double signalFloor = 0.0001;
            var samples = new float[2048];
            double mixRms = MeasureAudio(samples);
            float audioDeadline = Time.unscaledTime + 5f;
            while (mixRms < signalFloor && Time.unscaledTime < audioDeadline)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                mixRms = MeasureAudio(samples);
            }

            campfire.mute = true;
            yield return new WaitForSecondsRealtime(0.25f);
            double breezeRms = MeasureAudio(samples);

            campfire.mute = false;
            breeze.mute = true;
            yield return new WaitForSecondsRealtime(0.25f);
            double campfireRms = MeasureAudio(samples);
            breeze.mute = false;

            float volume = AudioListener.volume;
            AudioListener.volume = 0f;
            yield return new WaitForSecondsRealtime(0.25f);
            double mutedRms = MeasureAudio(samples);

            AudioListener.volume = volume;
            yield return new WaitForSecondsRealtime(0.25f);
            double restoredRms = MeasureAudio(samples);

            bool audioPassed = mixRms >= signalFloor && breezeRms >= signalFloor && campfireRms >= signalFloor
                && mutedRms < 0.00001 && restoredRms >= signalFloor;
            File.WriteAllText(Path.Combine(output, "player-audio.txt"), FormattableString.Invariant(
                $"{(audioPassed ? "PASS" : "FAIL")}: Windows player RMS. Mix {mixRms:F7}, wind and birds {breezeRms:F7}, campfire {campfireRms:F7}, muted {mutedRms:F7}, restored {restoredRms:F7}. Output sample rate: {AudioSettings.outputSampleRate}."));
            Require(audioPassed, "Ambient audio reaches the listener and follows master mute");
            Press("openSettings");
            Press("langEn");
            Require(Text("settingsHeading") == "Settings", "English preview");
            yield return null;
            yield return new WaitForEndOfFrame();
            Capture("player-05-settings-en");
            Press("settingsBack");
            Require(Text("location") == "Observatório da Aurora", "Cancel restores locale");
            Press("openSettings");
            Press("langEs");
            Press("volumeDown");
            Press("motionToggle");
            Require(Text("settingsHeading") == "Ajustes", "Spanish preview");
            yield return null;
            yield return new WaitForEndOfFrame();
            Capture("player-06-ajustes-es");
            Press("settingsSave");
            Require(Text("location") == "Observatorio de la Aurora", "Spanish confirmed on plaza");
        }

        private static double MeasureAudio(float[] samples)
        {
            AudioListener.GetOutputData(samples, 0);
            double left = samples.Select(value => (double)value * value).Average();
            if (AudioSettings.speakerMode == AudioSpeakerMode.Mono) return Math.Sqrt(left);
            AudioListener.GetOutputData(samples, 1);
            double right = samples.Select(value => (double)value * value).Average();
            return Math.Sqrt((left + right) * 0.5);
        }

        private IEnumerator VerifyReload()
        {
            HubPreferences saved = HubPreferences.Load(TestKey);
            Require(Text("location") == "Observatorio de la Aurora" && saved.Language == "es", "Spanish restored");
            Require(saved.TeamMask == 3 && root.Q("rosterLeaf").ClassListContains("is-resting"), "Team membership restored");
            Require(saved.ExpeditionPlanned && Text("bookSubtitle") == "Ruta marcada en el atlas", "Route mark restored");
            Require(Mathf.Abs(AudioListener.volume - 0.62f) < 0.01f, "Audio volume restored");
            Require(saved.ReducedMotion && FindObjectsByType<HubLayerMotion>(FindObjectsSortMode.None)
                .Where(layer => layer.Motion != HubMotion.Static).All(layer => !layer.enabled), "Reduced motion restored");
            yield return new WaitForEndOfFrame();
            CheckHudLayout();
            Capture(wideReview ? "player-08-praca-es-16x10" : "player-07-praca-es-reabertura");
            Press("openAtlas");
            yield return null;
            yield return new WaitForEndOfFrame();
            Require(Text("atlasHeading") == "Valle del Manantial", "Spanish atlas translated");
            Capture(wideReview ? "player-09-atlas-es-16x10" : "player-10-atlas-es");
        }

        private IEnumerator VerifyInputs()
        {
            var controller = FindFirstObjectByType<HubUiController>();
            Vector2 spawn = ground.FeetPlate;
            yield return new WaitForEndOfFrame();
            SetInputStep("walk");
            float timeout = Time.unscaledTime + 15;
            while (ground.FeetPlate.x > spawn.x - 15 && Time.unscaledTime < timeout) yield return null;
            Require(ground.FeetPlate.x < spawn.x - 15, "A moved the Keeper left");

            SetInputStep("settings");
            timeout = Time.unscaledTime + 15;
            while (!controller.IsModalOpen && Time.unscaledTime < timeout) yield return null;
            Require(controller.IsModalOpen, "Escape opened settings");
            var first = root.Q<Button>("langPt");
            timeout = Time.unscaledTime + 5;
            while (root.focusController.focusedElement != first && Time.unscaledTime < timeout) yield return null;
            Require(root.focusController.focusedElement == first, "Settings moved focus to its first button");

            SetInputStep("language");
            timeout = Time.unscaledTime + 15;
            while (Text("settingsHeading") != "Settings" && Time.unscaledTime < timeout) yield return null;
            Require(Text("settingsHeading") == "Settings", "Tab and Enter selected the next language");

            SetInputStep("close");
            timeout = Time.unscaledTime + 15;
            while (controller.IsModalOpen && Time.unscaledTime < timeout) yield return null;
            Require(!controller.IsModalOpen && Text("location") == "Observatório da Aurora", "Escape cancelled language preview");
            var shortcut = root.Q<Button>("openSettings");
            timeout = Time.unscaledTime + 5;
            while (root.focusController.focusedElement != shortcut && Time.unscaledTime < timeout) yield return null;
            Require(root.focusController.focusedElement == shortcut, "Focus returned to the plaza settings button");

            SetPointerStep("click", "openAtlas");
            timeout = Time.unscaledTime + 15;
            while (root.Q("atlasPanel").ClassListContains("is-hidden") && Time.unscaledTime < timeout) yield return null;
            Require(!root.Q("atlasPanel").ClassListContains("is-hidden"), "Left mouse button opened the book");
            yield return new WaitForEndOfFrame();
            Capture("player-11-atlas-mouse");
            Press("atlasBack");
            WalkTo(new Vector2(452, 612));
            yield return null;
            SetInputStep("station");
            timeout = Time.unscaledTime + 15;
            while (root.Q("teamPanel").ClassListContains("is-hidden") && Time.unscaledTime < timeout) yield return null;
            Require(!root.Q("teamPanel").ClassListContains("is-hidden"), "E opened the team stall");
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(.32f);
            yield return new WaitForEndOfFrame();
            Capture("workshop-station-e");

            var teamTab = root.Q<Button>("tabTeam");
            SetPointerStep("eggs", "tabEggs");
            timeout = Time.unscaledTime + 15;
            while (root.Q("eggPane").ClassListContains("is-hidden") && Time.unscaledTime < timeout) yield return null;
            Require(!root.Q("eggPane").ClassListContains("is-hidden"), "Mouse opened the Eggs tab");
            yield return new WaitForEndOfFrame();
            SetPointerStep("buy", "buyFire");
            timeout = Time.unscaledTime + 15;
            while (Text("hubCoins") == "150" && Time.unscaledTime < timeout) yield return null;
            var roster = HubRosterState.Load(TestKey, 7);
            Require(roster.Coins == 125 && roster.Eggs.Count == 1, "Mouse bought one Brasa egg");
            string eggButton = $"egg-action-{roster.Eggs[0].Id}";
            yield return new WaitForEndOfFrame();
            SetPointerStep("incubate", eggButton);
            timeout = Time.unscaledTime + HubRosterState.IncubationSeconds + 8;
            Button egg;
            do
            {
                yield return null;
                egg = root.Q<Button>(eggButton);
            }
            while (!egg.text.StartsWith("Chocar", StringComparison.Ordinal) && Time.unscaledTime < timeout);
            Require(egg.enabledInHierarchy && egg.text.StartsWith("Chocar", StringComparison.Ordinal), "Incubation made the egg ready to hatch");
            SetPointerStep("hatch", eggButton);
            timeout = Time.unscaledTime + 15;
            var next = root.Q<Button>("hatchContinue");
            while (root.focusController.focusedElement != next && Time.unscaledTime < timeout) yield return null;
            Require(root.focusController.focusedElement == next, "Hatch moved focus to its continue button");
            yield return new WaitForSecondsRealtime(.70f);
            yield return new WaitForEndOfFrame();
            Capture("workshop-hatch-input");

            SetInputStep("continue");
            timeout = Time.unscaledTime + 15;
            while (root.focusController.focusedElement != teamTab && Time.unscaledTime < timeout) yield return null;
            Require(root.focusController.focusedElement == teamTab && root.Q("hatchReveal").ClassListContains("is-hidden"),
                "Enter closed hatch and focused the Team tab");
            SetInputStep("fusion");
            timeout = Time.unscaledTime + 15;
            while (root.Q("fusionPane").ClassListContains("is-hidden") && Time.unscaledTime < timeout) yield return null;
            Require(!root.Q("fusionPane").ClassListContains("is-hidden"), "Tab and Enter opened Fusion from Team");
            yield return new WaitForEndOfFrame();
            SetPointerStep("fusion-back", "fusionBack");
            timeout = Time.unscaledTime + 15;
            while (root.focusController.focusedElement != teamTab && Time.unscaledTime < timeout) yield return null;
            Require(root.focusController.focusedElement == teamTab && root.Q("fusionPane").ClassListContains("is-hidden"),
                "Back moved keyboard focus to Team");
            yield return new WaitForEndOfFrame();
            Capture("workshop-team-focus-input");

            SetInputStep("close-team");
            timeout = Time.unscaledTime + 15;
            var plazaTeam = root.Q<Button>("openTeam");
            while (root.focusController.focusedElement != plazaTeam && Time.unscaledTime < timeout) yield return null;
            Require(root.focusController.focusedElement == plazaTeam && !controller.IsModalOpen && !stations.WorldInputPaused,
                "Escape returned focus and movement to the plaza");
        }

        private void SetPointerStep(string step, string button)
        {
            Vector2 center = root.Q<Button>(button).worldBound.center;
            Rect surface = root.worldBound;
            int x = Mathf.RoundToInt(Screen.width * (center.x - surface.xMin) / surface.width);
            int y = Mathf.RoundToInt(Screen.height * (center.y - surface.yMin) / surface.height);
            SetInputStep($"{step} {x} {y}");
        }

        private void SetInputStep(string step)
        {
            File.WriteAllText(Path.Combine(output, "player-input-step.txt"), step);
        }

        private IEnumerator VerifyWorkshop()
        {
            WalkTo(new Vector2(452, 612));
            Require(stations.Interact() && !root.Q("teamPanel").ClassListContains("is-hidden"), "Workshop opens from team station");
            yield return new WaitForEndOfFrame();
            Capture("workshop-opening");
            yield return new WaitForSecondsRealtime(.32f);
            Press("tabEggs");
            Press("buyFire");
            Press("buyLeaf");
            HubRosterState state = HubRosterState.Load(WorkshopKey, 7);
            Require(state.Coins == 100 && state.Eggs.Count == 2, "Two egg purchases debit 50 coins");
            int egg = state.Eggs[0].Id;
            Press($"egg-action-{egg}");
            state = HubRosterState.Load(WorkshopKey, 7);
            long deadline = state.Eggs[0].ReadyAtTicks;
            Require(deadline > DateTime.UtcNow.Ticks, "Incubation end persisted");
            Require(state.StartIncubation(state.Eggs[1].Id, DateTime.UtcNow.Ticks) == HubWorkshopResult.IncubatorBusy,
                "Second egg cannot occupy the incubator");
            Require(state.HatchEgg(egg, DateTime.UtcNow.Ticks) == HubWorkshopResult.EggNotReady, "Egg cannot hatch before its deadline");
            yield return new WaitForEndOfFrame();
            Capture("workshop-eggs");

            while (DateTime.UtcNow.Ticks <= deadline || !root.Q<Button>($"egg-action-{egg}").enabledInHierarchy)
                yield return null;
            Press($"egg-action-{egg}");
            state = HubRosterState.Load(WorkshopKey, 7);
            int newId = state.Creatures.Single(item => item.Id > 3 && item.Element == 0).Id;
            Require(state.Eggs.Count == 1 && state.Coins == 100, "Hatch converts one egg into one creature without charging");
            Require(!root.Q("hatchReveal").ClassListContains("is-hidden") && !root.Q<Button>("tabTeam").enabledInHierarchy,
                "Hatching opens the reveal and holds focus in it");
            for (int frame = 0; frame < 8; frame++)
            {
                yield return new WaitForEndOfFrame();
                Capture($"workshop-hatch-{frame:00}");
                yield return new WaitForSecondsRealtime(.06f);
            }
            Require(root.Q("hatchPortrait").style.opacity.value >= .995f &&
                root.Q("hatchTitle").style.opacity.value >= .995f, "Hatch reveal finishes with the Lumon and its name visible");
            Capture("workshop-hatch");
            Press("hatchContinue");
            yield return new WaitForSecondsRealtime(.06f);
            Require(root.panel.focusController.focusedElement == root.Q("tabTeam"),
                "Continuing after hatch moves keyboard focus to the team tab");
            foreach (var (tab, back) in new[] { ("tabFusion", "fusionBack"), ("tabImprove", "improveBack"), ("tabEggs", "eggBack") })
            {
                Press(tab);
                Press(back);
                yield return new WaitForSecondsRealtime(.06f);
                Require(root.panel.focusController.focusedElement == root.Q("tabTeam"),
                    $"{back} moves keyboard focus to the team tab");
            }
            yield return new WaitForEndOfFrame();
            Capture("workshop-after-hatch-focus");
            Press("slotLeaf");
            Press($"extra-{newId}");
            Press("teamSave");
            state = HubRosterState.Load(WorkshopKey, 7);
            Require(state.Team.Contains(newId) && !state.Team.Contains(3), "Newly hatched creature selectable and persistent");
            Require(stations.Interact(), "Workshop reopens");
            Press("tabFusion");
            Press("fuse-target-1");
            Require(root.Q<Button>($"fuse-material-{newId}") == null, "Active party creature cannot be spent");
            Press("tabTeam");
            Press($"extra-{newId}");
            Press("slotLeaf");
            Press("teamSave");
            Require(stations.Interact(), "Workshop reopens after freeing material");
            yield return new WaitForSecondsRealtime(.32f);
            Press("tabFusion");
            Press("fuse-target-1");
            Press($"fuse-material-{newId}");
            Press("fusionAction");
            state = HubRosterState.Load(WorkshopKey, 7);
            Require(state.Coins == 100 && state.Creatures.Count == 4, "Fusion preview leaves coins and material intact");
            yield return new WaitForEndOfFrame();
            Require(root.Q<Button>($"fuse-target-{newId}").worldBound.yMax + 3f <= root.Q("fusionState").worldBound.yMin,
                "Fourth fusion candidate fits above the confirmation");
            Capture("workshop-fusion");
            Press("fusionAction");
            state = HubRosterState.Load(WorkshopKey, 7);
            Require(state.Coins == 65 && state.Creatures.Count == 3 && state.Creatures.Single(x => x.Id == 1).Rank == 2,
                "Confirmed fusion debits coins, consumes material, and increases rank");

            Press("tabImprove");
            Press("improve-1");
            Press("improveAction");
            state = HubRosterState.Load(WorkshopKey, 7);
            Require(state.Coins == 35 && state.Creatures.Single(x => x.Id == 1).Level == 2, "Upgrade debits coins and increases level");
            yield return new WaitForEndOfFrame();
            Capture("workshop-improve");

            Press("tabEggs");
            Press("buyWater");
            state = HubRosterState.Load(WorkshopKey, 7);
            Require(state.Coins == 10 && !root.Q<Button>("buyWater").enabledInHierarchy,
                "Insufficient balance disables purchase without changing the balance");
            Press($"egg-action-{state.Eggs[0].Id}");
            Require(HubRosterState.Load(WorkshopKey, 7).Eggs.Count(item => item.ReadyAtTicks > 0) == 1,
                "A second incubation is left in progress for the next process");
        }

        private IEnumerator VerifyWorkshopReload()
        {
            HubRosterState state = HubRosterState.Load(WorkshopKey, 7);
            Require(state.Coins == 10 && state.Eggs.Count == 2 && state.Creatures.Count == 3, "Balance and inventory restored in a new process");
            Require(state.Team.SequenceEqual(new[] { 1, 2, 3 }), "Final team restored in a new process");
            Require(state.Creatures.Single(x => x.Id == 1).Rank == 2 && state.Creatures.Single(x => x.Id == 1).Level == 2,
                "Fusion rank and upgrade level restored in a new process");
            Require(root.Q<Label>("hubCoins").text == "10", "Plaza shows saved balance");
            HubEgg incubating = state.Eggs.SingleOrDefault(item => item.ReadyAtTicks > 0);
            Require(incubating != null, "An in-progress incubation reloads from disk");
            Press("openSettings");
            Press("motionToggle");
            Press("settingsSave");
            WalkTo(new Vector2(452, 612));
            Require(stations.Interact() && root.Q("teamPanel").style.opacity.value == 1f,
                "Reduced motion opens the workshop in its final pose");
            Press("tabEggs");
            string action = $"egg-action-{incubating.Id}";
            while (DateTime.UtcNow.Ticks <= incubating.ReadyAtTicks || !root.Q<Button>(action).enabledInHierarchy)
                yield return null;
            Press(action);
            Require(root.Q("hatchPortrait").style.opacity.value == 1f &&
                root.Q("hatchTitle").style.opacity.value == 1f &&
                root.Q("hatchShellTop").style.opacity.value == 0f, "Reduced-motion hatch is immediately readable");
            yield return new WaitForEndOfFrame();
            Capture("workshop-hatch-reduced");
        }


        private IEnumerator VerifyDesign()
        {
            foreach (var (locale, option) in new[] { ("pt", "langPt"), ("en", "langEn"), ("es", "langEs") })
            {
                Press("openSettings");
                Press(option);
                yield return null;
                yield return new WaitForEndOfFrame();
                CheckTextFit("settingsHeading", "languageLabel", "volumeLabel", "motionLabel", "volumeValue",
                    "langPt", "langEn", "langEs", "motionToggle", "settingsBack", "settingsSave");
                yield return new WaitForSecondsRealtime(.31f);
                yield return new WaitForEndOfFrame();
                Capture($"design-{locale}-settings");
                Press("settingsSave");
                yield return new WaitForSecondsRealtime(2.9f);
                yield return new WaitForEndOfFrame();
                CheckHudLayout();
                CheckTextFit("location", "subtitle", "teamTitle", "bookTitle", "bookSubtitle", "openTeam", "openAtlas",
                    "deskLabel", "deskAction", "boothLabel", "controls", "rosterFire", "rosterWater", "rosterLeaf");
                Capture($"design-{locale}-plaza");
                Press("openTeam");
                yield return null;
                yield return new WaitForEndOfFrame();
                CheckTextFit("teamHeading", "teamIntro", "teamState", "teamBack", "teamSave", "slotFireName",
                    "slotFireState", "slotWaterName", "slotWaterState", "slotLeafName", "slotLeafState");
                yield return new WaitForSecondsRealtime(.31f);
                yield return new WaitForEndOfFrame();
                Capture($"design-{locale}-team");
                CheckTextFit("workshopBalance", "tabTeam", "tabFusion", "tabImprove", "tabEggs");
                Press("tabFusion");
                yield return null;
                yield return new WaitForEndOfFrame();
                CheckTextFit("fusionIntro", "fusionTargetLabel", "fusionMaterialLabel", "fusionState", "fusionAction", "fusionBack");
                Capture($"design-{locale}-fusion");
                Press("tabImprove");
                yield return null;
                yield return new WaitForEndOfFrame();
                CheckTextFit("improveIntro", "improveState", "improveAction", "improveBack");
                Capture($"design-{locale}-improve");
                Press("tabEggs");
                yield return null;
                yield return new WaitForEndOfFrame();
                CheckTextFit("eggIntro", "eggState", "shopLabel", "incubatorLabel", "buyFire", "buyWater", "buyLeaf", "eggBack");
                Capture($"design-{locale}-eggs");
                Press("tabTeam");
                Press("teamBack");
                Press("openAtlas");
                yield return null;
                yield return new WaitForEndOfFrame();
                CheckTextFit("atlasHeading", "atlasIntro", "routeState", "atlasBack", "planRoute");
                yield return new WaitForSecondsRealtime(.31f);
                yield return new WaitForEndOfFrame();
                Capture($"design-{locale}-atlas");
                Press("atlasBack");
            }
            Require(designIssues.Count == 0, string.Join("\n", designIssues));
        }

        private IEnumerator CaptureMotion()
        {
            int previousFrameRate = Time.captureFramerate;
            Time.captureFramerate = 10;
            try
            {
                for (int i = 0; i < 60; i++)
                {
                    yield return new WaitForEndOfFrame();
                    Capture($"motion-{i:00}");
                }
            }
            finally
            {
                Time.captureFramerate = previousFrameRate;
            }
            Press("openSettings");
            Press("motionToggle");
            Press("settingsSave");
            yield return new WaitForSecondsRealtime(3f);
            yield return new WaitForEndOfFrame();
            Capture("reduced-a");
            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();
            Capture("reduced-b");
        }

        private void CheckTextFit(params string[] names)
        {
            foreach (string name in names)
            {
                var element = root.Q<TextElement>(name);
                Rect bounds = element.contentRect;
                var widthMode = element.resolvedStyle.whiteSpace == WhiteSpace.NoWrap
                    ? VisualElement.MeasureMode.Undefined : VisualElement.MeasureMode.AtMost;
                Vector2 required = element.MeasureTextSize(element.text, bounds.width, widthMode,
                    0, VisualElement.MeasureMode.Undefined);
                if (bounds.width <= 0 || required.x > bounds.width + 1f || required.y > bounds.height + 2f)
                    designIssues.Add($"Text '{name}' requires {required.x:F1} × {required.y:F1}, available {bounds.width:F1} × {bounds.height:F1}: {element.text}");
            }
        }


        private void CheckHudLayout()
        {
            Rect canvas = root.Q("hubCanvas").worldBound;
            Rect title = root.Q("location").worldBound;
            Rect subtitle = root.Q("subtitle").worldBound;
            Rect book = root.Q("bookTitle").worldBound;
            Require(canvas.height > 320f && Mathf.Abs(canvas.width / canvas.height - 16f / 9f) < 0.01f, "Plaza UI fits the fixed camera frame");
            Require(title.yMax < subtitle.yMin, "Observatory title clears its subtitle");
            Require(book.yMin > canvas.yMin + canvas.height * 0.6f && book.yMax < root.Q("bookSubtitle").worldBound.yMin,
                "Book title fits above the second line of copy");
        }

        private string ReportPath() => Path.Combine(output, workshopReview ? (readOnly ? "workshop-reload.txt" : "workshop-play.txt") : designReview ? "player-design.txt" : motionReview ? "player-motion.txt" : inputReview ? "player-input.txt" : wideReview ? "player-wide.txt" : readOnly ? "player-reload.txt" : "player-smoke.txt");

        private void WalkTo(Vector2 target)
        {
            for (int step = 0; step < 720 && Vector2.Distance(ground.FeetPlate, target) > 4f; step++)
            {
                Vector3 delta = ground.PlateToGround(target) - ground.Guardian.transform.position;
                Vector2 input = new Vector2(delta.x, delta.z);
                ground.Guardian.Tick(input.normalized, Mathf.Min(1f / 60f, input.magnitude / ground.Guardian.Speed));
            }
            Require(Vector2.Distance(ground.FeetPlate, target) < 9f, $"Walk to {target}, reached {ground.FeetPlate}");
        }

        private string Text(string name) => root.Q<TextElement>(name).text;

        private void Press(string name)
        {
            var button = root.Q<Button>(name);
            Require(button != null && button.enabledInHierarchy, $"Available control {name}");
            using var submit = NavigationSubmitEvent.GetPooled();
            submit.target = button;
            button.SendEvent(submit);
        }

        private void Capture(string name)
        {
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG());
            }
            finally
            {
                Destroy(frame);
            }
        }

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
