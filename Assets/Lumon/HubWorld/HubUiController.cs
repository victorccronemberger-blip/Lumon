using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class HubUiController : MonoBehaviour
    {
        internal static string PreferencesKey { get; set; } = HubPreferences.DefaultKeyPrefix;

        private const string Hidden = "is-hidden";
        private const string Selected = "is-selected";
        private enum Panel { Plaza, Team, Atlas, Settings }

        [SerializeField] private Camera hubCamera;
        [SerializeField] private HubStations stations;
        [SerializeField] private Texture2D inscriptionAtlas;
        [SerializeField] private TextAsset inscriptionMetrics;
        [SerializeField] private Texture2D fieldBookAtlas;
        [SerializeField] private TextAsset fieldBookMetrics;
        [SerializeField] private Texture2D teamFabric;
        private VisualElement root;
        private VisualElement canvas;
        private readonly System.Collections.Generic.Dictionary<string, HubInscription> inscriptions = new();
        private HubPreferences saved;
        private HubPreferences draft;
        private HubRosterState roster;
        private HubTeamWorkshop workshop;
        private readonly HubMenuCinematics menuCinematics = new();
        private HubLayerMotion[] ambientMotion;
        private Panel current;
        private HubStation nearbyStation;
        private Button returnFocus;
        private IVisualElementScheduledItem toastTimer;
        private readonly System.Collections.Generic.Dictionary<Button, System.Action> buttons = new();
        private HubClothPatch teamCloth;
        private HubInscription teamClothInk;
        private bool reducedMotion;
        private Rect initialCameraRect;
        private bool cameraRectChanged;

        public bool IsModalOpen => current != Panel.Plaza;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            canvas = root.Q("hubCanvas");
            if (canvas == null) return;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            current = Panel.Plaza;
            saved = HubPreferences.Load(PreferencesKey);
            draft = Copy(saved);
            roster = HubRosterState.Load(PreferencesKey, saved.TeamMask);
            workshop = new HubTeamWorkshop(root, roster, T, () => workshop.RefreshPlaza(), () => reducedMotion);
            ambientMotion = FindObjectsByType<HubLayerMotion>(FindObjectsSortMode.None);
            if (hubCamera == null) hubCamera = Camera.main;
            if (hubCamera != null) initialCameraRect = hubCamera.rect;

            Bind("openTeam", () => stations.RequestStation(HubStation.Team));
            Bind("openAtlas", () => stations.RequestStation(HubStation.Expeditions));
            Bind("openSettings", () => stations.RequestStation(HubStation.Settings));
            Bind("teamBack", Close);
            Bind("teamSave", Save);
            Bind("atlasBack", Close);
            Bind("planRoute", ToggleRoute);
            Bind("settingsBack", Close);
            Bind("settingsSave", Save);
            Bind("langPt", () => PreviewLanguage("pt-BR"));
            Bind("langEn", () => PreviewLanguage("en"));
            Bind("langEs", () => PreviewLanguage("es"));
            Bind("volumeDown", () => PreviewVolume(-0.1f));
            Bind("volumeUp", () => PreviewVolume(0.1f));
            Bind("motionToggle", ToggleMotion);
            root.RegisterCallback<GeometryChangedEvent>(FitCanvas);
            stations.OnStationRequested += OpenStation;
            stations.OnNearbyStationChanged += SetStationPrompt;
            stations.PointerOverInterface = IsPointerOverControl;
            nearbyStation = stations.NearbyStation;
            AttachInscriptions();
            ApplyPreferences(saved);
            Refresh();
        }

        private void OnDisable()
        {
            toastTimer?.Pause();
            menuCinematics.Reset();
            workshop?.Dispose();
            workshop = null;
            foreach (HubInscription inscription in inscriptions.Values) inscription.RemoveFromHierarchy();
            inscriptions.Clear();
            teamCloth?.RemoveFromHierarchy();
            teamCloth = null;
            teamClothInk = null;
            foreach (var binding in buttons) binding.Key.clicked -= binding.Value;
            buttons.Clear();
            if (stations != null)
            {
                stations.OnStationRequested -= OpenStation;
                stations.OnNearbyStationChanged -= SetStationPrompt;
                stations.PointerOverInterface = null;
                stations.SetWorldInputPaused(this, false);
            }
            if (root != null) root.UnregisterCallback<GeometryChangedEvent>(FitCanvas);
            if (cameraRectChanged && hubCamera != null) hubCamera.rect = initialCameraRect;
            cameraRectChanged = false;
        }

        private void Update()
        {
            if (teamCloth != null && teamClothInk != null)
            {
                int frame = reducedMotion ? 0 : Mathf.FloorToInt(Time.timeSinceLevelLoad * 8f) % HubClothPatch.Frames;
                teamCloth.SetFrame(frame);
                teamClothInk.SetClothFrame(frame);
            }
            menuCinematics.Tick();
            if (current == Panel.Team) workshop?.Tick();
            if (canvas == null || !Input.GetKeyDown(KeyCode.Escape)) return;
            if (current == Panel.Team && workshop.DismissHatch(restoreEggFocus: true)) return;
            if (IsModalOpen) Close();
            else Open(Panel.Settings, "openSettings");
        }

        private void OpenStation(HubStation station)
        {
            if (canvas == null) return;
            switch (station)
            {
                case HubStation.Team: Open(Panel.Team, "openTeam"); break;
                case HubStation.Expeditions: Open(Panel.Atlas, "openAtlas"); break;
                case HubStation.Settings: Open(Panel.Settings, "openSettings"); break;
            }
        }

        private bool IsPointerOverControl(Vector2 screenPosition)
        {
            if (root?.panel == null) return false;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return root.panel.Pick(point) is Button;
        }

        private void SetStationPrompt(HubStation stationId)
        {
            nearbyStation = stationId;
            if (canvas != null) RefreshPrompt();
        }



        private void FitCanvas(GeometryChangedEvent evt)
        {
            float width = evt.newRect.width;
            float height = evt.newRect.height;
            if (width <= 0 || height <= 0) return;

            const float aspect = 16f / 9f;
            float canvasWidth = Mathf.Min(width, height * aspect);
            float canvasHeight = canvasWidth / aspect;
            float left = (width - canvasWidth) * 0.5f;
            float top = (height - canvasHeight) * 0.5f;
            canvas.style.left = left;
            canvas.style.top = top;
            canvas.style.right = left;
            canvas.style.bottom = top;

            if (hubCamera != null)
            {
                hubCamera.rect = new Rect(left / width, top / height, canvasWidth / width, canvasHeight / height);
                cameraRectChanged = true;
            }
        }

        private void Open(Panel target, string source)
        {
            if (current != Panel.Plaza) return;
            draft = Copy(saved);
            current = target;
            stations.SetWorldInputPaused(this, true);
            returnFocus = root.Q<Button>(source);
            Visible("modalHost", true);
            Visible("teamPanel", target == Panel.Team);
            Visible("atlasPanel", target == Panel.Atlas);
            Visible("settingsPanel", target == Panel.Settings);
            string panelName = target == Panel.Team ? "teamPanel" : target == Panel.Atlas ? "atlasPanel" : "settingsPanel";
            menuCinematics.Open(root.Q(panelName), root.Q("modalLighting"), reducedMotion);
            root.Q("plazaHud").SetEnabled(false);
            PresentToast(null);
            if (target == Panel.Team) workshop.Open();
            Refresh();
            string firstControl = target == Panel.Team
                ? (root.Q("slotFire").ClassListContains(Hidden) ? "tabTeam" : "slotFire")
                : target == Panel.Atlas ? "planRoute" : "langPt";
            Button first = root.Q<Button>(firstControl);
            first.schedule.Execute(first.Focus);
        }

        private void Close()
        {
            if (!IsModalOpen) return;
            current = Panel.Plaza;
            menuCinematics.Reset();
            workshop.DismissHatch(restoreEggFocus: false);
            draft = Copy(saved);
            ApplyPreferences(saved);
            Visible("modalHost", false);
            Visible("teamPanel", false);
            Visible("atlasPanel", false);
            Visible("settingsPanel", false);
            root.Q("plazaHud").SetEnabled(true);
            stations.SetWorldInputPaused(this, false);
            Refresh();
            if (returnFocus != null) returnFocus.schedule.Execute(returnFocus.Focus);
        }

        private void Save()
        {
            if (current == Panel.Team)
            {
                if (!workshop.SaveTeam()) return;
                // Mirror the three legacy starter flags for profiles created before the collection.
                int legacy = roster.LegacyTeamMask;
                if (legacy != 0) draft.TeamMask = legacy;
            }
            saved = Copy(draft);
            saved.Save(PreferencesKey);
            string confirmation = current == Panel.Team
                ? T("Equipe reunida.", "Party assembled.", "Equipo reunido.")
                : T("Preferências salvas.", "Settings saved.", "Preferencias guardadas.");
            Close();
            PresentToast(confirmation);
        }

        private void ToggleRoute()
        {
            draft.ExpeditionPlanned = !saved.ExpeditionPlanned;
            saved.ExpeditionPlanned = draft.ExpeditionPlanned;
            saved.Save(PreferencesKey);
            Refresh();
        }

        private void PreviewLanguage(string code)
        {
            draft.Language = code;
            Refresh();
        }

        private void PreviewVolume(float delta)
        {
            draft.MasterVolume = Mathf.Clamp01(draft.MasterVolume + delta);
            ApplyPreferences(draft);
            Refresh();
        }

        private void ToggleMotion()
        {
            draft.ReducedMotion = !draft.ReducedMotion;
            ApplyPreferences(draft);
            Refresh();
        }

        private void ApplyPreferences(HubPreferences options)
        {
            AudioListener.volume = options.MasterVolume;
            reducedMotion = options.ReducedMotion;
            if (reducedMotion) menuCinematics.Reset();
            foreach (HubLayerMotion layer in ambientMotion)
            {
                if (layer != null && layer.Motion != HubMotion.Static)
                    layer.enabled = !options.ReducedMotion;
            }
        }

        private void Refresh()
        {
            string valley = T("Vale da Nascente", "Wellspring Valley", "Valle del Manantial");
            string back = T("Voltar", "Back", "Volver");
            string save = T("Salvar", "Save", "Guardar");
            Set("location", T("Observatório da Aurora", "Aurora Observatory", "Observatorio de la Aurora"));
            root.EnableInClassList("is-spanish", draft.Language == "es");
            Set("subtitle", T("Refúgio dos guardiões", "Guardians’ refuge", "Refugio de los guardianes"));
            root.Q<Button>("openSettings").tooltip = T("Configurações (Esc)", "Settings (Esc)", "Ajustes (Esc)");
            Set("teamTitle", T("Sua companhia", "Your party", "Tu compañía"));
            workshop.RefreshPlaza();
            Set("openTeam", T("Reunir equipe", "Assemble party", "Reunir equipo"));
            Set("bookTitle", T("Vale da\nNascente", "Wellspring\nValley", "Valle del\nManantial"));
            Set("bookSubtitle", saved.ExpeditionPlanned
                ? T("Rota marcada no atlas", "Route marked in the atlas", "Ruta marcada en el atlas")
                : T("Preparar a próxima jornada", "Prepare the next journey", "Prepara la próxima aventura"));
            Set("openAtlas", T("Traçar expedição", "Chart an expedition", "Trazar expedición"));
            Set("deskLabel", T("Expedições", "Expeditions", "Expediciones"));
            Set("deskAction", T("Abrir atlas", "Open atlas", "Abrir atlas"));
            Set("boothLabel", T("Equipe", "Party", "Equipo"));
            RefreshPrompt();
            Set("atlasHeading", valley);
            Set("atlasIntro", T("Siga o rio que desce das ilhas até a nascente do vale.", "Follow the river down from the islands to the valley spring.", "Sigue el río desde las islas hasta el manantial del valle."));
            Set("routeState", saved.ExpeditionPlanned
                ? T("Rota marcada", "Route marked", "Ruta marcada")
                : T("Rota disponível", "Route available", "Ruta disponible"));
            root.Q("routeState").EnableInClassList(Selected, saved.ExpeditionPlanned);
            Set("atlasBack", back);
            Set("planRoute", saved.ExpeditionPlanned
                ? T("Remover rota", "Clear route", "Quitar ruta")
                : T("Marcar rota", "Mark route", "Marcar ruta"));
            Set("settingsHeading", T("Configurações", "Settings", "Ajustes"));
            Set("languageLabel", T("Idioma", "Language", "Idioma"));
            Set("volumeLabel", T("Volume geral", "Master volume", "Volumen general"));
            Set("volumeValue", $"{Mathf.RoundToInt(draft.MasterVolume * 100f)}%");
            Set("motionLabel", T("Reduzir movimento", "Reduce motion", "Reducir movimiento"));
            Set("motionToggle", draft.ReducedMotion ? T("Sim", "On", "Sí") : T("Não", "Off", "No"));
            root.Q("motionToggle").EnableInClassList(Selected, draft.ReducedMotion);
            root.Q("langPt").EnableInClassList(Selected, draft.Language == "pt-BR");
            root.Q("langEn").EnableInClassList(Selected, draft.Language == "en");
            root.Q("langEs").EnableInClassList(Selected, draft.Language == "es");
            root.Q("volumeDown").SetEnabled(draft.MasterVolume > 0f);
            root.Q("volumeUp").SetEnabled(draft.MasterVolume < 1f);
            Set("settingsBack", back);
            Set("settingsSave", save);
        }

        private void RefreshPrompt()
        {
            string prompt = nearbyStation switch
            {
                HubStation.Team => T("E · Abrir equipe", "E · Open party", "E · Abrir equipo"),
                HubStation.Expeditions => T("F · Abrir atlas", "F · Open atlas", "F · Abrir atlas"),
                HubStation.Settings => T("F · Configurações", "F · Settings", "F · Ajustes"),
                _ => T("WASD / setas · Mover", "WASD / arrows · Move", "WASD / flechas · Mover"),
            };
            Set("controls", prompt);
        }

        private void PresentToast(string content)
        {
            toastTimer?.Pause();
            Visible("toast", !string.IsNullOrEmpty(content));
            if (string.IsNullOrEmpty(content)) return;
            Set("toast", content);
            toastTimer = root.schedule.Execute(() => Visible("toast", false)).StartingIn(2800);
        }

        private string T(string pt, string en, string es)
        {
            return draft.Language == "en" ? en : draft.Language == "es" ? es : pt;
        }

        private void Bind(string name, System.Action action)
        {
            var button = root.Q<Button>(name);
            button.clicked += action;
            buttons.Add(button, action);
        }
        private void AttachInscriptions()
        {
            if (inscriptionAtlas == null || inscriptionMetrics == null) return;
            var sheet = JsonUtility.FromJson<HubInscription.Sheet>(inscriptionMetrics.text);
            var plaza = root.Q("plazaHud");
            if (teamFabric != null)
            {
                teamCloth = new HubClothPatch(teamFabric);
                plaza.Insert(0, teamCloth);
            }

            Add("deskLabel", 25f, new Vector2[] { new(927, 271), new(1045, 287), new(1038, 309), new(918, 293) });
            teamClothInk = Add("boothLabel", 23f, new Vector2[] { new(184, 337), new(253, 325), new(262, 359), new(192, 372) },
                folded: teamCloth != null);
            var bookCorners = new Vector2[] { new(1023, 493), new(1208, 475), new(1218, 561), new(1030, 579) };
            if (fieldBookAtlas != null && fieldBookMetrics != null)
            {
                var book = JsonUtility.FromJson<HubInscription.Sheet>(fieldBookMetrics.text);
                var title = new HubInscription(fieldBookAtlas, book, bookCorners, 48f, leftAligned: true, fieldBook: true);
                Mount("bookTitle", title);
                var north = new HubInscription(fieldBookAtlas, book,
                    new Vector2[] { new(1191, 466), new(1203, 465), new(1204, 477), new(1192, 478) },
                    11f, fieldBook: true);
                north.SetText("N");
                inscriptions.Add("bookNorth", north);
                plaza.Add(north);
            }
            else
                Add("bookTitle", 38f, bookCorners, true);

            HubInscription Add(string name, float size, Vector2[] corners, bool leftAligned = false, bool folded = false)
            {
                var inscription = new HubInscription(inscriptionAtlas, sheet, corners, size, leftAligned, folded: folded);
                Mount(name, inscription);
                return inscription;
            }

            void Mount(string name, HubInscription inscription)
            {
                inscriptions.Add(name, inscription);
                root.Q(name).AddToClassList("hub-inscription-source");
                plaza.Add(inscription);
            }
        }

        private void Set(string name, string text)
        {
            root.Q<TextElement>(name).text = text;
            if (inscriptions.TryGetValue(name, out HubInscription inscription)) inscription.SetText(text);
        }
        private void Visible(string name, bool visible) => root.Q(name).EnableInClassList(Hidden, !visible);

        private static HubPreferences Copy(HubPreferences options)
        {
            return new HubPreferences
            {
                Language = options.Language,
                TeamMask = options.TeamMask,
                ExpeditionPlanned = options.ExpeditionPlanned,
                MasterVolume = options.MasterVolume,
                ReducedMotion = options.ReducedMotion,
            };
        }
    }
}
