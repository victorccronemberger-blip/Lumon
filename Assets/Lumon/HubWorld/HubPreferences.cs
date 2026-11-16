using System;
using UnityEngine;

namespace Lumon.HubWorld
{
    /// <summary>Preferências do Hub persistidas em PlayerPrefs. Valores são validados ao carregar e ao salvar.</summary>
    public sealed class HubPreferences
    {
        public const string DefaultKeyPrefix = "Lumon.Hub.v1";
        public const string DefaultLanguage = "pt-BR";
        public const int DefaultTeamMask = 7; // bit 0 = Brasa, bit 1 = Gota, bit 2 = Broto
        public const float DefaultMasterVolume = 0.72f;

        static readonly string[] ValidLanguages = { "pt-BR", "en", "es" };

        public string Language { get; set; } = DefaultLanguage;
        public int TeamMask { get; set; } = DefaultTeamMask;
        public bool ExpeditionPlanned { get; set; }
        public float MasterVolume { get; set; } = DefaultMasterVolume;
        public bool ReducedMotion { get; set; }

        public static HubPreferences Load(string keyPrefix = DefaultKeyPrefix)
        {
            var prefs = new HubPreferences
            {
                Language = PlayerPrefs.GetString(Key(keyPrefix, "Language"), DefaultLanguage),
                TeamMask = PlayerPrefs.GetInt(Key(keyPrefix, "TeamMask"), DefaultTeamMask),
                ExpeditionPlanned = PlayerPrefs.GetInt(Key(keyPrefix, "ExpeditionPlanned"), 0) == 1,
                MasterVolume = PlayerPrefs.GetFloat(Key(keyPrefix, "MasterVolume"), DefaultMasterVolume),
                ReducedMotion = PlayerPrefs.GetInt(Key(keyPrefix, "ReducedMotion"), 0) == 1,
            };
            prefs.Sanitize();
            return prefs;
        }

        public void Save(string keyPrefix = DefaultKeyPrefix)
        {
            Sanitize();
            PlayerPrefs.SetString(Key(keyPrefix, "Language"), Language);
            PlayerPrefs.SetInt(Key(keyPrefix, "TeamMask"), TeamMask);
            PlayerPrefs.SetInt(Key(keyPrefix, "ExpeditionPlanned"), ExpeditionPlanned ? 1 : 0);
            PlayerPrefs.SetFloat(Key(keyPrefix, "MasterVolume"), MasterVolume);
            PlayerPrefs.SetInt(Key(keyPrefix, "ReducedMotion"), ReducedMotion ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Troca valores inválidos pelos defaults (locale desconhecido, máscara fora de 1..7, volume NaN/fora de 0..1).</summary>
        void Sanitize()
        {
            Language = NormalizeLanguage(Language);
            if (TeamMask < 1 || TeamMask > 7) TeamMask = DefaultTeamMask;
            MasterVolume = float.IsNaN(MasterVolume) ? DefaultMasterVolume : Mathf.Clamp01(MasterVolume);
        }

        static string NormalizeLanguage(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var trimmed = value.Trim();
                foreach (var valid in ValidLanguages)
                    if (string.Equals(valid, trimmed, StringComparison.OrdinalIgnoreCase))
                        return valid;
            }
            return DefaultLanguage;
        }

        static string Key(string keyPrefix, string name)
        {
            return (string.IsNullOrEmpty(keyPrefix) ? DefaultKeyPrefix : keyPrefix) + "." + name;
        }
    }
}
