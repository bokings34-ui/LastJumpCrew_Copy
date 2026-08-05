using LastJumpCrew.Common;
using UnityEngine;

namespace SM
{
    public class CommDeviceTarget : MonoBehaviour, IDevice, IDamageable, IInteractable, IRequireHeldItem, IRepairable
    {
        private const string WrenchItemId = "wrench";

        [SerializeField, Min(1)] private int maximumHealth = 10;
        [SerializeField] private Transform visualRoot;

        [Header("수리 설정")]
        [SerializeField, Min(1f)] private float maxRepairProgress = 10f;

        private Renderer[] _renderers;
        private bool[] _initialRendererStates;
        private int _currentHealth;
        private bool _isRegistered;
        private bool _isDestroyed;
        private float _repairProgress;

        public Transform Transform => transform;
        public bool IsAlive => _currentHealth > 0;

        // IRequireHeldItem
        public string RequiredItemId => WrenchItemId;

        public bool IsRequirementMet(IItemHolder itemHolder)
        {
            return itemHolder.HasItem && itemHolder.CurrentItem.ItemId == RequiredItemId;
        }

        // IInteractable
        public string InteractionPrompt => "렌치로 장치 수리하기";

        public bool CanInteract(IItemHolder itemHolder)
        {
            return _isDestroyed && IsRequirementMet(itemHolder);
        }

        public void Interact(IItemHolder itemHolder)
        {
            // 실제 수리는 렌치 아이템의 IUsableItem.Use()에서 ApplyRepair() 호출로 처리
        }

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform;

            _renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            _initialRendererStates = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _initialRendererStates[i] = _renderers[i].enabled;
            }
        }

        private void OnEnable()
        {
            _currentHealth = maximumHealth;
            _isDestroyed = false;
            _repairProgress = 0f;
            SetVisualsAlive(true);
            DeviceRegistry.Instance.Register(this);
            _isRegistered = true;
        }

        private void OnDisable()
        {
            Unregister();
        }

        public void ApplyDamage(int amount, GameObject attacker)
        {
            if (amount <= 0 || !IsAlive) return;

            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            if (_currentHealth > 0) return;

            DestroyDevice(attacker);
        }

        private void DestroyDevice(GameObject attacker)
        {
            _isDestroyed = true;
            _repairProgress = 0f;

            Unregister();
            SetVisualsAlive(false);

            Debug.Log($"<color=orange>[CommDeviceTarget]</color> {name} 파괴됨 (공격자: {attacker?.name ?? "unknown"})");
        }

        // 렌치 아이템이 Use()에서 직접 호출하는 진입점 (Fire/OxygenLeak과 동일 패턴)
        // TODO: 팀원 IRepairable 공유받으면 이 시그니처를 그쪽에 맞춰 교체
        public void ApplyRepair(float amount)
        {
            if (!_isDestroyed) return;

            _repairProgress += amount;
            if (_repairProgress < maxRepairProgress) return;

            RestoreDevice();
        }

        private void RestoreDevice()
        {
            _isDestroyed = false;
            _currentHealth = maximumHealth;
            _repairProgress = 0f;
            SetVisualsAlive(true);

            if (isActiveAndEnabled && !_isRegistered)
            {
                DeviceRegistry.Instance.Register(this);
                _isRegistered = true;
            }

            Debug.Log($"<color=lime>[CommDeviceTarget]</color> {name} 복구 완료.");
        }

        private void Unregister()
        {
            if (!_isRegistered) return;
            DeviceRegistry.Peek()?.Unregister(this);
            _isRegistered = false;
        }

        private void SetVisualsAlive(bool alive)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = alive && _initialRendererStates[i];
            }
        }

        // ===== IRepairable 대응 준비 (팀원 IRepairable.cs main 반영 후 주석 해제) =====
        public bool CanRepair => _isDestroyed;

        public float CurrentIntegrity => _repairProgress;

        public float MaxIntegrity => maxRepairProgress;

        public bool ApplyRepair(float amount, GameObject repairer)
        {
            if (!CanRepair || amount <= 0f) return false;
            ApplyRepair(amount); // 기존 ApplyRepair(float) 재사용 - 이름 겹침 주의, 아래 참고
            return true;
        }
        // ================================================================
    }
}
