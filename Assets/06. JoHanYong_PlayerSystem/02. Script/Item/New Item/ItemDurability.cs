using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using System;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ItemDurability : NetworkBehaviour
    {
        [Header("Item Data")]

        [Tooltip("아이템의 최대 내구도와 사용 당 소모량이 들어있는 SO")]
        [SerializeField]
        private UtilityItemDataSO itemData;

        private readonly NetworkVariable<int> currentDurability = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action<int, int> OnDurabilityChanged; //내구도 변경 시 호출 | 1번째는 변경 전 내구도 2번째는 변경 후 내구도 

        public event Action OnBroken; //내구도 0이 되어 파손되었을 때 호출

        public UtilityItemDataSO ItemData => itemData;
        public int CurrentDurability => currentDurability.Value;

        public int MaxDurability => itemData != null ? itemData.MaxDurability : 0;

        public bool UsesDurability => itemData != null && itemData.UsesDurability; //아이템이 내구도를 사용하는 확인

        public bool IsBroken => UsesDurability && CurrentDurability <= 0; //아이템 파손 상태 확인, 내구도를 사용 안하는 아이템은 파손된 아이템으로 취급을 안함

        public bool CanUse //내구도가 사용 비용보다 높아야함
        {
            get
            {
                if (ItemData == null)
                {
                    return false;
                }
                if (!ItemData.UsesDurability)
                {
                    return true;
                }
                return currentDurability.Value >= itemData.DurabilityCostPerUse;
            }
        }
        private void Awake()
        {
            if(itemData  == null)
            {
                Debug.LogError($"PHS_ITEM_DURABILITY_SETUP_FAILED " + $"reason=item_data_missing " + $"item={name}");
            }
        }
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            currentDurability.OnValueChanged += HandleDurabilityChanged;
            //현재 내구도 변경 이벤트를 등록합니다.
            if (IsServer)
            {
                InitializeDurability();
            }
        }
        public override void OnNetworkDespawn()
        {
            currentDurability.OnValueChanged -= HandleDurabilityChanged;

            base.OnNetworkDespawn();
        }
        private void InitializeDurability()
        {

            // 아이템 처음 생성되었을 때 현재 내구도를 최대 내구도로 설정합니다.
            if (!IsServer || itemData == null)
            {
                return;
            }
            if (itemData.UsesDurability)
            {
                currentDurability.Value = itemData.MaxDurability;
            }
            else
            {
                currentDurability.Value = 0;
                // 내구도를 사용하지 않는 아이템은 현재 내구도를 0으로 두어도 됩니다.
            }
        }
        public bool ConsumeForUse()
        {
            if (!IsServer)
            {
                Debug.LogError($"PHS_ITEM_DURABILITY_CONSUME_FAILED " + $"reason=server_only " + $"item={name}");
                return false;

            }
            if (itemData == null)
            {
                return false;
            }
            if (!itemData.UsesDurability)//내구도를 사용하지 않는 아이템은 차감하지 않고 성공으로 처리한다.
            {
                return true;
            }
            int cost = itemData.DurabilityCostPerUse;

            if(cost <= 0)
            {
                return true;
            }

            if (currentDurability.Value < cost)
            {
                Debug.Log($"PHS_ITEM_DURABILITY_CONSUME_FAILED " + $"reason=not_enough_durability " + $"item={name} " + $"current={currentDurability.Value} " + $"cost={cost}");
                return false;
            }

            ReduceDurability(cost);
            return true;
        }
        public void ReduceDurability(int amount)
        {
            //지정된 수치만큼 내구도를 감소시킴
            if (!IsServer)
            {
                return;
            }
            if (!UsesDurability || amount <= 0)
            {
                return;
            }
            int previousDurability = currentDurability.Value;

            currentDurability.Value = Mathf.Clamp(previousDurability -  amount, 0, itemData.MaxDurability);

            if(previousDurability > 0 && currentDurability.Value <= 0)
            {
                OnBroken?.Invoke();

                Debug.Log($"PHS_ITEM_BROKEN item={name}" , this);
            } 
        }
        public void RestoreDurability(int amount)
        {
            if (!IsServer)
            {
                return;
            }
            if(!UsesDurability || amount <= 0)
            {
                return;
            }
            currentDurability.Value = Mathf.Clamp(currentDurability.Value + amount, 0 , itemData.MaxDurability);
            //지정된 수치만큼 아이템 내구도를 복구
        }
        private void HandleDurabilityChanged(int previousValue, int newValue)
        {
            //UI 내구도 표시를 갱신할 때, 이 이벤트를 구독하기 
            OnDurabilityChanged?.Invoke(previousValue, newValue);

            Debug.Log($"PHS_ITEM_DURABILITY_CHANGED " + $"item={name} " + $"previous={previousValue} " + $"current={newValue}");
        }
    }
}
