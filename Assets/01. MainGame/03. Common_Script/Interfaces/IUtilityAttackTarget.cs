using UnityEngine;

namespace LastJumpCrew.Common
{
    public readonly struct UtilityAttackHit
    {
        public UtilityAttackHit(string itemId, GameObject attacker, uint requestSequence)
        {
            ItemId = itemId ?? string.Empty;
            Attacker = attacker;
            RequestSequence = requestSequence;
        }

        public string ItemId { get; }
        public GameObject Attacker { get; }
        public uint RequestSequence { get; }
    }

    // The struck object decides whether a utility attack is a repair/suppression hit.
    public interface IUtilityAttackTarget
    {
        bool TryResolveUtilityAttack(in UtilityAttackHit hit);
    }
}
