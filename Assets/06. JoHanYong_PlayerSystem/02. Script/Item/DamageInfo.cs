using UnityEngine;

public readonly struct DamageInfo//피해를 전달할 때 사용하는 정보
{
    public readonly int Damage;
    public readonly GameObject Attacker;
    public readonly Vector3 HitPoint;

    public DamageInfo(int damage, GameObject attacker, Vector3 hitPoint)
    {
        Damage = damage;
        Attacker = attacker;
        HitPoint = hitPoint;
    }

}
