using System.Collections.Generic;
using UnityEngine;

namespace RFIDBaggage.Levels
{
    [CreateAssetMenu(
        fileName = "LevelDatabase",
        menuName = "RFID Baggage/Level Database"
    )]
    public sealed class LevelDatabase : ScriptableObject
    {
        [SerializeField, Tooltip("Level configs available to the RFID baggage flow.")]
        private List<LevelConfig> levels = new List<LevelConfig>(6);

        public IReadOnlyList<LevelConfig> Levels => levels;

        public bool TryGetByLevelId(string levelId, out LevelConfig level)
        {
            string normalized = LevelConfig.NormalizeIdentifier(levelId);
            return TryGetByNormalizedId(normalized, false, out level);
        }

        public bool TryGetByRfidId(string rfidId, out LevelConfig level)
        {
            string normalized = LevelConfig.NormalizeIdentifier(rfidId);
            return TryGetByNormalizedId(normalized, true, out level);
        }

        private bool TryGetByNormalizedId(string normalizedId, bool useRfid, out LevelConfig level)
        {
            level = null;

            if (string.IsNullOrEmpty(normalizedId))
            {
                return false;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                LevelConfig candidate = levels[i];
                if (candidate == null || !candidate.IsValid(out _))
                {
                    continue;
                }

                string candidateId = useRfid
                    ? candidate.GetNormalizedRfidId()
                    : candidate.GetNormalizedLevelId();

                if (string.Equals(candidateId, normalizedId, System.StringComparison.Ordinal))
                {
                    level = candidate;
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            ValidateDuplicates(false);
            ValidateDuplicates(true);
        }

        private void ValidateDuplicates(bool useRfid)
        {
            HashSet<string> ids = new HashSet<string>();

            for (int i = 0; i < levels.Count; i++)
            {
                LevelConfig level = levels[i];
                if (level == null)
                {
                    continue;
                }

                string id = useRfid ? level.GetNormalizedRfidId() : level.GetNormalizedLevelId();
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (!ids.Add(id))
                {
                    string idType = useRfid ? "RFID ID" : "level ID";
                    Debug.LogWarning($"[LevelDatabase] Duplicate {idType}: {id}", this);
                }
            }
        }
    }
}
