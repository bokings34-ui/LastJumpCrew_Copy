namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>함선 생존 목 — 항상 생존. 협의 후 실제 함선 IDamageable 로 교체.</summary>
    public sealed class MockShipStatus : IShipStatus
    {
        public bool IsShipAlive => true;
    }
}
