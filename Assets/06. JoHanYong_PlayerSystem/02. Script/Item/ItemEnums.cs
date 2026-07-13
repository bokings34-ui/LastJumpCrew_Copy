using UnityEngine;

public enum ItemType //아이템 자체의 종류
{
    None,
    Wrench,
    FireExtinguisher,
    Battery
}
public enum ItemAttackType //공격 아이템 사용 방식
{
    None,
    Melee,
    Spray,
    Throw
}
public enum EffectType //상태이상의 종류
{
    None,
    Electric,
    Stun,
    Burn
}
