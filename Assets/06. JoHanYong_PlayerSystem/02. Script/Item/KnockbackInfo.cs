using UnityEngine;

public readonly struct KnockbackInfo //넉백을 전달할 때 사용하는 정보
{
    public readonly Vector3 Direction;
    public readonly float Force;
    public readonly GameObject Attacker;

    public KnockbackInfo(Vector3 direction, float force, GameObject attacker)
    {
        Direction = direction.normalized;
        Force = force;
        Attacker = attacker;
    }
}
