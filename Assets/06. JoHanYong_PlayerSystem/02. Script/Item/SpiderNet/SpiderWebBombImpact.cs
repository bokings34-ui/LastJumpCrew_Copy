using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class SpiderWebBombImpact : NetworkBehaviour
    {
        [Header("Slow Zone")]

        // 첫 충돌 위치에 생성할 거미줄 장판 프리팹
        [SerializeField]
        private SpiderWebSlowZone slowZonePrefab;

        // 폭탄을 던진 플레이어
        private GameObject attacker;

        // 투척 당시 사용한 거미줄 폭탄 SO
        private UtilityItemDataSO itemData;

        // 첫 충돌만 처리하기 위한 플래그
        private bool hasImpacted;

        /// <summary>
        /// 거미줄 폭탄이 공격 투척될 때
        /// 공격자와 SO 데이터를 전달받는다.
        /// </summary>
        public void InitializeAttackThrow(
            GameObject throwAttacker,
            UtilityItemDataSO throwItemData)
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            if (throwAttacker == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_INIT_FAILED " +
                    $"reason=attacker_missing bomb={name}",
                    this);
                return;
            }

            if (throwItemData == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_INIT_FAILED " +
                    $"reason=item_data_missing bomb={name}",
                    this);
                return;
            }

            // 투척형 아이템인지 확인
            if (throwItemData.UseType != ItemUseType.Throwable)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_INIT_FAILED " +
                    $"reason=use_type_invalid " +
                    $"item={throwItemData.ItemId} " +
                    $"actual={throwItemData.UseType}",
                    throwItemData);
                return;
            }

            if (throwItemData.AttackRadius <= 0f)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_INIT_FAILED " +
                    $"reason=attack_radius_invalid " +
                    $"item={throwItemData.ItemId} " +
                    $"radius={throwItemData.AttackRadius:F2}",
                    throwItemData);
                return;
            }

            if (throwItemData.HitEffects == null ||
                throwItemData.HitEffects.Count == 0)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_INIT_FAILED " +
                    $"reason=hit_effects_missing " +
                    $"item={throwItemData.ItemId}",
                    throwItemData);
                return;
            }

            attacker = throwAttacker;
            itemData = throwItemData;
            hasImpacted = false;

            Debug.Log(
                $"PHS_WEB_BOMB_ARMED " +
                $"bomb={name} " +
                $"item={itemData.ItemId} " +
                $"attacker={attacker.name}",
                this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // 장판 생성은 서버만 담당
            if (!IsServer)
            {
                return;
            }

            // 이미 첫 충돌을 처리했으면 다시 실행하지 않는다.
            if (hasImpacted)
            {
                return;
            }

            if (collision == null)
            {
                return;
            }

            if (itemData == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_IMPACT_FAILED " +
                    $"reason=item_data_missing bomb={name}",
                    this);
                return;
            }

            if (attacker == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_IMPACT_FAILED " +
                    $"reason=attacker_missing bomb={name}",
                    this);
                return;
            }

            hasImpacted = true;

            // 첫 번째 충돌 위치
            Vector3 hitPosition =
                collision.contactCount > 0
                    ? collision.GetContact(0).point
                    : transform.position;

            Debug.Log(
                $"PHS_WEB_BOMB_FIRST_IMPACT " +
                $"bomb={name} " +
                $"target={collision.collider.name} " +
                $"position={hitPosition}",
                this);

            SpawnSlowZone(hitPosition);
        }

        private void SpawnSlowZone(Vector3 position)
        {
            if (slowZonePrefab == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_ZONE_FAILED " +
                    $"reason=zone_prefab_missing bomb={name}",
                    this);
                return;
            }

            SpiderWebSlowZone zoneInstance =
                Instantiate(
                    slowZonePrefab,
                    position,
                    Quaternion.identity);

            if (zoneInstance == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_ZONE_FAILED " +
                    $"reason=instantiate_failed bomb={name}",
                    this);
                return;
            }

            NetworkObject zoneNetworkObject =
                zoneInstance.GetComponent<NetworkObject>();

            if (zoneNetworkObject == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_ZONE_FAILED " +
                    $"reason=network_object_missing " +
                    $"zone={zoneInstance.name}",
                    zoneInstance);

                Destroy(zoneInstance.gameObject);
                return;
            }

            // NetworkObject.Spawn() 전에 데이터를 먼저 전달
            zoneInstance.InitializeServer(
                attacker,
                itemData);

            if (!zoneNetworkObject.IsSpawned)
            {
                zoneNetworkObject.Spawn();
            }

            Debug.Log(
                $"PHS_WEB_BOMB_ZONE_CREATED " +
                $"bomb={name} " +
                $"item={itemData.ItemId} " +
                $"position={position}",
                zoneInstance);

            // 폭탄은 장판 생성 후 역할 종료
            if (NetworkObject != null &&
                NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}