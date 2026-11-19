using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lumon.HubWorld
{
    public enum HubWorkshopResult
    {
        Success,
        InvalidSelection,
        InsufficientCoins,
        CapacityReached,
        IncubatorBusy,
        EggNotReady,
        LevelLimit,
        RankLimit,
        MaterialInTeam
    }

    [Serializable]
    public sealed class HubCreature
    {
        public int Id;
        public int Element; // 0 = Brasa, 1 = Gota, 2 = Broto
        public int Level;   // 1..HubRosterState.MaxLevel
        public int Rank;    // 1..HubRosterState.MaxRank
    }

    [Serializable]
    public sealed class HubEgg
    {
        public int Id;
        public int Element; // Sorteado na compra, mas oculto até o nascimento.
        public int Tier;    // 0 = Comum (grau 1); 1 = Raro (grau 2)
        public long ReadyAtTicks; // 0 = fora da incubadora; > 0 = ticks UTC em que pode chocar
    }

    /// <summary>
    /// Estado offline da barraca (equipe, ovos, incubadora, aprimoramento e fusão).
    /// Tudo vive em uma única chave JSON de PlayerPrefs; só moedas do jogo, sem dinheiro real.
    /// Cada método mutante valida tudo antes de alterar e salva sozinho; em falha nada muda.
    /// </summary>
    public sealed class HubRosterState
    {
        public const int StartingCoins = 150;
        public const int EggCost = 25;
        public const int RareEggCost = 75;
        public const int ImproveCost = 30;
        public const int FusionCost = 35;
        public const int IncubationSeconds = 18;

        public const int MaxCreatures = 30;
        public const int MaxEggs = 12;
        public const int MaxTeamSize = 3;
        public const int MaxLevel = 5;
        public const int MaxRank = 3;
        public const int ElementCount = 3;

        const int SaveVersion = 1;
        const int BaseCreatureCount = 3; // IDs 1, 2, 3 = Brasa, Gota, Broto
        const string KeySuffix = ".Roster";

        [Serializable]
        sealed class SaveData
        {
            public int Version;
            public int Coins;
            public int NextId;
            public List<HubCreature> Creatures;
            public List<HubEgg> Eggs;
            public List<int> Team;
        }

        readonly string storageKey;
        SaveData data;

        HubRosterState(string storageKey, SaveData data)
        {
            this.storageKey = storageKey;
            this.data = data;
        }

        public int Coins => data.Coins;
        public IReadOnlyList<HubCreature> Creatures => data.Creatures;
        public IReadOnlyList<HubEgg> Eggs => data.Eggs;
        public IReadOnlyList<int> Team => data.Team;

        /// <summary>Bits dos IDs base na equipe (bit 0 = ID 1, bit 1 = ID 2, bit 2 = ID 3); 0 se nenhum.</summary>
        public int LegacyTeamMask
        {
            get
            {
                int mask = 0;
                foreach (int id in data.Team)
                    if (id >= 1 && id <= BaseCreatureCount) mask |= 1 << (id - 1);
                return mask;
            }
        }

        /// <summary>
        /// Carrega o perfil do prefixo. Sem save (ou save corrompido/inválido) cria o perfil inicial
        /// usando a TeamMask legada; com save válido a equipe salva prevalece. Load nunca grava.
        /// </summary>
        public static HubRosterState Load(string keyPrefix, int legacyTeamMask)
        {
            string key = StorageKey(keyPrefix);
            return new HubRosterState(key, ReadValid(key) ?? CreateFresh(legacyTeamMask));
        }

        internal static string StorageKey(string keyPrefix) =>
            (string.IsNullOrEmpty(keyPrefix) ? HubPreferences.DefaultKeyPrefix : keyPrefix) + KeySuffix;

        public HubWorkshopResult SetTeam(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count < 1 || ids.Count > MaxTeamSize) return HubWorkshopResult.InvalidSelection;
            var next = new List<int>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (FindCreature(id) == null || next.Contains(id)) return HubWorkshopResult.InvalidSelection;
                next.Add(id);
            }

            return Commit(() => data.Team = next);
        }

        public HubWorkshopResult BuyEgg(int tier)
        {
            if (tier < 0 || tier > 1) return HubWorkshopResult.InvalidSelection;
            if (data.Eggs.Count >= MaxEggs) return HubWorkshopResult.CapacityReached;
            if (data.NextId == int.MaxValue) return HubWorkshopResult.CapacityReached;
            int cost = tier == 1 ? RareEggCost : EggCost;
            if (data.Coins < cost) return HubWorkshopResult.InsufficientCoins;

            return Commit(() =>
            {
                data.Coins -= cost;
                data.Eggs.Add(new HubEgg
                {
                    Id = data.NextId++,
                    Element = UnityEngine.Random.Range(0, ElementCount),
                    Tier = tier,
                    ReadyAtTicks = 0
                });
            });
        }

        public HubWorkshopResult StartIncubation(int eggId, long nowTicks)
        {
            HubEgg egg = FindEgg(eggId);
            long duration = IncubationSeconds * TimeSpan.TicksPerSecond;
            if (egg == null || nowTicks <= 0 || nowTicks > DateTime.MaxValue.Ticks - duration) return HubWorkshopResult.InvalidSelection;
            foreach (HubEgg other in data.Eggs)
                if (other.ReadyAtTicks > 0) return HubWorkshopResult.IncubatorBusy;

            return Commit(() => egg.ReadyAtTicks = nowTicks + duration);
        }

        public HubWorkshopResult HatchEgg(int eggId, long nowTicks)
        {
            HubEgg egg = FindEgg(eggId);
            if (egg == null) return HubWorkshopResult.InvalidSelection;
            if (egg.ReadyAtTicks <= 0 || nowTicks < egg.ReadyAtTicks) return HubWorkshopResult.EggNotReady;
            if (data.Creatures.Count >= MaxCreatures || data.NextId == int.MaxValue) return HubWorkshopResult.CapacityReached;

            return Commit(() =>
            {
                data.Eggs.Remove(egg);
                data.Creatures.Add(new HubCreature
                {
                    Id = data.NextId++,
                    Element = egg.Element,
                    Level = 1,
                    Rank = egg.Tier == 1 ? 2 : 1
                });
            });
        }

        public HubWorkshopResult Improve(int creatureId)
        {
            HubCreature creature = FindCreature(creatureId);
            if (creature == null) return HubWorkshopResult.InvalidSelection;
            if (creature.Level >= MaxLevel) return HubWorkshopResult.LevelLimit;
            if (data.Coins < ImproveCost) return HubWorkshopResult.InsufficientCoins;

            return Commit(() =>
            {
                data.Coins -= ImproveCost;
                creature.Level++;
            });
        }

        public HubWorkshopResult Fuse(int targetId, int materialId)
        {
            HubCreature target = FindCreature(targetId);
            HubCreature material = FindCreature(materialId);
            if (target == null || material == null || targetId == materialId || target.Element != material.Element)
                return HubWorkshopResult.InvalidSelection;
            if (data.Team.Contains(materialId)) return HubWorkshopResult.MaterialInTeam;
            if (target.Rank >= MaxRank) return HubWorkshopResult.RankLimit;
            if (data.Coins < FusionCost) return HubWorkshopResult.InsufficientCoins;

            return Commit(() =>
            {
                data.Coins -= FusionCost;
                data.Creatures.Remove(material);
                target.Rank++;
            });
        }

        // ------------------------------------------------------------------ persistência

        /// <summary>Aplica a transação já validada e grava; se a gravação falhar, restaura o estado anterior e relança.</summary>
        HubWorkshopResult Commit(Action apply)
        {
            string before = JsonUtility.ToJson(data);
            try
            {
                apply();
                PlayerPrefs.SetString(storageKey, JsonUtility.ToJson(data));
                PlayerPrefs.Save();
            }
            catch
            {
                data = JsonUtility.FromJson<SaveData>(before);
                throw;
            }

            return HubWorkshopResult.Success;
        }

        static SaveData ReadValid(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return null;
            try
            {
                string json = PlayerPrefs.GetString(key, string.Empty);
                if (string.IsNullOrWhiteSpace(json)) return null;
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                return IsValid(loaded) ? loaded : null;
            }
            catch (Exception)
            {
                return null; // JSON corrompido: recomeça do perfil inicial sem lançar
            }
        }

        static SaveData CreateFresh(int legacyTeamMask)
        {
            if (legacyTeamMask < 1 || legacyTeamMask > 7) legacyTeamMask = HubPreferences.DefaultTeamMask;
            var fresh = new SaveData
            {
                Version = SaveVersion,
                Coins = StartingCoins,
                NextId = BaseCreatureCount + 1,
                Creatures = new List<HubCreature>(),
                Eggs = new List<HubEgg>(),
                Team = new List<int>(),
            };
            for (int i = 0; i < BaseCreatureCount; i++)
            {
                fresh.Creatures.Add(new HubCreature { Id = i + 1, Element = i, Level = 1, Rank = 1 });
                if ((legacyTeamMask & (1 << i)) != 0) fresh.Team.Add(i + 1);
            }

            return fresh;
        }

        static bool IsValid(SaveData d)
        {
            if (d == null || d.Version != SaveVersion || d.Coins < 0) return false;
            if (d.Creatures == null || d.Eggs == null || d.Team == null) return false;
            if (d.Creatures.Count < 1 || d.Creatures.Count > MaxCreatures || d.Eggs.Count > MaxEggs) return false;
            if (d.Team.Count < 1 || d.Team.Count > MaxTeamSize) return false;

            var ids = new HashSet<int>();
            var creatureIds = new HashSet<int>();
            foreach (HubCreature c in d.Creatures)
            {
                if (c == null || c.Id < 1 || !ids.Add(c.Id)) return false;
                if (c.Element < 0 || c.Element >= ElementCount) return false;
                if (c.Level < 1 || c.Level > MaxLevel || c.Rank < 1 || c.Rank > MaxRank) return false;
                creatureIds.Add(c.Id);
            }

            int incubating = 0;
            foreach (HubEgg e in d.Eggs)
            {
                if (e == null || e.Id < 1 || !ids.Add(e.Id)) return false;
                if (e.Element < 0 || e.Element >= ElementCount || e.Tier < 0 || e.Tier > 1 || e.ReadyAtTicks < 0 || e.ReadyAtTicks > DateTime.MaxValue.Ticks) return false;
                if (e.ReadyAtTicks > 0 && ++incubating > 1) return false;
            }

            var teamIds = new HashSet<int>();
            foreach (int id in d.Team)
                if (!creatureIds.Contains(id) || !teamIds.Add(id)) return false;

            // O contador de IDs nunca reutiliza um ID existente.
            foreach (int id in ids)
                if (id >= d.NextId) return false;
            return d.NextId > BaseCreatureCount;
        }

        HubCreature FindCreature(int id)
        {
            foreach (HubCreature c in data.Creatures)
                if (c.Id == id) return c;
            return null;
        }

        HubEgg FindEgg(int id)
        {
            foreach (HubEgg e in data.Eggs)
                if (e.Id == id) return e;
            return null;
        }
    }
}
