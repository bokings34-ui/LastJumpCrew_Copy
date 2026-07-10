using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;
using CommonInteraction = LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 배터리 아이템을 전원 소켓에 꽂는 테스트 상호작용 컴포넌트다.
    // 손에 든 배터리로 상호작용하거나, 바닥 배터리가 Trigger에 들어오면 설치된다.
    public sealed class BatteryInsertPowerStationSocket : MonoBehaviour, IInteractable, CommonInteraction.IInteractable, IBatteryUseTarget
    {
        // 설치 가능한 아이템 ID다. 기본값은 UtilityItemPrefabData의 battery_pack과 맞춘다.
        [SerializeField] private string requiredItemId = "battery_pack";

        // 상호작용 UI에 표시할 문구다.
        [SerializeField] private string interactionPrompt = "Insert Battery";

        // 설치 완료 후 켜질 배터리 시각 오브젝트다. 씬/프리팹에서 직접 연결한다.
        [SerializeField] private GameObject installedBatteryVisual;

        // Trigger로 삽입된 월드 배터리를 설치 후 삭제할지 여부다.
        [SerializeField] private bool destroyInsertedBattery = true;

        private bool isBatteryInstalled;

        public string InteractionPrompt => interactionPrompt;
        public bool IsBatteryInstalled => isBatteryInstalled;

        private void Awake()
        {
            // 설치 완료 시각물 참조는 필수다. 없으면 성공 상태를 눈으로 확인할 수 없다.
            if (installedBatteryVisual == null)
            {
                Debug.LogError($"PHS_BATTERY_SOCKET_SETUP_FAILED reason=installedBatteryVisual_missing target={name}");
                return;
            }

            installedBatteryVisual.SetActive(isBatteryInstalled);
        }

        private void OnTriggerEnter(Collider other)
        {
            // 바닥에 놓인 배터리가 소켓 Trigger에 들어온 경우 자동 삽입한다.
            // 손에 든 아이템은 Trigger로 처리하지 않고 Interact 경로에서 소비한다.
            if (isBatteryInstalled)
            {
                return;
            }

            if (other == null)
            {
                Debug.LogError($"PHS_BATTERY_INSERT_FAILED reason=collider_missing target={name}");
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            if (itemObject == null)
            {
                return;
            }

            if (itemObject.IsHeld)
            {
                return;
            }

            if (itemObject.ItemId != requiredItemId)
            {
                Debug.LogWarning($"PHS_BATTERY_INSERT_FAILED reason=wrong_item target={name} item={itemObject.ItemId}");
                return;
            }

            InsertBattery(itemObject);
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            // 플레이어 손에 든 아이템이 requiredItemId와 같을 때만 상호작용 가능하다.
            if (isBatteryInstalled)
            {
                return false;
            }

            if (installedBatteryVisual == null)
            {
                Debug.LogError($"PHS_BATTERY_INTERACT_FAILED reason=installedBatteryVisual_missing target={name}");
                return false;
            }

            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_BATTERY_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return false;
            }

            if (itemHolder.CurrentItemPrefabData == null)
            {
                Debug.LogWarning($"PHS_BATTERY_INTERACT_FAILED reason=heldItem_missing target={name}");
                return false;
            }

            if (itemHolder.CurrentItemPrefabData.ItemId != requiredItemId)
            {
                Debug.LogWarning($"PHS_BATTERY_INTERACT_FAILED reason=wrong_item target={name} expected={requiredItemId} actual={itemHolder.CurrentItemPrefabData.ItemId}");
                return false;
            }

            return true;
        }

        public void Interact(IItemHolder itemHolder)
        {
            // 손에 든 배터리는 TryConsumeHeldItem으로 제거한 뒤 설치 상태로 바꾼다.
            if (!CanInteract(itemHolder))
            {
                return;
            }

            if (!itemHolder.TryConsumeHeldItem(requiredItemId))
            {
                Debug.LogError($"PHS_BATTERY_INTERACT_FAILED reason=consume_failed target={name} item={requiredItemId}");
                return;
            }

            if (InstallBattery())
            {
                Debug.Log($"PHS_BATTERY_INSERTED_BY_INTERACT target={name} item={requiredItemId}");
            }
        }

        bool CommonInteraction.IInteractable.CanInteract(CommonInteraction.IItemHolder itemHolder)
        {
            return CanUseBattery(itemHolder);
        }

        void CommonInteraction.IInteractable.Interact(CommonInteraction.IItemHolder itemHolder)
        {
            TryUseBattery(itemHolder);
        }

        public bool CanUseBattery(CommonInteraction.IItemHolder user)
        {
            if (isBatteryInstalled)
            {
                return false;
            }

            if (installedBatteryVisual == null)
            {
                Debug.LogError($"PHS_BATTERY_USE_FAILED reason=installedBatteryVisual_missing target={name}");
                return false;
            }

            if (user == null)
            {
                Debug.LogWarning($"PHS_BATTERY_USE_FAILED reason=user_missing target={name}");
                return false;
            }

            if (user.CurrentItem == null)
            {
                Debug.LogWarning($"PHS_BATTERY_USE_FAILED reason=heldItem_missing target={name}");
                return false;
            }

            if (user.CurrentItem.ItemId != requiredItemId)
            {
                Debug.LogWarning($"PHS_BATTERY_USE_FAILED reason=wrong_item target={name} expected={requiredItemId} actual={user.CurrentItem.ItemId}");
                return false;
            }

            return true;
        }

        public bool TryUseBattery(CommonInteraction.IItemHolder user)
        {
            if (!CanUseBattery(user))
            {
                return false;
            }

            if (user is not IItemHolder parkItemHolder)
            {
                Debug.LogError($"PHS_BATTERY_USE_FAILED reason=parkItemHolder_missing target={name}");
                return false;
            }

            if (!parkItemHolder.TryConsumeHeldItem(requiredItemId))
            {
                Debug.LogError($"PHS_BATTERY_USE_FAILED reason=consume_failed target={name} item={requiredItemId}");
                return false;
            }

            if (!InstallBattery())
            {
                return false;
            }

            Debug.Log($"PHS_BATTERY_INSERTED_BY_USE target={name} item={requiredItemId}");
            return true;
        }

        private void InsertBattery(UtilityItemObject itemObject)
        {
            // Trigger 삽입 경로는 이미 바닥 배터리임을 확인하고 들어온다.
            if (installedBatteryVisual == null)
            {
                Debug.LogError($"PHS_BATTERY_INSERT_FAILED reason=installedBatteryVisual_missing target={name}");
                return;
            }

            if (!InstallBattery())
            {
                return;
            }

            if (destroyInsertedBattery && itemObject != null)
            {
                Destroy(itemObject.gameObject);
            }

            Debug.Log($"PHS_BATTERY_INSERTED target={name} item={requiredItemId}");
        }

        private bool InstallBattery()
        {
            // 설치 상태는 중복 삽입 방지와 시각물 활성화 기준으로 사용한다.
            if (installedBatteryVisual == null)
            {
                Debug.LogError($"PHS_BATTERY_INSTALL_FAILED reason=installedBatteryVisual_missing target={name}");
                return false;
            }

            isBatteryInstalled = true;
            installedBatteryVisual.SetActive(true);
            return true;
        }
    }
}
