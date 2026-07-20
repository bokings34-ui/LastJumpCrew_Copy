using System;
using System.Collections.Generic;
using LastJumpCrew.SeoBoGyeong.Economy;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 지속(개인 세이브) 데이터의 런타임 구현. Token 잔액은 TokenWallet(IWallet)에 위임하고,
    /// 변경이 생길 때마다 스토어에 즉시 저장한다(autosave).
    /// 생성 시 스토어에서 로드 — GameCore.Init() 에서 만들어 IPlayerProfile 로 등록한다.
    /// </summary>
    public sealed class PlayerProfile : IPlayerProfile
    {
        private readonly IPlayerProfileStore store;
        private readonly TokenWallet tokenWallet;
        private readonly HashSet<int> ownedCosmetics;

        public int Tokens => tokenWallet.Balance;
        public IReadOnlyCollection<int> OwnedCosmetics => ownedCosmetics;

        public event Action ProfileChanged;

        public PlayerProfile(IPlayerProfileStore store)
        {
            this.store = store;

            var data = store.Load();
            tokenWallet = new TokenWallet(data.tokens);
            ownedCosmetics = new HashSet<int>(data.ownedCosmetics);
        }

        public void AddTokens(int amount)
        {
            tokenWallet.Add(amount);
            SaveAndNotify();
        }

        public bool TrySpendTokens(int amount)
        {
            if (!tokenWallet.TrySpend(amount)) return false;

            SaveAndNotify();
            return true;
        }

        public void UnlockCosmetic(int cosmeticId)
        {
            if (!ownedCosmetics.Add(cosmeticId)) return; // 중복 해금 무시

            SaveAndNotify();
        }

        public bool HasCosmetic(int cosmeticId) => ownedCosmetics.Contains(cosmeticId);

        private void SaveAndNotify()
        {
            store.Save(new PlayerProfileData
            {
                tokens = Tokens,
                ownedCosmetics = new List<int>(ownedCosmetics),
            });
            ProfileChanged?.Invoke();
        }
    }
}
