using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class HubUiController : MonoBehaviour
    {
        [SerializeField] private HubSplatWorld world;
        [SerializeField] private Camera view;
        private VisualElement root;
        private VisualElement body;
        private VisualElement panel;
        private HubPreferences preferences;
        private HubRosterState roster;
        private LumonStationId current;
        [SerializeField] private Color[] palette = Array.Empty<Color>();
        private readonly Dictionary<LumonStationId, Button> markers = new();
        private const float MarkerHeight = 24f;
        private const float MarkerGap = 7f;
        private static readonly int[] MarkerAngles = { 0, -20, 20, -40, 40, -60, 60, -80, 80, -100, 100, -120, 120, -140, 140, -160, 160, 180 };
        private readonly List<Rect> protectedAreas = new();
        private readonly List<Rect> placedMarkers = new();
        private readonly Dictionary<LumonStationId, Rect> stationEnvelopes = new();
        private VisualElement brand;
        private VisualElement currencyAndSettings;
        private VisualElement interactionPrompt;
        private VisualElement controls;
        private SplatCameraRig cameraRig;
        private DawnForgeWorkshop workshop;
        private HubLanSession session;
        private Button partyButton;
        private readonly Label[] playerTags = new Label[HubLanSession.Capacity];
        private int lastRoster = -1, lastSlot = -2;
        private bool lastActive;
        private string lastNotice, savedAddress = "";


        private readonly List<int> teamSelection = new();
        private readonly List<SplatAssetPreview> assetPreviews = new();
        private int teamPreviewId;
        private int fusionTarget;
        private int fusionMaterial;
        private float opening = 1f;
        private IVisualElementScheduledItem toastTimer;

        public bool IsModalOpen => current != LumonStationId.None;

        public void Configure(HubSplatWorld hub, Camera camera, Color[] colors) { world = hub; view = camera; palette = colors; }

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null || world == null) return;
            if (view != null) view.TryGetComponent(out cameraRig);
            workshop = world.GetComponentInChildren<DawnForgeWorkshop>();
            root.pickingMode = PickingMode.Ignore;
            body = root.Q("stationBody");
            root.Q<ScrollView>("stationScroll").horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            panel = root.Q("panelShadow");
            brand = root.Q(className: "brand");
            currencyAndSettings = root.Q(className: "hud-right");
            interactionPrompt = root.Q("interactionPrompt");
            controls = root.Q(className: "controls");
            preferences = HubPreferences.Load();
            roster = HubRosterState.Load(HubPreferences.ActiveKeyPrefix, preferences.TeamMask);
            world.ReducedMotion = preferences.ReducedMotion;
            session = world.GetComponent<HubLanSession>() ?? world.gameObject.AddComponent<HubLanSession>();
            partyButton = root.Q<Button>("partyButton");
            partyButton.clicked += () => Open(LumonStationId.Party);
            for (int i = 0; i < playerTags.Length; i++)
            {
                playerTags[i] = Styled(new Label(HubLanSession.Callsigns[i]) { pickingMode = PickingMode.Ignore }, "party-tag is-hidden");
                root.Q("markerLayer").Add(playerTags[i]);
            }
            InstallGlyph("brandGlyph", 10);
            InstallGlyph("coinGlyph", 7);
            root.Q<Button>("settingsButton").Add(new LumonPixelGlyph { Kind = 6 });
            InstallMarker(LumonStationId.WestScope, "markerWestScope", 10);
            InstallMarker(LumonStationId.EastScope, "markerEastScope", 10);
            InstallMarker(LumonStationId.Team, "markerTeam", 0);
            InstallMarker(LumonStationId.Nest, "markerNest", 1);
            InstallMarker(LumonStationId.EggShop, "markerEggShop", 2);
            InstallMarker(LumonStationId.Fusion, "markerFusion", 3);
            InstallMarker(LumonStationId.Improve, "markerImprove", 4);
            InstallMarker(LumonStationId.Expeditions, "markerExpeditions", 5);
            root.Q<Button>("settingsButton").clicked += () => Open(LumonStationId.Settings);
            root.Q<Button>("closeButton").clicked += Close;
            world.StationRequested += Open;
            world.NearbyChanged += RefreshPrompt;
            world.PointerOverInterface = PointerOverInterface;
            RefreshCoins();
            ApplyFonts();
            RefreshPrompt(world.Nearby);
        }

        private void ApplyFonts()
        {
            UnityEngine.TextCore.Text.FontAsset title = Resources.Load<UnityEngine.TextCore.Text.FontAsset>("Lumon/Fonts/SourceSerif4-Semibold");
            UnityEngine.TextCore.Text.FontAsset bodyFont = Resources.Load<UnityEngine.TextCore.Text.FontAsset>("Lumon/Fonts/AtkinsonHyperlegible-Regular");
            if (title == null || bodyFont == null)
            {
                Debug.LogError($"LUMON_FONT_LOAD title={title != null} body={bodyFont != null}");
                return;
            }
            root.Query<TextElement>().ForEach(element =>
                element.style.unityFontDefinition = new FontDefinition { fontAsset = element.ClassListContains("display-title") ? title : bodyFont });
            Debug.Log("LUMON_FONT_LOAD applied=runtime");
        }

        private void OnDisable()
        {
            toastTimer?.Pause();
            if (world != null)
            {
                world.StationRequested -= Open;
                world.NearbyChanged -= RefreshPrompt;
                world.PointerOverInterface = null;
                world.SetPaused(this, false);
            }
            markers.Clear();
            ClearPreviews();
        }

        private void ClearPreviews()
        {
            foreach (SplatAssetPreview preview in assetPreviews)
                if (preview != null) Destroy(preview.gameObject);
            assetPreviews.Clear();
        }

        private VisualElement EggPreview(int tier, string className)
        {
            GameObject host = new($"Preview de ovo — tier {tier}");
            SplatAssetPreview preview = host.AddComponent<SplatAssetPreview>();
            preview.ConfigureEgg(tier, palette, preferences.ReducedMotion, className == "nest-hero");
            assetPreviews.Add(preview);
            preview.Image.AddToClassList(className);
            return preview.Image;
        }

        private void InstallMarker(LumonStationId id, string name, int glyph)
        {
            Button button = root.Q<Button>(name);
            if (button == null) return;
            button.style.width = MarkerWidth(id);
            button.style.height = MarkerHeight;
            button.Q(className: "marker-glyph")?.Add(new LumonPixelGlyph { Kind = glyph });
            button.clicked += () =>
            {
                if (!world.Request(id)) Toast("Caminhe até a estação para interagir.");
            };
            markers.Add(id, button);
        }

        private void InstallGlyph(string name, int kind)
        {
            VisualElement host = root.Q(name);
            if (host != null) host.Add(new LumonPixelGlyph { Kind = kind });
        }

        private void Update()
        {
            if (root == null) return;
            if (session.RosterState != lastRoster || session.Notice != lastNotice || session.LocalSlot != lastSlot || session.Active != lastActive)
            {
                lastRoster = session.RosterState; lastNotice = session.Notice; lastSlot = session.LocalSlot; lastActive = session.Active;
                partyButton.text = session.LocalSlot >= 0 ? $"Grupo  {session.Count}/6" : "Grupo";
                if (current is LumonStationId.Party or LumonStationId.WestScope or LumonStationId.EastScope) BuildPanel();
            }
            if (HubQaInput.GetKeyDown(KeyCode.Escape))
            {
                if (IsModalOpen) Close(); else Open(LumonStationId.Settings);
            }
            if (!IsModalOpen || opening >= 1f) return;
            opening = Mathf.Min(1f, opening + Time.unscaledDeltaTime * 5.5f);
            float eased = 1f - Mathf.Pow(1f - opening, 3f);
            panel.style.opacity = eased;
            panel.style.scale = new Scale(Vector2.one * Mathf.Lerp(0.94f, 1f, eased));
        }

        private void LateUpdate()
        {
            if (view == null || root?.panel == null || IsModalOpen) return;
            for (int i = 0; i < playerTags.Length; i++)
            {
                Label tag = playerTags[i];
                bool show = session.LocalSlot >= 0 && session.Occupied(i) && !(cameraRig != null && cameraRig.Inspecting);
                if (show)
                {
                    Vector2 at = Project(session.Position(i) + view.transform.up * 1.62f);
                    show = root.worldBound.Contains(at);
                    bool compact = cameraRig != null && cameraRig.Overview;
                    tag.EnableInClassList("map-tag", compact);
                    tag.style.left = at.x - (compact ? 10 : 34); tag.style.top = at.y - 20;
                    tag.text = compact ? (i + 1).ToString() : HubLanSession.Callsigns[i] + (session.Ready(i) ? " •" : "");
                }
                tag.EnableInClassList("is-hidden", !show);
            }
            protectedAreas.Clear();
            placedMarkers.Clear();
            if (cameraRig != null && cameraRig.Inspecting)
            {
                foreach (Button marker in markers.Values) marker.AddToClassList("is-hidden");
                return;
            }
            foreach (Label tag in playerTags)
                if (!tag.ClassListContains("is-hidden")) protectedAreas.Add(tag.worldBound);
            protectedAreas.Add(brand.worldBound);
            protectedAreas.Add(currencyAndSettings.worldBound);
            protectedAreas.Add(controls.worldBound);
            if (!interactionPrompt.ClassListContains("is-hidden")) protectedAreas.Add(interactionPrompt.worldBound);

            Vector3 guardian = world.GuardianPosition;
            if (workshop != null)
            {
                protectedAreas.Add(ProjectEnvelope(workshop.AnimalCenter,new Vector3(.72f,.36f,.48f)));
                protectedAreas.Add(ProjectEnvelope(workshop.ArtisanCenter,new Vector3(.72f,.68f,.48f)));
            }
            protectedAreas.Add(ProjectEnvelope(guardian + Vector3.up * 0.65f, new Vector3(0.34f, 0.68f, 0.34f)));
            protectedAreas.Add(ProjectEnvelope(new Vector3(0.55f, 0.33f, -0.55f), new Vector3(0.71f, 0.35f, 0.71f)));
            protectedAreas.Add(ProjectEnvelope(new Vector3(0.55f, 1.53f, -0.55f), new Vector3(0.73f, 0.76f, 0.73f)));
            foreach (SplatStationSpot spot in world.Spots)
            {
                Vector3 halfSize = StationHalfSize(spot.id);
                Rect envelope = ProjectEnvelope(spot.position + Vector3.up * halfSize.y, halfSize);
                stationEnvelopes[spot.id] = envelope;
                protectedAreas.Add(envelope);
            }

            // Keep the actionable station closest to its anchor when two markers compete.
            SplatStationSpot nearby = world.Spot(world.Nearby);
            if (nearby != null) PlaceMarker(nearby);
            foreach (SplatStationSpot spot in world.Spots)
                if (spot != nearby) PlaceMarker(spot);
        }

        private static Vector3 StationHalfSize(LumonStationId id) => id switch
        {
            LumonStationId.Team => new(0.75f, 0.97f, 0.75f),
            LumonStationId.Nest => new(0.63f, 0.63f, 0.63f),
            LumonStationId.EggShop => new(0.80f, 0.84f, 0.82f),
            LumonStationId.Fusion => new(0.65f, 0.76f, 0.66f),
            LumonStationId.Improve => new(0.64f, 0.88f, 0.76f),
            _ => new(0.72f, 0.73f, 0.72f),
        };

        private static float MarkerWidth(LumonStationId id) => id switch
        {
            LumonStationId.Expeditions => 108f,
            LumonStationId.Improve => 102f,
            LumonStationId.Team => 86f,
            _ => 74f,
        };

        private Vector2 Project(Vector3 worldPoint)
        {
            Vector3 screen = view.WorldToScreenPoint(worldPoint);
            return RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
        }

        private Rect ProjectEnvelope(Vector3 center, Vector3 halfSize)
        {
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new((corner & 1) == 0 ? -halfSize.x : halfSize.x,
                    (corner & 2) == 0 ? -halfSize.y : halfSize.y, (corner & 4) == 0 ? -halfSize.z : halfSize.z);
                Vector2 point = Project(center + offset);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void PlaceMarker(SplatStationSpot spot)
        {
            if (!markers.TryGetValue(spot.id, out Button button)) return;
            if (cameraRig != null && !cameraRig.Overview && (spot.approach - world.GuardianPosition).sqrMagnitude > 3.4f * 3.4f)
            { button.AddToClassList("is-hidden"); return; }
            Rect envelope = stationEnvelopes[spot.id];
            Rect viewport = root.worldBound;
            if (!viewport.Overlaps(envelope)) { button.AddToClassList("is-hidden"); return; }
            Vector2 center = envelope.center;
            Vector2 outward = center - viewport.center;
            float bearing = Mathf.Atan2(outward.y, outward.x);
            float width = MarkerWidth(spot.id);
            Vector2 clearance = (envelope.size + new Vector2(width, MarkerHeight)) * 0.5f + Vector2.one * MarkerGap;
            // Follow the outside of the station continuously during orbit. Rotate only when space is blocked.
            foreach (int offset in MarkerAngles)
            {
                float angle = bearing + offset * Mathf.Deg2Rad;
                Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = Mathf.Min(clearance.x / Mathf.Abs(direction.x), clearance.y / Mathf.Abs(direction.y));
                Vector2 point = center + direction * distance;
                Rect label = new(point.x - width * 0.5f, point.y - MarkerHeight * 0.5f, width, MarkerHeight);
                Rect padded = Rect.MinMaxRect(label.xMin - 4, label.yMin - 4, label.xMax + 4, label.yMax + 4);
                if (padded.xMin < viewport.xMin || padded.xMax > viewport.xMax ||
                    padded.yMin < viewport.yMin || padded.yMax > viewport.yMax ||
                    Intersects(protectedAreas, padded) || Intersects(placedMarkers, padded)) continue;
                button.style.left = label.x;
                button.style.top = label.y;
                button.RemoveFromClassList("is-hidden");
                placedMarkers.Add(label);
                return;
            }
            button.AddToClassList("is-hidden");
        }

        private static bool Intersects(List<Rect> areas, Rect rect)
        {
            foreach (Rect area in areas) if (area.Overlaps(rect)) return true;
            return false;
        }

        internal (int visible, int hidden, int covering, int collisions) QaMarkerLayout()
        {
            int visible = 0, covering = 0, collisions = 0;
            foreach (Button button in markers.Values)
            {
                if (button.ClassListContains("is-hidden")) continue;
                visible++;
                Rect rect = button.worldBound;
                if (Intersects(protectedAreas, rect)) covering++;
                foreach (Button other in markers.Values)
                    if (other != button && !other.ClassListContains("is-hidden") && other.worldBound.Overlaps(rect)) collisions++;
            }
            return (visible, markers.Count - visible, covering, collisions / 2);
        }

        private bool PointerOverInterface(Vector2 screen)
        {
            if (IsModalOpen) return true;
            if (root?.panel == null) return false;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement target = root.panel.Pick(point);
            return target is Button || target?.GetFirstAncestorOfType<Button>() != null;
        }

        private void RefreshPrompt(LumonStationId id)
        {
            VisualElement prompt = root.Q("interactionPrompt");
            prompt.EnableInClassList("is-hidden", id == LumonStationId.None || IsModalOpen);
            if (id != LumonStationId.None) root.Q<Label>("promptLabel").text = id is LumonStationId.WestScope or LumonStationId.EastScope ? "Observar pelo telescópio" : id == LumonStationId.Improve
                ? "Aprimorar   ·   F  Ver de perto / voltar" : $"Entrar em {Title(id)}";
            foreach (var pair in markers) pair.Value.EnableInClassList("is-near", pair.Key == id);
        }

        private void Open(LumonStationId id)
        {
            if (IsModalOpen || id == LumonStationId.None) return;
            current = id;
            world.SetPaused(this, true);
            root.Q("worldHud").SetEnabled(false);
            root.Q("modalHost").RemoveFromClassList("is-hidden");
            root.Q("interactionPrompt").AddToClassList("is-hidden");
            root.Q<Label>("stationTitle").text = Title(id);
            root.Q<Label>("stationKicker").text = id == LumonStationId.Party ? "OBSERVATÓRIO LUMON" : id == LumonStationId.Settings ? "CONTROLE DO SANTUÁRIO" : "ESTAÇÃO DO SANTUÁRIO";
            VisualElement emblem = root.Q("stationEmblem");
            emblem.Clear();
            emblem.Add(new LumonPixelGlyph { Kind = Glyph(id) });
            opening = preferences.ReducedMotion ? 1f : 0f;
            panel.style.opacity = opening;
            panel.style.scale = new Scale(Vector2.one * (preferences.ReducedMotion ? 1f : 0.94f));
            BuildPanel();
            root.Q<Button>("closeButton").schedule.Execute(() => root.Q<Button>("closeButton").Focus());
        }

        private void Close()
        {
            if (!IsModalOpen) return;
            current = LumonStationId.None;
            root.Q("modalHost").AddToClassList("is-hidden");
            root.Q("worldHud").SetEnabled(true);
            root.Q("toast").AddToClassList("is-hidden");
            world.SetPaused(this, false);
            RefreshPrompt(world.Nearby);
            ClearPreviews();
        }

        private void BuildPanel()
        {
            ClearPreviews();
            body.Clear();
            switch (current)
            {
                case LumonStationId.Team: BuildTeam(); break;
                case LumonStationId.Nest: BuildNest(); break;
                case LumonStationId.EggShop: BuildShop(); break;
                case LumonStationId.Fusion: BuildFusion(); break;
                case LumonStationId.Improve: BuildImprove(); break;
                case LumonStationId.Expeditions: BuildExpeditions(); break;
                case LumonStationId.Settings: BuildSettings(); break;
                case LumonStationId.WestScope:
                case LumonStationId.EastScope: BuildScope(); break;
                case LumonStationId.Party: BuildParty(); break;
            }
            root.Q<ScrollView>("stationScroll").verticalScrollerVisibility = current == LumonStationId.EggShop ? ScrollerVisibility.Hidden : ScrollerVisibility.Auto;
            root.Q<Label>("stationHint").text = current == LumonStationId.EggShop ? "Os ovos guardam sua espécie e afinidade até o nascimento." : "ESC para voltar ao santuário";
        }

        private void BuildTeam()
        {
            if (teamSelection.Count == 0) teamSelection.AddRange(roster.Team);
            Label(body, "Escolha de 1 a 3 Lumons para a equipe.", "body-copy");

            VisualElement layout = Styled(new VisualElement(), "team-layout");
            ScrollView rows = Styled(new ScrollView(ScrollViewMode.Vertical), "team-roster");
            rows.verticalScrollerVisibility = ScrollerVisibility.Auto;
            rows.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            layout.Add(rows);

            VisualElement showcase = Styled(new VisualElement(), "team-showcase");
            GameObject host = new("Prévia da equipe");
            SplatAssetPreview preview = host.AddComponent<SplatAssetPreview>();
            preview.ConfigureCreature();
            assetPreviews.Add(preview);
            showcase.Add(Styled(preview.Image, "team-model"));
            VisualElement caption = Styled(new VisualElement(), "team-caption");
            Label profile = Styled(new Label(), "row-title pixel-title");
            caption.Add(profile);
            caption.Add(ActionButton("GIRAR 90°", preview.Turn, "turn-model"));
            showcase.Add(caption);
            layout.Add(showcase);
            body.Add(layout);

            void Inspect(HubCreature creature)
            {
                teamPreviewId = creature.Id;
                profile.text = CreatureName(creature);
                preview.ShowCreature(CreatureSpecies(creature), preferences.ReducedMotion);
            }

            HubCreature initial = roster.Creatures.FirstOrDefault(c => c.Id == teamPreviewId) ?? roster.Creatures[0];
            Inspect(initial);

            VisualElement actions = Styled(new VisualElement(), "action-bar");
            Button save = ActionButton("SALVAR EQUIPE", () =>
            {
                HubWorkshopResult result = roster.SetTeam(teamSelection);
                if (result == HubWorkshopResult.Success)
                {
                    preferences.TeamMask = roster.LegacyTeamMask;
                    preferences.Save();
                }
                Notify(result, "Equipe atualizada.");
            });
            save.SetEnabled(teamSelection.Count > 0);
            actions.Add(save);
            body.Add(actions);

            foreach (HubCreature creature in roster.Creatures)
            {
                bool selected = teamSelection.Contains(creature.Id);
                Button row = Styled(new Button(), "pixel-row");
                row.name = $"teamCreature{creature.Id}";
                row.EnableInClassList("is-selected", selected);
                row.Add(CreaturePortrait(creature));
                VisualElement main = Styled(new VisualElement(), "row-main");
                Label(main, CreatureName(creature), "row-title pixel-title");
                Label(main, $"NÍVEL {creature.Level}   •   GRAU {creature.Rank}", "row-detail pixel-title");
                row.Add(main);
                Label status = Styled(new Label(selected ? "NA EQUIPE" : "RESERVA"), "row-detail pixel-title");
                row.Add(status);
                row.RegisterCallback<PointerEnterEvent>(_ => Inspect(creature));
                row.RegisterCallback<FocusInEvent>(_ =>
                {
                    rows.ScrollTo(row);
                    Inspect(creature);
                });
                int id = creature.Id;
                row.clicked += () =>
                {
                    Inspect(creature);
                    if (teamSelection.Contains(id)) teamSelection.Remove(id);
                    else if (teamSelection.Count < HubRosterState.MaxTeamSize) teamSelection.Add(id);
                    else Toast("A equipe já tem 3 Lumons.");
                    bool isSelected = teamSelection.Contains(id);
                    row.EnableInClassList("is-selected", isSelected);
                    status.text = isSelected ? "NA EQUIPE" : "RESERVA";
                    save.SetEnabled(teamSelection.Count > 0);
                };
                rows.Add(row);
            }
        }

        private void BuildNest()
        {
            long now = DateTime.UtcNow.Ticks;
            HubEgg active = roster.Eggs.FirstOrDefault(e => e.ReadyAtTicks > 0);
            Label(body, "INCUBADORA DO NINHO", "section-title pixel-title");
            if (active != null)
            {
                bool ready = now >= active.ReadyAtTicks;
                int initialSeconds = Math.Max(0, (int)Math.Ceiling((new DateTime(active.ReadyAtTicks) - DateTime.UtcNow).TotalSeconds));
                VisualElement layout = Styled(new VisualElement(), "nest-layout");
                LumonIncubatorChamber chamber = new(ready, preferences.ReducedMotion);
                chamber.AddToClassList("nest-chamber");
                chamber.Add(EggPreview(active.Tier, "nest-hero"));
                Label(chamber, "CÂMARA / 01", "chamber-index pixel-title");
                Label(chamber, ready ? "SELO LIBERADO" : "SELO ATIVO", "chamber-seal pixel-title");
                layout.Add(chamber);

                VisualElement info = Styled(new VisualElement(), "nest-info");
                Label(info, "NÚCLEO DE INCUBAÇÃO  /  CANAL 01", "row-detail pixel-title");
                Label state = Styled(new Label(ready ? "O OVO ESTÁ PRONTO" : "INCUBANDO"), "nest-state pixel-title");
                info.Add(state);
                Label description = Styled(new Label(ready ? "A casca responde ao chamado. O selo da câmara foi liberado." : "Ciclo em andamento. Espécie e afinidade continuam ocultas."), "nest-description");
                info.Add(description);

                VisualElement telemetry = Styled(new VisualElement(), "nest-telemetry");
                Label(telemetry, "01 OVO", "telemetry-value pixel-title");
                Label(telemetry, "DADOS OCULTOS", "telemetry-value pixel-title");
                info.Add(telemetry);
                Label progressLabel = Styled(new Label(), "progress-label pixel-title");
                info.Add(progressLabel);
                VisualElement track = Styled(new VisualElement(), "progress-track");
                VisualElement[] segments = new VisualElement[18];
                for (int i = 0; i < segments.Length; i++)
                {
                    segments[i] = Styled(new VisualElement(), "progress-segment");
                    track.Add(segments[i]);
                }
                info.Add(track);
                Button action = ActionButton("CHOCAR", () => Notify(roster.HatchEgg(active.Id, DateTime.UtcNow.Ticks), "Um novo Lumon nasceu!"), "primary-action nest-action");
                action.EnableInClassList("is-hidden", !ready);
                info.Add(action);
                Label waiting = Styled(new Label($"AGUARDE  •  {initialSeconds}s"), "nest-waiting pixel-title");
                waiting.EnableInClassList("is-hidden", ready);
                info.Add(waiting);
                layout.Add(info);
                body.Add(layout);
                Label(body, "Você pode voltar ao santuário. O ciclo continua e o progresso fica salvo neste dispositivo.", "body-copy nest-note");

                IVisualElementScheduledItem ticker = null;
                ticker = layout.schedule.Execute(() =>
                {
                    if (layout.panel == null || current != LumonStationId.Nest)
                    {
                        ticker?.Pause();
                        return;
                    }
                    int seconds = Math.Max(0, (int)Math.Ceiling((new DateTime(active.ReadyAtTicks) - DateTime.UtcNow).TotalSeconds));
                    bool isReady = seconds <= 0;
                    float progress = isReady ? 1f : Mathf.Clamp01(1f - seconds / (float)HubRosterState.IncubationSeconds);
                    int filled = Mathf.CeilToInt(progress * segments.Length);
                    for (int i = 0; i < segments.Length; i++) segments[i].EnableInClassList("is-filled", i < filled);
                    track.EnableInClassList("is-ready", isReady);
                    progressLabel.text = isReady ? "CICLO CONCLUÍDO  /  100%" : $"SINCRONIA DA CASCA  /  {Mathf.FloorToInt(progress * 100f)}%";
                    if (!isReady)
                    {
                        description.text = $"Faltam {seconds} segundos. Espécie e afinidade continuam ocultas.";
                        waiting.text = $"AGUARDE  •  {seconds}s";
                    }
                    else if (!ready)
                    {
                        ticker?.Pause();
                        BuildPanel();
                    }
                }).Every(200);
                return;
            }
            if (roster.Eggs.Count == 0)
            {
                Empty("Nenhum ovo na reserva. Visite a Banca e traga uma nova descoberta.", 1);
                return;
            }
            Label(body, "Escolha um ovo da reserva. Espécie e afinidade continuam ocultas.", "body-copy");
            foreach (HubEgg egg in roster.Eggs)
            {
                VisualElement row = Row(egg.Tier == 1 ? 9 : 8, egg.Tier == 1 ? "OVO RARO" : "OVO COMUM", "Misterioso • pronto para incubar");
                row.Add(ActionButton("INCUBAR", () => Notify(roster.StartIncubation(egg.Id, DateTime.UtcNow.Ticks), "Incubação iniciada."), "row-action"));
                body.Add(row);
            }
        }

        private void BuildShop()
        {
            Label(body, "Escolha um ovo para incubar no Ninho.", "body-copy");
            VisualElement grid = Styled(new VisualElement(), "product-grid");
            grid.Add(Product(0, "OVO COMUM", "Padrão quente e irregular. Uma surpresa acessível para começar.", HubRosterState.EggCost));
            grid.Add(Product(1, "OVO RARO", "Veios densos e brilho contido. Nasce com grau 2, sem revelar sua espécie.", HubRosterState.RareEggCost));
            body.Add(grid);
        }

        private VisualElement Product(int tier, string name, string copy, int cost)
        {
            VisualElement card = Styled(new VisualElement(), "product-card");
            card.Add(EggPreview(tier, "product-art"));
            Label(card, name, "product-name pixel-title");
            Label(card, copy, "product-copy");
            Button buy = ActionButton($"{cost} MOEDAS", () => Notify(roster.BuyEgg(tier), tier == 1 ? "Ovo Raro guardado na reserva." : "Ovo Comum guardado na reserva."), "primary-action");
            buy.SetEnabled(roster.Coins >= cost && roster.Eggs.Count < HubRosterState.MaxEggs);
            card.Add(buy); return card;
        }

        private void BuildFusion()
        {
            Label(body, "DUAS ORIGENS. UM NOVO GRAU.", "section-title pixel-title");
            Label(body, "Escolha Lumons da mesma afinidade. O material não pode estar na equipe e será consumido.", "body-copy");
            HubCreature target = roster.Creatures.FirstOrDefault(c => c.Id == fusionTarget && c.Rank < HubRosterState.MaxRank)
                ?? roster.Creatures.FirstOrDefault(c => c.Rank < HubRosterState.MaxRank);
            if (target == null)
            {
                Empty("Todos os Lumons já alcançaram o grau máximo.", 3);
                return;
            }
            fusionTarget = target.Id;
            HubCreature material = roster.Creatures.FirstOrDefault(c => c.Id == fusionMaterial && c.Id != fusionTarget && c.Element == target.Element && !roster.Team.Contains(c.Id))
                ?? roster.Creatures.FirstOrDefault(c => c.Id != fusionTarget && c.Element == target.Element && !roster.Team.Contains(c.Id));
            fusionMaterial = material?.Id ?? 0;
            Label(body, "LUMON QUE RECEBE O GRAU", "row-detail pixel-title");
            foreach (HubCreature c in roster.Creatures) body.Add(Choice(c, c.Id == fusionTarget, () => { fusionTarget = c.Id; fusionMaterial = 0; BuildPanel(); }));
            Label(body, "LUMON USADO COMO MATERIAL", "row-detail pixel-title");
            foreach (HubCreature c in roster.Creatures.Where(c => c.Id != fusionTarget && !roster.Team.Contains(c.Id)))
                body.Add(Choice(c, c.Id == fusionMaterial, () => { fusionMaterial = c.Id; BuildPanel(); }));
            Button fuse = ActionButton("FUSIONAR", () => { var result = roster.Fuse(fusionTarget, fusionMaterial); fusionMaterial = 0; Notify(result, "Fusão concluída."); });
            fuse.SetEnabled(fusionTarget > 0 && fusionMaterial > 0); body.Add(fuse);
        }

        private void BuildImprove()
        {
            Label(body, "TREINO DE APRIMORAMENTO", "section-title pixel-title");
            Label(body, $"Cada treino aumenta um nível e custa {HubRosterState.ImproveCost} moedas. Limite: nível {HubRosterState.MaxLevel}.", "body-copy");
            foreach (HubCreature c in roster.Creatures)
            {
                VisualElement row = Row(c.Element + 1, CreatureName(c), $"Nível {c.Level}  •  Grau {c.Rank}", CreaturePortrait(c));
                Button action = ActionButton(c.Level >= HubRosterState.MaxLevel ? "MÁXIMO" : "TREINAR", () => Notify(roster.Improve(c.Id), $"{CreatureName(c)} subiu de nível."), "row-action");
                action.SetEnabled(c.Level < HubRosterState.MaxLevel && roster.Coins >= HubRosterState.ImproveCost); row.Add(action); body.Add(row);
            }
        }

        private void BuildExpeditions()
        {
            Label(body, "MAPA DE EXPEDIÇÃO", "section-title pixel-title");
            Label(body, "Prepare a saída da equipe para o Vale da Nascente. Esta etapa registra a rota; o combate não foi alterado.", "body-copy");
            VisualElement row = Row(5, "VALE DA NASCENTE", preferences.ExpeditionPlanned ? "Rota marcada no mapa" : "Rota ainda não planejada");
            row.Add(ActionButton(preferences.ExpeditionPlanned ? "DESMARCAR" : "PLANEJAR", () => { preferences.ExpeditionPlanned = !preferences.ExpeditionPlanned; preferences.Save(); Toast(preferences.ExpeditionPlanned ? "Rota planejada." : "Rota desmarcada."); BuildPanel(); }, "row-action"));
            body.Add(row);
        }

        private void BuildScope()
        {
            bool west = current == LumonStationId.WestScope;
            int chart = west ? 1 : 2;
            Label(body, west ? "MIRANTE OESTE" : "MIRANTE LESTE", "section-title");
            body.Add(new ObservatoryChart(west));
            Label(body, west ? "Siga a estrela Vega até o pequeno paralelogramo da Lira." :
                "Deneb marca a cauda do Cisne. Siga o eixo mais longo até o bico.", "body-copy");
            bool recorded = (session.Charts & chart) != 0;
            var record = ActionButton(recorded ? "CARTA REGISTRADA" : "REGISTRAR NO ATLAS", () => session.Observe(chart));
            record.SetEnabled(!recorded); body.Add(record);
            Label(body, session.LocalSlot >= 0 ? "As observações entram no atlas de todos na sala." :
                "Visite os dois mirantes para completar o atlas desta visita.", "body-copy");
        }

        private void BuildParty()
        {
            if (session.LocalSlot < 0)
            {
                Label(body, "Reúna até seis guardiões no mesmo Observatório pela rede local.", "body-copy");
                if (session.Notice.Length > 0) Label(body, session.Notice, "party-status");
                if (session.Active) { body.Add(ActionButton("CANCELAR", () => session.Leave())); return; }
                body.Add(ActionButton("CRIAR SALA", session.Host));
                Label(body, "Para visitar uma sala, digite o IPv4 mostrado no jogo do anfitrião.", "body-copy");
                var field = Styled(new TextField { name = "partyAddress", value = savedAddress, maxLength = 15 }, "party-field");
                field.RegisterValueChangedCallback(e => savedAddress = e.newValue);
                body.Add(field);
                body.Add(ActionButton("ENTRAR NA SALA", () => session.Enter(field.value)));
                return;
            }
            Label(body, $"{session.Count} de 6 guardiões na praça", "section-title");
            Label(body, "Atlas da sala    ·    Lira " + ((session.Charts & 1) != 0 ? "registrada" : "por observar") +
                "    ·    Cisne " + ((session.Charts & 2) != 0 ? "registrado" : "por observar"), "body-copy");
            if (session.Hosting) Label(body, "Endereço para convidados   " + session.Address, "body-copy");
            for (int i = 0; i < HubLanSession.Capacity; i++)
            {
                if (!session.Occupied(i)) continue;
                string role = i == 0 ? "Anfitrião" : "Visitante";
                Label(body, HubLanSession.Callsigns[i] + (i == session.LocalSlot ? "  (você)" : "") + "    ·    " + role + "    ·    " + (session.Ready(i) ? "Pronto" : "Explorando"), "body-copy");
            }
            body.Add(ActionButton(session.Ready(session.LocalSlot) ? "VOLTAR A EXPLORAR" : "MARCAR PRONTO", session.ToggleReady));
            body.Add(ActionButton(session.Hosting ? "ENCERRAR SALA" : "SAIR DA SALA", () => session.Leave("Você está no modo solo."), "row-action"));
        }

        private void BuildSettings()
        {
            Label(body, "ACESSIBILIDADE E CÂMERA", "section-title pixel-title");
            VisualElement motion = Row(3, "MOVIMENTO REDUZIDO", preferences.ReducedMotion ? "Transições instantâneas e sem rotação automática" : "Rotação e animações de interface ativas");
            motion.Add(ActionButton(preferences.ReducedMotion ? "ATIVADO" : "DESATIVADO", () => { preferences.ReducedMotion = !preferences.ReducedMotion; preferences.Save(); world.ReducedMotion = preferences.ReducedMotion; BuildPanel(); }, "row-action")); body.Add(motion);
            VisualElement camera = Row(5, "ENQUADRAMENTO", "Retorna à vista inicial do diorama");
            camera.Add(ActionButton("RECENTRALIZAR", () => { view.GetComponent<SplatCameraRig>()?.ResetView(); Toast("Câmera recentralizada."); }, "row-action")); body.Add(camera);
            Label(body, "Gire o mundo com o botão direito ou com Q / R. A tecla E é exclusiva da interação com estações.", "body-copy");
        }

        private VisualElement Choice(HubCreature c, bool selected, Action choose)
        {
            Button button = Styled(new Button(choose), "pixel-row");
            button.EnableInClassList("is-selected", selected);
            button.Add(CreaturePortrait(c));
            VisualElement main = Styled(new VisualElement(), "row-main");
            Label(main, CreatureName(c), "row-title pixel-title"); Label(main, $"NÍVEL {c.Level}  •  GRAU {c.Rank}", "row-detail pixel-title"); button.Add(main);
            return button;
        }

        private VisualElement Row(int glyph, string title, string detail, VisualElement portrait = null)
        {
            VisualElement row = Styled(new VisualElement(), "pixel-row");
            row.Add(portrait ?? GlyphElement(glyph, "row-glyph"));
            VisualElement main = Styled(new VisualElement(), "row-main");
            Label(main, title, "row-title pixel-title"); Label(main, detail, "row-detail"); row.Add(main); return row;
        }

        private void Empty(string text, int glyph)
        {
            VisualElement empty = Styled(new VisualElement(), "empty-state");
            empty.Add(GlyphElement(glyph, "empty-icon")); Label(empty, text, "empty-text"); body.Add(empty);
        }

        private static T Styled<T>(T element, string className) where T : VisualElement
        {
            foreach (string name in className.Split(' ', StringSplitOptions.RemoveEmptyEntries)) element.AddToClassList(name);
            if (element is TextElement text)
            {
                string resource = element.ClassListContains("display-title") ? "Lumon/Fonts/SourceSerif4-Semibold" : "Lumon/Fonts/AtkinsonHyperlegible-Regular";
                UnityEngine.TextCore.Text.FontAsset font = Resources.Load<UnityEngine.TextCore.Text.FontAsset>(resource);
                if (font != null) text.style.unityFontDefinition = new FontDefinition { fontAsset = font };
            }
            return element;
        }

        private static LumonPixelGlyph GlyphElement(int kind, string className) => Styled(new LumonPixelGlyph { Kind = kind }, className);

        private static VisualElement CreaturePortrait(HubCreature creature)
        {
            string name = CreatureSpecies(creature);
            Texture2D art = Resources.Load<Texture2D>($"Lumon/Portraits/portrait-{name}");
            if (art == null) return GlyphElement(creature.Element + 1, "row-glyph");
            return Styled(new Image { image = art, scaleMode = ScaleMode.ScaleAndCrop, pickingMode = PickingMode.Ignore }, "row-glyph creature-portrait");
        }
        private static void Label(VisualElement parent, string text, string className) => parent.Add(Styled(new Label(text), className));
        private static Button ActionButton(string text, Action action, string className = "primary-action") => Styled(new Button(action) { text = text }, className);

        private void Notify(HubWorkshopResult result, string success)
        {
            Toast(result == HubWorkshopResult.Success ? success : ResultText(result));
            RefreshCoins(); BuildPanel();
        }

        public string QaState()
        {
            if (roster == null) return "state=loading";
            HubEgg active = roster.Eggs.FirstOrDefault(e => e.ReadyAtTicks > 0);
            return $"panel={current} coins={roster.Coins} creatures={roster.Creatures.Count} eggs={roster.Eggs.Count} incubating={active?.Id ?? 0} ready={(active != null && DateTime.UtcNow.Ticks >= active.ReadyAtTicks)}";
        }

        private Button FindQaButton(string label) =>
            root?.Query<Button>().ToList().FirstOrDefault(b => b.enabledInHierarchy &&
                b.resolvedStyle.display != DisplayStyle.None &&
                (string.Equals(b.text, label, StringComparison.OrdinalIgnoreCase) || b.name == label));

        public bool QaFocus(string label)
        {
            Button button = FindQaButton(label);
            if (button == null) return false;
            button.Focus();
            return true;
        }

        public bool QaActivate(string label)
        {
            Button button = FindQaButton(label);
            if (button == null) return false;
            button.Focus();
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            return true;
        }

        private void RefreshCoins() { if (root != null && roster != null) root.Q<Label>("coinLabel").text = roster.Coins.ToString(); }

        private void Toast(string text)
        {
            toastTimer?.Pause();
            root.Q<Label>("toastLabel").text = text;
            root.Q("toast").RemoveFromClassList("is-hidden");
            toastTimer = root.schedule.Execute(() => root.Q("toast").AddToClassList("is-hidden")).StartingIn(3200);
        }

        private static string ResultText(HubWorkshopResult result) => result switch
        {
            HubWorkshopResult.InsufficientCoins => "Moedas insuficientes.",
            HubWorkshopResult.CapacityReached => "A capacidade máxima foi atingida.",
            HubWorkshopResult.IncubatorBusy => "A incubadora já está ocupada.",
            HubWorkshopResult.EggNotReady => "Este ovo ainda não está pronto.",
            HubWorkshopResult.LevelLimit => "Este Lumon já atingiu o nível máximo.",
            HubWorkshopResult.RankLimit => "Este Lumon já atingiu o grau máximo.",
            HubWorkshopResult.MaterialInTeam => "Retire o Lumon da equipe antes de usá-lo como material.",
            _ => "Selecione Lumons válidos para esta ação.",
        };

        private static string CreatureName(HubCreature c) => c.Element switch { 0 => $"BRASA #{c.Id}", 1 => $"GOTA #{c.Id}", _ => $"BROTO #{c.Id}" };
        private static string CreatureSpecies(HubCreature c) => c.Element switch { 0 => "brasa", 1 => "gota", _ => "broto" };
        private static int Glyph(LumonStationId id) => id switch { LumonStationId.Team => 0, LumonStationId.Nest => 1, LumonStationId.EggShop => 2, LumonStationId.Fusion => 3, LumonStationId.Improve => 4, LumonStationId.Expeditions => 5, LumonStationId.WestScope or LumonStationId.EastScope => 10, LumonStationId.Party => 0, _ => 6 };
        private static string Title(LumonStationId id) => id switch { LumonStationId.Team => "Equipe", LumonStationId.Nest => "Ninho", LumonStationId.EggShop => "Banca de Ovos", LumonStationId.Fusion => "Fusão", LumonStationId.Improve => "Aprimoramento", LumonStationId.Expeditions => "Expedições", LumonStationId.Party => "Grupo", LumonStationId.WestScope => "Lira", LumonStationId.EastScope => "Cisne", _ => "Ajustes" };
    }
}
