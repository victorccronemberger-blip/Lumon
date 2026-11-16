using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    internal sealed class HubTeamWorkshop : IDisposable
    {
        private const string Selected = "is-selected";
        private enum Page { Team, Fusion, Improve, Eggs }
        private static readonly string[] Tabs = { "tabTeam", "tabFusion", "tabImprove", "tabEggs" };
        private static readonly string[] Panes = { "teamRosterPane", "fusionPane", "improvePane", "eggPane" };
        private static readonly string[] StarterCards = { "slotFire", "slotWater", "slotLeaf" };
        private static readonly string[] Buyers = { "buyFire", "buyWater", "buyLeaf" };
        private static readonly string[] ShellNames = { "fire", "water", "leaf" };
        private static readonly string[] ElementStyles = { "hub-lumon-fire", "hub-lumon-water", "hub-lumon-leaf" };

        private readonly VisualElement root;
        private readonly HubRosterState roster;
        private readonly Func<string, string, string, string> text;
        private readonly Action balanceChanged;
        private readonly Func<bool> reducedMotion;
        private readonly List<int> draft = new();
        private readonly List<(HubEgg egg, Button action, Label status)> eggRows = new();
        private readonly Dictionary<Button, Action> bindings = new();

        private Page page;
        private int target, material, improve;
        private bool confirmingFusion;
        private string message;
        private long lastEggSecond = -1;
        private float hatchStartedAt;
        private bool hatchRevealed;

        internal HubTeamWorkshop(VisualElement root, HubRosterState roster,
            Func<string, string, string, string> text, Action balanceChanged, Func<bool> reducedMotion)
        {
            this.root = root;
            this.roster = roster;
            this.text = text;
            this.balanceChanged = balanceChanged;
            this.reducedMotion = reducedMotion;
            for (int i = 0; i < Tabs.Length; i++)
            {
                Page index = (Page)i;
                Bind(Tabs[i], () => Show(index));
            }
            for (int i = 0; i < StarterCards.Length; i++)
            {
                int element = i;
                Bind(StarterCards[i], () => ToggleMember(element + 1));
                Bind(Buyers[i], () => Apply(roster.BuyEgg(element), T("Ovo comprado. Coloque-o na incubadora.",
                    "Egg purchased. Place it in the incubator.", "Huevo comprado. Ponlo en la incubadora.")));
            }
            Bind("fusionBack", ReturnToTeam);
            Bind("improveBack", ReturnToTeam);
            Bind("eggBack", ReturnToTeam);
            Bind("fusionAction", Fuse);
            Bind("hatchContinue", () =>
            {
                DismissHatch(restoreEggFocus: false);
                ReturnToTeam();
            });
            Bind("improveAction", () => Apply(roster.Improve(improve), T("Aprimoramento concluído.", "Upgrade complete.", "Mejora completada.")));
        }

        public void Dispose()
        {
            foreach (var binding in bindings) binding.Key.clicked -= binding.Value;
        }

        internal void Open()
        {
            DismissHatch(restoreEggFocus: false);
            draft.Clear();
            draft.AddRange(roster.Team);
            target = material = improve = 0;
            Show(Page.Team);
        }

        internal bool SaveTeam()
        {
            HubWorkshopResult result = roster.SetTeam(draft);
            if (result == HubWorkshopResult.Success) return true;
            Set("teamState", ResultText(result));
            return false;
        }

        internal string CreatureName(HubCreature creature)
        {
            string name = Name(creature.Element);
            return creature.Id <= 3 ? name : $"{name} #{creature.Id}";
        }

        internal void RefreshPlaza()
        {
            Set("hubCoins", roster.Coins.ToString(CultureInfo.InvariantCulture));
            string[] labels = { "rosterFire", "rosterWater", "rosterLeaf" };
            for (int i = 0; i < labels.Length; i++)
            {
                HubCreature creature = i < roster.Team.Count ? Find(roster.Team[i]) : null;
                Set(labels[i], creature == null ? T("Vaga livre", "Empty slot", "Hueco libre") : Name(creature.Element));
                root.Q(labels[i]).EnableInClassList("is-resting", creature == null);
            }
        }

        internal void Tick()
        {
            if (hatchRevealed) UpdateHatch();
            if (page != Page.Eggs || eggRows.Count == 0) return;
            long now = DateTime.UtcNow.Ticks;
            long second = now / TimeSpan.TicksPerSecond;
            if (lastEggSecond == second) return;
            lastEggSecond = second;
            bool busy = roster.Eggs.Any(egg => egg.ReadyAtTicks != 0);
            foreach (var row in eggRows)
            {
                long readyAt = row.egg.ReadyAtTicks;
                bool started = readyAt > 0;
                bool ready = started && readyAt <= now;
                int remaining = started && !ready ? (int)Math.Ceiling((readyAt - now) / (double)TimeSpan.TicksPerSecond) : 0;
                string species = Name(row.egg.Element);
                row.action.text = ready ? T($"Chocar · {species}", $"Hatch · {species}", $"Eclosionar · {species}")
                    : started ? T($"{species} · Incubando", $"{species} · Incubating", $"{species} · En incubación")
                    : T($"Incubar · {species}", $"Incubate · {species}", $"Incubar · {species}");
                row.action.SetEnabled((ready && roster.Creatures.Count < HubRosterState.MaxCreatures) || (!started && !busy));
                row.status.text = ready ? (roster.Creatures.Count < HubRosterState.MaxCreatures
                        ? T("Pronto para chocar", "Ready to hatch", "Listo para eclosionar")
                        : T("Coleção cheia", "Collection full", "Colección llena"))
                    : started ? T($"Pronto em {remaining}s", $"Ready in {remaining}s", $"Listo en {remaining}s")
                    : busy ? T("Aguardando incubadora", "Waiting for incubator", "En espera de incubadora")
                    : T("Aguardando incubação", "Ready to incubate", "Listo para incubar");
            }
        }

        private void Show(Page value)
        {
            page = value;
            message = null;
            confirmingFusion = false;
            Refresh();
        }

        private void ReturnToTeam()
        {
            Show(Page.Team);
            var team = root.Q<Button>("tabTeam");
            team.schedule.Execute(team.Focus);
        }

        private void Refresh()
        {
            Set("teamHeading", T("Monte sua companhia", "Assemble your party", "Forma tu compañía"));
            Set("workshopBalance", Coins(roster.Coins));
            Set("tabTeam", T("Equipe", "Party", "Equipo"));
            Set("tabFusion", T("Fusão", "Fusion", "Fusión"));
            Set("tabImprove", T("Aprimorar", "Upgrade", "Mejorar"));
            Set("tabEggs", T("Ovos", "Eggs", "Huevos"));
            for (int i = 0; i < Tabs.Length; i++)
            {
                root.Q(Tabs[i]).EnableInClassList(Selected, i == (int)page);
                root.Q(Panes[i]).EnableInClassList("is-hidden", i != (int)page);
            }

            switch (page)
            {
                case Page.Team: RenderTeam(); break;
                case Page.Fusion: RenderFusion(); break;
                case Page.Improve: RenderImprove(); break;
                case Page.Eggs: RenderEggs(); break;
            }
        }

        private void RenderTeam()
        {
            Set("teamIntro", T("Escolha até três Lumons para a próxima jornada.",
                "Choose up to three Lumons for your next journey.", "Elige hasta tres Lumons para tu próxima aventura."));
            Set("teamState", message ?? T($"{draft.Count} de 3 na equipe", $"{draft.Count} of 3 in the party", $"{draft.Count} de 3 en el equipo"));
            Set("teamBack", T("Voltar", "Back", "Volver"));
            Set("teamSave", T("Salvar equipe", "Save party", "Guardar equipo"));
            for (int i = 0; i < StarterCards.Length; i++)
            {
                var creature = Find(i + 1);
                string card = StarterCards[i];
                root.Q(card).EnableInClassList("is-hidden", creature == null);
                if (creature == null) continue;
                bool chosen = draft.Contains(creature.Id);
                Set(card + "Name", CreatureName(creature));
                Set(card + "State", (chosen ? T("Na equipe", "In party", "En el equipo") : T("Em descanso", "Resting", "En descanso"))
                    + " · " + T($"Nv. {creature.Level}", $"Lv. {creature.Level}", $"Nv. {creature.Level}"));
                root.Q(card).EnableInClassList(Selected, chosen);
            }

            var list = root.Q<ScrollView>("extraRoster");
            var added = roster.Creatures.Where(creature => creature.Id > 3).ToArray();
            Fill(list, added, "extra", draft.Contains, ToggleMember);
            list.EnableInClassList("is-hidden", added.Length == 0);
        }

        private void ToggleMember(int id)
        {
            if (Find(id) == null) return;
            message = null;
            if (draft.Contains(id))
            {
                if (draft.Count == 1)
                    message = T("Mantenha pelo menos um Lumon na equipe.", "Keep at least one Lumon in the party.",
                        "Mantén al menos un Lumon en el equipo.");
                else
                    draft.Remove(id);
            }
            else if (draft.Count == HubRosterState.MaxTeamSize)
                message = T("Equipe cheia. Libere uma vaga primeiro.", "Party full. Free a slot first.", "Equipo completo. Libera un hueco.");
            else
                draft.Add(id);
            RenderTeam();
        }

        private void RenderFusion()
        {
            Set("fusionIntro", T("Escolha dois Lumons do mesmo tipo. O material precisa estar em descanso.",
                "Choose two Lumons of the same type. The material must be resting.",
                "Elige dos Lumons del mismo tipo. El material debe estar en descanso."));
            Set("fusionTargetLabel", T("Lumon principal", "Main Lumon", "Lumon principal"));
            Set("fusionMaterialLabel", T("Material", "Material", "Material"));
            Set("fusionBack", T("Voltar", "Back", "Volver"));
            var primary = Find(target);
            var spare = Find(material);
            Fill(root.Q<ScrollView>("fusionTargets"), roster.Creatures, "fuse-target", id => id == target, id =>
            {
                target = id;
                material = 0;
                confirmingFusion = false;
                message = null;
                RenderFusion();
            });
            var sources = primary == null ? Array.Empty<HubCreature>() : roster.Creatures
                .Where(item => item.Id != target && item.Element == primary.Element && !roster.Team.Contains(item.Id)
                    && !draft.Contains(item.Id)).ToArray();
            var list = root.Q<ScrollView>("fusionMaterials");
            Fill(list, sources, "fuse-material", id => id == material, id =>
            {
                material = id;
                confirmingFusion = false;
                message = null;
                RenderFusion();
            });
            if (sources.Length == 0)
                Empty(list, primary == null
                    ? T("Selecione o Lumon principal.", "Select the main Lumon.", "Elige el Lumon principal.")
                    : T("Nenhum do mesmo tipo em descanso. Choque um ovo do mesmo tipo ou salve uma equipe diferente.",
                        "No resting match. Hatch the same type or save a different party.",
                        "No hay otro de ese tipo en descanso. Eclosiona un huevo del mismo tipo o guarda otro equipo."));
            string price = Coins(HubRosterState.FusionCost);
            Set("fusionAction", confirmingFusion
                ? T("Confirmar fusão", "Confirm fusion", "Confirmar fusión")
                : T($"Fundir · {price}", $"Fuse · {price}", $"Fusionar · {price}"));
            root.Q<Button>("fusionAction").SetEnabled(primary != null && primary.Rank < HubRosterState.MaxRank &&
                sources.Any(item => item.Id == material) && roster.Coins >= HubRosterState.FusionCost);
            string info = T("A fusão aumenta o grau do Lumon principal.", "Fusion raises the main Lumon's rank.",
                "La fusión aumenta el grado del Lumon principal.");
            if (primary != null)
                info = primary.Rank >= HubRosterState.MaxRank ? ResultText(HubWorkshopResult.RankLimit)
                    : T($"Grau {primary.Rank} → {primary.Rank + 1}", $"Rank {primary.Rank} → {primary.Rank + 1}",
                        $"Grado {primary.Rank} → {primary.Rank + 1}");
            if (roster.Coins < HubRosterState.FusionCost) info = ResultText(HubWorkshopResult.InsufficientCoins);
            if (confirmingFusion && spare != null)
                info = T($"Consumir {CreatureName(spare)} e pagar {price}?",
                    $"Consume {CreatureName(spare)} and pay {price}?", $"¿Consumir {CreatureName(spare)} y pagar {price}?");
            Set("fusionState", message ?? info);
        }

        private void Fuse()
        {
            if (!confirmingFusion)
            {
                confirmingFusion = true;
                message = null;
                RenderFusion();
                return;
            }

            HubWorkshopResult result = roster.Fuse(target, material);
            confirmingFusion = false;
            if (result == HubWorkshopResult.Success) material = 0;
            Apply(result, T("Fusão concluída. O Lumon principal ganhou um grau.",
                "Fusion complete. The main Lumon gained a rank.", "Fusión completada. El Lumon principal ganó un grado."));
        }

        private void RenderImprove()
        {
            Set("improveIntro", T("Escolha um Lumon para subir um nível.",
                "Choose a Lumon to raise its level.", "Elige un Lumon para subir un nivel."));
            Set("improveBack", T("Voltar", "Back", "Volver"));
            var creature = Find(improve);
            Fill(root.Q<ScrollView>("improveRoster"), roster.Creatures, "improve", id => id == improve, id =>
            {
                improve = id;
                message = null;
                RenderImprove();
            });
            string price = Coins(HubRosterState.ImproveCost);
            Set("improveAction", T($"Aprimorar · {price}", $"Upgrade · {price}", $"Mejorar · {price}"));
            root.Q<Button>("improveAction").SetEnabled(creature != null && creature.Level < HubRosterState.MaxLevel && roster.Coins >= HubRosterState.ImproveCost);
            string info = creature == null
                ? T("Selecione um Lumon.", "Select a Lumon.", "Elige un Lumon.")
                : creature.Level >= HubRosterState.MaxLevel ? ResultText(HubWorkshopResult.LevelLimit)
                : T($"Nível {creature.Level} → {creature.Level + 1}", $"Level {creature.Level} → {creature.Level + 1}",
                    $"Nivel {creature.Level} → {creature.Level + 1}");
            if (roster.Coins < HubRosterState.ImproveCost) info = ResultText(HubWorkshopResult.InsufficientCoins);
            Set("improveState", message ?? info);
        }

        private void RenderEggs()
        {
            Set("eggIntro", T($"Incube um ovo por vez. Cada ovo leva {HubRosterState.IncubationSeconds}s para chocar.",
                $"Incubate one egg at a time. Each egg is ready in {HubRosterState.IncubationSeconds}s.",
                $"Incuba un huevo a la vez. Cada huevo tarda {HubRosterState.IncubationSeconds}s en eclosionar."));
            Set("shopLabel", T("Comprar ovo", "Buy an egg", "Comprar huevo"));
            Set("incubatorLabel", T("Incubadora", "Incubator", "Incubadora"));
            Set("eggBack", T("Voltar", "Back", "Volver"));
            string price = Coins(HubRosterState.EggCost);
            for (int i = 0; i < Buyers.Length; i++)
            {
                string name = Name(i);
                Set(Buyers[i], T($"Ovo de {name} · {price}", $"{name} egg · {price}", $"Huevo de {name} · {price}"));
                root.Q<Button>(Buyers[i]).SetEnabled(roster.Coins >= HubRosterState.EggCost && roster.Eggs.Count < HubRosterState.MaxEggs);
            }
            var list = root.Q<ScrollView>("eggList");
            string focus = SelectedFocus(list);
            list.Clear();
            eggRows.Clear();
            foreach (HubEgg egg in roster.Eggs)
            {
                var row = new VisualElement();
                row.AddToClassList("hub-egg-entry");
                var button = new Button(() => IncubateOrHatch(egg)) { name = $"egg-action-{egg.Id}" };
                button.AddToClassList("hub-workshop-row");
                button.AddToClassList("hub-egg-action");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("hub-egg-icon");
                icon.AddToClassList($"hub-egg-{ShellNames[egg.Element]}");
                button.Add(icon);
                var info = new Label();
                info.AddToClassList("hub-egg-status");
                button.RegisterCallback<FocusInEvent>(_ => list.ScrollTo(button));
                row.Add(button);
                row.Add(info);
                list.Add(row);
                eggRows.Add((egg, button, info));
            }
            if (roster.Eggs.Count == 0)
                Empty(list, T("Compre um ovo para começar.", "Buy an egg to get started.", "Compra un huevo para empezar."));
            RestoreFocus(list, focus);
            lastEggSecond = -1;
            Tick();
            Set("eggState", message ?? (roster.Coins < HubRosterState.EggCost ? ResultText(HubWorkshopResult.InsufficientCoins)
                : T("Ao chocar, o Lumon entra na sua coleção.", "Hatched Lumons join your collection.",
                    "Al eclosionar, el Lumon pasa a tu colección.")));
        }

        private void IncubateOrHatch(HubEgg egg)
        {
            long now = DateTime.UtcNow.Ticks;
            if (egg.ReadyAtTicks == 0)
                Apply(roster.StartIncubation(egg.Id, now), T("Incubação iniciada.", "Incubation started.", "Incubación iniciada."));
            else
            {
                HubWorkshopResult result = roster.HatchEgg(egg.Id, now);
                Apply(result, T("O ovo chocou. O Lumon entrou na coleção.",
                    "The egg hatched. The Lumon joined your collection.", "El huevo eclosionó. El Lumon se unió a tu colección."));
                if (result == HubWorkshopResult.Success) RevealHatch(roster.Creatures[roster.Creatures.Count - 1]);
            }
        }

        private void RevealHatch(HubCreature creature)
        {
            var view = root.Q("hatchReveal");
            var portrait = root.Q("hatchPortrait");
            foreach (string style in ElementStyles) portrait.RemoveFromClassList(style);
            portrait.AddToClassList(ElementStyles[creature.Element]);
            string shell = ShellNames[creature.Element];
            foreach (string kind in ShellNames)
            {
                root.Q("hatchShellTop").EnableInClassList($"hub-egg-{kind}-top", kind == shell);
                root.Q("hatchShellBottom").EnableInClassList($"hub-egg-{kind}-bottom", kind == shell);
            }
            string name = Name(creature.Element);
            Set("hatchTitle", T($"{name} nasceu!", $"{name} has hatched!", $"¡{name} ha nacido!"));
            Set("hatchText", T("O Lumon entrou na sua coleção.", "This Lumon joined your collection.", "El Lumon se unió a tu colección."));
            Set("hatchContinue", T("Ver equipe", "View party", "Ver equipo"));
            root.Q("eggPane").SetEnabled(false);
            foreach (string tab in Tabs) root.Q(tab).SetEnabled(false);
            hatchStartedAt = Time.unscaledTime;
            hatchRevealed = true;
            view.RemoveFromClassList("is-hidden");
            UpdateHatch();
            var button = root.Q<Button>("hatchContinue");
            button.schedule.Execute(button.Focus);
        }

        internal bool DismissHatch(bool restoreEggFocus)
        {
            var view = root.Q("hatchReveal");
            if (view.ClassListContains("is-hidden")) return false;
            hatchRevealed = false;
            view.AddToClassList("is-hidden");
            root.Q("eggPane").SetEnabled(true);
            foreach (string tab in Tabs) root.Q(tab).SetEnabled(true);
            if (restoreEggFocus)
            {
                var eggs = root.Q<Button>("tabEggs");
                eggs.schedule.Execute(eggs.Focus);
            }
            return true;
        }

        private void UpdateHatch()
        {
            float t = reducedMotion() ? .66f : Mathf.Clamp(Time.unscaledTime - hatchStartedAt, 0f, .66f);
            float crack = HubMenuCinematics.EaseOut((t - .08f) / .40f);
            float emerge = HubMenuCinematics.EaseOut((t - .18f) / .48f);
            var upper = root.Q("hatchShellTop");
            var lower = root.Q("hatchShellBottom");
            var portrait = root.Q("hatchPortrait");
            upper.style.opacity = 1f - crack;
            lower.style.opacity = 1f - crack;
            upper.style.translate = new Translate(-20f * crack, -29f * crack);
            lower.style.translate = new Translate(16f * crack, 19f * crack);
            upper.style.rotate = new Rotate(Angle.Degrees(-19f * crack));
            lower.style.rotate = new Rotate(Angle.Degrees(17f * crack));
            portrait.style.opacity = emerge;
            portrait.style.translate = new Translate(0f, 13f * (1f - emerge));
            portrait.style.scale = new Scale(Vector3.one * Mathf.Lerp(.82f, 1f, emerge));
            float caption = HubMenuCinematics.EaseOut((t - .34f) / .32f);
            root.Q("hatchTitle").style.opacity = caption;
            root.Q("hatchText").style.opacity = caption;
            if (t >= .66f) hatchRevealed = false;
        }

        private void Apply(HubWorkshopResult result, string success)
        {
            message = result == HubWorkshopResult.Success ? success : ResultText(result);
            if (result == HubWorkshopResult.Success) balanceChanged();
            Refresh();
        }

        private void Fill(ScrollView list, IEnumerable<HubCreature> creatures, string key, Func<int, bool> chosen, Action<int> select)
        {
            string focus = SelectedFocus(list);
            Vector2 scroll = list.scrollOffset;
            list.Clear();
            foreach (HubCreature item in creatures)
            {
                int id = item.Id;
                var button = new Button(() => select(id)) { name = $"{key}-{id}" };
                button.AddToClassList("hub-workshop-row");
                button.EnableInClassList(Selected, chosen(id));
                string rank = T($"Nv. {item.Level} · Grau {item.Rank}", $"Lv. {item.Level} · Rank {item.Rank}",
                    $"Nv. {item.Level} · Grado {item.Rank}");
                button.text = $"{CreatureName(item)}   {rank}";
                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("hub-workshop-icon");
                portrait.AddToClassList(ElementStyles[item.Element]);
                button.Add(portrait);
                button.AddToClassList("hub-workshop-creature");
                button.RegisterCallback<FocusInEvent>(_ => list.ScrollTo(button));
                list.Add(button);
            }
            list.scrollOffset = scroll;
            RestoreFocus(list, focus);
        }

        private string SelectedFocus(VisualElement container)
        {
            var element = root.focusController?.focusedElement as VisualElement;
            return element != null && container.Contains(element) ? element.name : null;
        }

        private static void RestoreFocus(VisualElement container, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var button = container.Q<Button>(name);
            button?.schedule.Execute(button.Focus);
        }

        private static void Empty(VisualElement list, string content)
        {
            var label = new Label(content);
            label.AddToClassList("hub-workshop-empty");
            list.Add(label);
        }

        private HubCreature Find(int id) => roster.Creatures.FirstOrDefault(creature => creature.Id == id);
        private string Coins(int amount) => T($"{amount} moedas", $"{amount} coins", $"{amount} monedas");
        private string Name(int element) => element switch
        {
            0 => T("Brasa", "Ember", "Brasa"),
            1 => T("Gota", "Droplet", "Gota"),
            _ => T("Broto", "Sprout", "Brote"),
        };

        private string ResultText(HubWorkshopResult result) => result switch
        {
            HubWorkshopResult.InsufficientCoins => T("Saldo de moedas insuficiente.", "You need more coins.", "Saldo de monedas insuficiente."),
            HubWorkshopResult.CapacityReached => T("Capacidade atingida.", "Capacity reached.", "Capacidad alcanzada."),
            HubWorkshopResult.IncubatorBusy => T("A incubadora está ocupada.", "The incubator is busy.", "La incubadora está ocupada."),
            HubWorkshopResult.EggNotReady => T("O ovo ainda está incubando.", "The egg is still incubating.", "El huevo aún se está incubando."),
            HubWorkshopResult.LevelLimit => T("O Lumon já atingiu o nível máximo.", "This Lumon is already at max level.", "El Lumon ya tiene el nivel máximo."),
            HubWorkshopResult.RankLimit => T("O Lumon já atingiu o grau máximo.", "This Lumon is already at max rank.", "El Lumon ya tiene el grado máximo."),
            HubWorkshopResult.MaterialInTeam => T("Retire o material da equipe e salve primeiro.", "Remove the material from the party and save first.",
                "Retira al material del equipo y guarda primero."),
            _ => T("Revise a seleção.", "Check your selection.", "Revisa la selección."),
        };

        private string T(string pt, string en, string es) => text(pt, en, es);
        private void Set(string name, string value) => root.Q<TextElement>(name).text = value;
        private void Bind(string name, Action action)
        {
            var button = root.Q<Button>(name);
            button.clicked += action;
            bindings.Add(button, action);
        }
    }
}
