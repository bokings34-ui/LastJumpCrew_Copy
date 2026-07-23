using LastJumpCrew.ParkHanSol.Combat;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.Common;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{

    // 플레이어 공격 요청과 서버 판정을 담당한다.
    // 실제 OverlapSphere, 데미지, 넉백 판정은 서버가 수행한다.

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerCombatController : NetworkBehaviour
    {
        private const string WrenchItemId = "wrench";
        private const string FireExtinguisherItemId = "fire_extinguisher";

        [Header("Wrench Attack")]

        [SerializeField]
        private Transform wrenchAttackPoint;

        [SerializeField, Min(0.1f)]
        private float wrenchAttackRadius = 1.2f; //렌치 공격의 구형 판정 범위

        [SerializeField, Min(0)]
        private int wrenchDamage = 15; //몬스터한테 적용되는 데미지

        [SerializeField, Min(0f)]
        private float wrenchKnockback = 4f; //넉백 세기 => 몬스터

        [SerializeField, Min(0.01f)]
        private float wrenchCooldown = 0.5f; //공격 간격

        [Header("Fire Extinguisher Spray")]
        [SerializeField]
        private Transform extinguisherSprayOrigin; //분사 시작위치

        [SerializeField, Min(0.05f)]
        private float extinguisherSprayRadius = 0.6f; //분사 범위

        [SerializeField, Min(0.1f)]
        private float extinguisherSprayDistance = 4f; //분사 도달하는 최대거리

        [SerializeField, Min(0)]
        private int extinguisherDamagePerTick = 2; //데미지

        [SerializeField, Min(0f)]
        private float extinguisherKnockback = 2f; //넉백 세기
        [SerializeField, Min(0.05f)]
        private float extinguisherDamageInterval = 0.5f; //분사 판정을 실행하는 시간 간격

        [SerializeField]
        private LayerMask extinguisherTargetLayers; //분사로 감지하는 레이어 플레이어 몬스터

        private float nextExtinguisherDamgeTime; //서버가 관리하는 다음 분사판정 가능 시간

        [Header("Battery Throw")] //배터리 필드
        [SerializeField] private Transform batteryThrowOrigin;

        [SerializeField, Min(0f)]
        private float batteryThrowForce = 12f; //카메라 전면 투척 힘
        [SerializeField, Min(0f)]
        private float battetyUpwardForce = 1.5f; //약간 뛰우기 위한 값 -> 포물선 느낌?

        [SerializeField, Min(0f)]
        private float batteryThrowCooldown = 0.8f; //좌클릭 연속 입력 요청 중복 방지

        private float nextBatteryThrowTime;

        private float nextBatteryServerThrowTime;
        [Header("General Item Throw")] //일반 투척

        [SerializeField]
        private Transform generalThrowOrigin; //일반 투척 시작위치
        [SerializeField, Min(0f)]
        private float minimumThrowForce = 5f; //투척 최소 속도
        [SerializeField, Min(0f)]
        private float maximumThrowForce = 13f;//완전 충전 투척 속도
        [SerializeField, Min(0.1f)]
        private float fullChargeTime = 2.5f; //최대 충전 시간
        [SerializeField, Min(0f)]
        private float generalThrowCooldown = 0.3f; //일단 투척 쿨타임

        [Header("Fire Extinguisher Visual Effect")]
        [SerializeField]
        private GameObject extinguisherSprayEffectRoot;

        [SerializeField]
        private GameObject extinguisherWorldSprayEffectRoot;

        [UnityEngine.Serialization.FormerlySerializedAs("extinguisherEffectKeepAliceTime")]
        [SerializeField, Min(0.05f)]
        private float extinguisherEffectKeepAliveTime = 0.65f;
        private float extinguisherEffectStopTime;

        [Header("Local Use Feedback")]
        [SerializeField] private ParticleSystem wrenchUseEffect;
        [SerializeField] private ParticleSystem batteryUseEffect;

        private float nextGeneralThrowTime;

        public Transform GeneralThrowOrigin => generalThrowOrigin;




        [SerializeField]
        private string batteryItemId = "battery_pack";
        [SerializeField]
        private LayerMask wrenchTargetLayers; //몬스터 플레이어 레이어 판정

        private readonly HashSet<GameObject> processedTargets = new();
        private readonly List<Vector3> itemFeedbackTargetPositions = new();

        private float nextWrenchAttackTime;

        private uint utilityAttackSequence;

        private bool isExtinguisherEffectPlaying;
        private void Awake()
        {
            CacheExtinguisherEffects();
        }
        private void Update()
        {
            UpdateExtinguisherEffect();

        }
        private void CacheExtinguisherEffects()
        {
            var firstPersonParticleCount = PrepareExtinguisherEffectRoot(
                extinguisherSprayEffectRoot,
                "first_person");
            var worldParticleCount = PrepareExtinguisherEffectRoot(
                extinguisherWorldSprayEffectRoot,
                "world");
            if (firstPersonParticleCount < 0 || worldParticleCount < 0)
            {
                return;
            }

            isExtinguisherEffectPlaying = false;
            Debug.Log(
                $"PHS_EXTINGUISHER_EFFECT_CACHED player={name} " +
                $"firstPersonParticles={firstPersonParticleCount} " +
                $"worldParticles={worldParticleCount}");
        }
        private void UpdateExtinguisherEffect()
        {
            if (!isExtinguisherEffectPlaying)
            {
                return;
            }

            if (Time.time < extinguisherEffectStopTime)
            {
                return;
            }

            StopExtinguisherEffect();
            isExtinguisherEffectPlaying = false;

            Debug.Log($"PHS_EXTINGUISHER_EFFECT_STOPPED " +$"player={name}");
        }
        public void RequestWrenchAttack()
        {
            if (!IsSpawned)
            {
                PlayOneShotEffect(wrenchUseEffect);
                PerformWrenchAttack();
                return;
            }
            if (!IsOwner)
            {
                return;
            }
            PlayOneShotEffect(wrenchUseEffect);
            RequestWrenchAttackServerRpc();
        }
        [ServerRpc]
        private void RequestWrenchAttackServerRpc() //서버에서 실제 렌치 공격 판정 실행
        {
            PerformWrenchAttack();
        }
        private void PerformWrenchAttack()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }
            if (!HasExpectedHeldItem(WrenchItemId))
            {
                Debug.LogWarning($"PHS_WRENCH_ATTACK_FAILED reason=item_mismatch player={name}");
                return;
            }
            if (wrenchAttackPoint == null)
            {
                Debug.LogError($"PHS_WRENCH_ATTACK_FAILED " +
                $"reason=attack_point_missing " +
                $"player={name}");

                return;
            }
            if (Time.time < nextWrenchAttackTime) //서버 기준 쿨타임 검사
            {
                return;
            }
            nextWrenchAttackTime = Time.time + wrenchCooldown;

            var hits = Physics.OverlapSphere(wrenchAttackPoint.position, wrenchAttackRadius, wrenchTargetLayers, QueryTriggerInteraction.Collide);

            processedTargets.Clear();
            itemFeedbackTargetPositions.Clear();

            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }


                if(!CombatHitResolver.TryResolveCombatTarget(hit, out var targetObject))
                {
                    continue;
                }
                if(CombatHitResolver.IsSameTarget(targetObject, gameObject))
                {
                    continue;
                }
                if (!processedTargets.Add(targetObject))
                {
                    continue;
                }
                var knockbackDirection = targetObject.transform.position - wrenchAttackPoint.position;

                CombatHitResolver.ResolveDamageAndKnockback(targetObject, gameObject, wrenchDamage, knockbackDirection, wrenchKnockback);

                var targetObject = CombatHitResolver.ResolveTargetObject(hit.gameObject);

                if (targetObject == null || targetObject.transform.root == transform.root)//자기 자신은 공격하지 않는다.
                {
                    continue;
                }

                if (!processedTargets.Add(targetObject)) //콜라이드가 여러개 있어도 한번 만 처리
                {
                    continue;
                }
                var requestSequence = NextUtilityAttackSequence();
                if (CombatHitResolver.TryResolveUtilityAttack(
                        targetObject,
                        gameObject,
                        WrenchItemId,
                        requestSequence))
                {
                    RecordAcceptedItemTarget(
                        WrenchItemId,
                        targetObject,
                        "utility_repair",
                        hit.ClosestPoint(wrenchAttackPoint.position),
                        requestSequence);
                    continue;
                }

                var knockbackDirection = targetObject.transform.position - wrenchAttackPoint.position;
                var damageable = targetObject.GetComponentInParent<IDamageable>();
                var knockbackable = targetObject.GetComponentInParent<IKnockbackable>();
                var acceptsCombatReaction = (damageable != null && damageable.IsAlive)
                    || knockbackable != null;
                CombatHitResolver.ResolveDamageAndKnockback(targetObject, gameObject, wrenchDamage, knockbackDirection, wrenchKnockback);
                if (acceptsCombatReaction)
                {
                    RecordAcceptedItemTarget(
                        WrenchItemId,
                        targetObject,
                        "damage_or_knockback",
                        hit.ClosestPoint(wrenchAttackPoint.position),
                        requestSequence);
                }

            }
            PublishItemUseFeedback(
                PHSItemUseFeedbackKind.Wrench,
                PHSItemUseFeedbackShape.Sphere,
                wrenchAttackPoint.position,
                wrenchAttackPoint.forward,
                wrenchAttackRadius,
                0f);
            Debug.Log(
                $"PHS_WRENCH_ATTACK player={name} candidates={processedTargets.Count} acceptedTargets={itemFeedbackTargetPositions.Count}",
                this);
        }
        public void RequestExtinguisherSpray() //자기 플레이어만 소화기 사용 요청을 보낼 수 있음
        {
            if (!IsSpawned)
            {
                if (PerformExtinguisherSpray())
                {
                    PlayExtinguisherEffectLocal();
                }
                return;
            }
            if (!IsOwner)
            {
                return;
            }
            RequestExtinguisherSprayServerRpc();
        }
        public void RequestBatteryThrow() //배터리
        {
            if(batteryThrowOrigin == null)
            {
                Debug.LogError($"PHS_BATTERY_THROW_FAILED " + $"reason=throw_origin_missing " + $"player={name}");
                return;
            }
            if (!IsSpawned)
            {
                PlayOneShotEffect(batteryUseEffect);
                PerformBatteryThrow(batteryThrowOrigin.position, batteryThrowOrigin.forward);
                return;
            }
            if (!IsOwner)
            {
                return;
            }
            if(Time.time < nextBatteryThrowTime) //쿨타임 아직 끝나지 않으면 중복 투척 요청 x
            {
                return ;
            }
            if(batteryThrowOrigin == null)
            {
                Debug.LogError($"PHS_BATTERY_THROW_FAILED " + $"reason=throw_origin_missing " + $"player={name}");
                return;
            }
            nextBatteryThrowTime = Time.time + batteryThrowCooldown;
            PlayOneShotEffect(batteryUseEffect);

            var direction = batteryThrowOrigin.forward.normalized; //플레이어가 바라보는 방향

            Debug.Log($"PHS_BATTERY_THROW_INPUT_ACCEPTED " + $"player={name} " + $"position={batteryThrowOrigin.position} " + $"direction={direction}");

            RequestBatteryThrowServerRpc(batteryThrowOrigin.position, direction);

        }
        public void RequestThrowHeldItem(float heldDuration)
        {
            if (!IsSpawned)
            {
                if(generalThrowOrigin == null)
                {
                    return;
                }
                var localForce = CalculateThrowForce(heldDuration);

                PerformThrowHeldItem(generalThrowOrigin.position, generalThrowOrigin.forward, localForce);
                return;

            }
            if(generalThrowOrigin == null)
            {
                Debug.LogError($"PHS_ITEM_THROW_FAILED " + $"reason=throw_origin_missing " + $"player={name}");
                return;
            }
            if(Time.time < nextGeneralThrowTime)
            {
                return ;
            }
            nextGeneralThrowTime = Time.time + generalThrowCooldown;

            var throwForce = CalculateThrowForce(heldDuration);

            var throwDirection = generalThrowOrigin.forward.normalized;

            Debug.Log($"PHS_ITEM_THROW_REQUESTED " + $"player={name} " + $"duration={heldDuration:F2} " + $"force={throwForce:F2}");

            RequestThrowHeldItemServerRpc(generalThrowOrigin.position, throwDirection, throwForce);
        }
        private void PerformThrowHeldItem(Vector3 requestedPosition, Vector3 requestedDirection, float requestedForce)
        {
            if (IsSpawned && !IsServer) //멀티 중에는 서버만 투척처리
            {
                return;
            }
            var itemHolder = GetComponent<TempPlayerItemHolder>();

            if(itemHolder == null)
            {
                Debug.LogError($"PHS_ITEM_THROW_FAILED " + $"reason=item_holder_missing " + $"player={name}");
                return;
            }
            if (!itemHolder.HasItem) //손의 아무것도 없으면 투척x
            {
                return;
            }
            var direction = requestedDirection.sqrMagnitude > 0.001f ? requestedDirection.normalized : transform.forward;
            var throwPosition = requestedPosition;

            if ((throwPosition - transform.position).sqrMagnitude > 9f)
            {
                throwPosition = transform.position + transform.forward * 0.7f;
            }
            var throwForce = Mathf.Clamp(requestedForce, minimumThrowForce, maximumThrowForce);

            var isBatteryThrow = itemHolder.IsHoldingItem(batteryItemId);
            GameObject thrownItem;
            var batteryDamage = 0;
            var created = isBatteryThrow
                ? itemHolder.TryCreateThrownItem(
                    throwPosition,
                    Quaternion.LookRotation(direction),
                    UtilityItemActionKind.BatteryDischarge,
                    out thrownItem,
                    out batteryDamage)
                : itemHolder.TryCreateThrownItem(
                    throwPosition,
                    Quaternion.LookRotation(direction),
                    out thrownItem);
            if (!created) //현재 손 아이템의 DroppedPrefab을 생성
            {
                return;
            }
            var body = thrownItem.GetComponent<Rigidbody>();
            if (body == null)
            {
                Debug.LogError($"PHS_ITEM_THROW_FAILED " + $"reason=rigidbody_missing " + $"item={thrownItem.name}");

                RemoveFailedThrownObject(thrownItem);

                return;
            }
            body.isKinematic = false;
            body.detectCollisions = true;

            if (isBatteryThrow)
            {
                var batteryImpact = thrownItem.GetComponent<BatteryThrownImpact>();
                if (batteryImpact == null)
                {
                    Debug.LogError(
                        $"PHS_BATTERY_THROW_FAILED reason=impact_missing item={thrownItem.name}",
                        thrownItem);
                    RemoveFailedThrownObject(thrownItem);
                    return;
                }

                batteryImpact.InitializeAttackThrow(gameObject, batteryDamage);
            }

            //카메라 방향으로 계산된 힘 만큼 날린다.
            body.linearVelocity = direction * throwForce;

            var impact = thrownItem.GetComponent<ThrownItemImpact>();
            if (impact != null)
            {
                impact.InitializeThrow(gameObject);
            }
            else
            {
                Debug.LogWarning($"PHS_ITEM_THROW_WARNING " + $"reason=thrown_impact_missing " + $"item={thrownItem.name}");
            }
            Debug.Log($"PHS_ITEM_THROW_EXECUTED " + $"player={name} " + $"item={thrownItem.name} " + $"force={throwForce:F2}");
        }
        private float CalculateThrowForce(float heldDuration)
        {
            var chargeRatio = Mathf.Clamp01(heldDuration / fullChargeTime);

            return Mathf.Lerp(minimumThrowForce, maximumThrowForce, chargeRatio);
        }
        private void PerformBatteryThrow(Vector3 requestedPosition,Vector3 requestedDirection)
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }
            var itemHolder = GetComponent<TempPlayerItemHolder>();

            if(itemHolder == null)
            {
                Debug.LogError($"PHS_BATTERY_THROW_FAILED " + $"reason=item_holder_missing " + $"player={name}");

                return;
            }
            if (!itemHolder.IsHoldingItem(batteryItemId))
            {
                Debug.LogWarning($"PHS_BATTERY_THROW_FAILED " + $"reason=battery_not_held " + $"player={name} " + $"actual=" + $"{itemHolder.CurrentItemPrefabData?.ItemId ?? "none"}");
                return;
            }
            var direction = requestedDirection.sqrMagnitude > 0.001f ? requestedDirection.normalized : transform.forward;

            var throwPosition = requestedPosition;

            if((throwPosition - transform.position).sqrMagnitude > 9f)
            {
                throwPosition = transform.position + transform.forward * 0.7f;
            }
            if (!itemHolder.TryCreateThrownItem(
                    throwPosition,
                    Quaternion.LookRotation(direction),
                    UtilityItemActionKind.BatteryDischarge,
                    out var batteryInstance,
                    out var batteryDamage))
            {
                return;
            }
            var body = batteryInstance.GetComponent<Rigidbody>();

            var impact = batteryInstance.GetComponent<BatteryThrownImpact>();

            if (body == null || impact == null)
            {
                Debug.LogError($"PHS_BATTERY_THROW_FAILED " + $"reason=required_component_missing " + $"battery={batteryInstance.name}");
                RemoveFailedThrownObject(batteryInstance);

                return;
            }
            body.isKinematic = false;
            body.detectCollisions = true;

            impact.InitializeAttackThrow(gameObject, batteryDamage);

            var throwVelocity = direction * batteryThrowForce + Vector3.up * battetyUpwardForce;

            body.linearVelocity = throwVelocity;

            Debug.Log(
                $"PHS_BATTERY_THROW_EXECUTED player={name} battery={batteryInstance.name} rangeFeedback=on_first_impact",
                this);
        }
        private void RemoveFailedThrownObject(GameObject thrownObject)
        {
            if (thrownObject == null)
            {
                return ;
            }
            var networkObject = thrownObject.GetComponent<NetworkObject>();
            if(networkObject != null && networkObject.IsSpawned && IsServer)
            {
                networkObject.Despawn(true);
                return;
            }
            Destroy(thrownObject );
        }

        [ServerRpc]
        private void RequestExtinguisherSprayServerRpc()
        {
            if (PerformExtinguisherSpray())
            {
                PlayExtinguisherEffectClientRpc();
            }
        }
        private bool PerformExtinguisherSpray()
        {
            if (IsSpawned && !IsServer)
            {
                return false;
            }//네트워크 플레이 중에는 서버만 공격판정 수행
            if (!HasExpectedHeldItem(FireExtinguisherItemId))
            {
                Debug.LogWarning($"PHS_EXTINGUISHER_SPRAY_FAILED reason=item_mismatch player={name}");
                return false;
            }

            if (extinguisherSprayOrigin == null)
            {
                Debug.LogError($"PHS_EXTINGUISHER_SPRAY_FAILED" + $"reason=spray_origin_missing " + $"player={name}");

                return false;
            }

            //서버 판정 간격 검사
            if (Time.time < nextExtinguisherDamgeTime)
            {
                return false;
            }
            nextExtinguisherDamgeTime = Time.time + extinguisherDamageInterval;

            //분사 범위 판정
            var hits = Physics.SphereCastAll(extinguisherSprayOrigin.position, extinguisherSprayRadius, extinguisherSprayOrigin.forward, extinguisherSprayDistance, extinguisherTargetLayers, QueryTriggerInteraction.Collide);

            processedTargets.Clear();
            itemFeedbackTargetPositions.Clear();

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }
                if(!CombatHitResolver.TryResolveCombatTarget(hit.collider, out var targetObject))
                {
                    continue;
                }
                if(CombatHitResolver.IsSameTarget(targetObject, gameObject))
                {
                    continue;
                }
                if (!processedTargets.Add(targetObject))
                {
                    continue;
                }
                var sprayDirection = extinguisherSprayOrigin.forward;
                CombatHitResolver.ResolveDamageAndKnockback(targetObject, gameObject, extinguisherDamagePerTick, sprayDirection, extinguisherKnockback);

                var targetObject = CombatHitResolver.ResolveTargetObject(hit.collider.gameObject);

                //소화기 사용하는 자신은 안 맞음
                if (targetObject == null || targetObject.transform.root == transform.root)
                {
                    continue;
                }
                if (!processedTargets.Add(targetObject))//Collider가 여러 개 검출돼고 한번만 데미지 넉백 처리용
                {
                    continue;
                }
                var requestSequence = NextUtilityAttackSequence();
                if (CombatHitResolver.TryResolveUtilityAttack(
                        targetObject,
                        gameObject,
                        FireExtinguisherItemId,
                        requestSequence))
                {
                    RecordAcceptedItemTarget(
                        FireExtinguisherItemId,
                        targetObject,
                        "fire_suppression",
                        hit.point,
                        requestSequence);
                    continue;
                }

                var sprayDirection = extinguisherSprayOrigin.forward; //넉백 방향
                var damageable = targetObject.GetComponentInParent<IDamageable>();
                var knockbackable = targetObject.GetComponentInParent<IKnockbackable>();
                var acceptsCombatReaction = (damageable != null && damageable.IsAlive)
                    || knockbackable != null;
                CombatHitResolver.ResolveDamageAndKnockback(targetObject, gameObject, extinguisherDamagePerTick, sprayDirection, extinguisherKnockback);
                if (acceptsCombatReaction)
                {
                    RecordAcceptedItemTarget(
                        FireExtinguisherItemId,
                        targetObject,
                        "damage_or_knockback",
                        hit.point,
                        requestSequence);
                }

            }
            PublishItemUseFeedback(
                PHSItemUseFeedbackKind.FireExtinguisher,
                PHSItemUseFeedbackShape.Cast,
                extinguisherSprayOrigin.position,
                extinguisherSprayOrigin.forward,
                extinguisherSprayRadius,
                extinguisherSprayDistance);
            Debug.Log(
                $"PHS_EXTINGUISHER_SPRAY player={name} candidates={processedTargets.Count} acceptedTargets={itemFeedbackTargetPositions.Count}",
                this);
            return true;
        }
        [ServerRpc]
        private void RequestBatteryThrowServerRpc(Vector3 throwPosition, Vector3 throwDirection, ServerRpcParams rpcParams = default)
        {
            PerformBatteryThrow(throwPosition, throwDirection);

        }
        [ServerRpc]
        private void RequestThrowHeldItemServerRpc(Vector3 throwPosition, Vector3 throwDirection, float requestedForce, ServerRpcParams rpcParams = default)
        {
            PerformThrowHeldItem(throwPosition, throwDirection, requestedForce);
        }
        private void OnDrawGizmosSelected()
        {
            if (wrenchAttackPoint == null)
            {
                return;
            }
            Gizmos.DrawWireSphere(wrenchAttackPoint.position, wrenchAttackRadius);

            // 소화기 분사 범위


            if (extinguisherSprayOrigin != null)
            {
                var endPosition =
                    extinguisherSprayOrigin.position
                    + extinguisherSprayOrigin.forward
                    * extinguisherSprayDistance;

                // 분사 시작 지점
                Gizmos.DrawWireSphere(
                    extinguisherSprayOrigin.position,
                    extinguisherSprayRadius);

                // 분사 끝 지점
                Gizmos.DrawWireSphere(
                    endPosition,
                    extinguisherSprayRadius);

                // 분사 방향
                Gizmos.DrawLine(
                    extinguisherSprayOrigin.position,
                    endPosition);
            }
        }

        private uint NextUtilityAttackSequence()
        {
            utilityAttackSequence++;
            if (utilityAttackSequence == 0U)
            {
                utilityAttackSequence = 1U;
            }

            return utilityAttackSequence;
        }

        private void PublishItemUseFeedback(
            PHSItemUseFeedbackKind kind,
            PHSItemUseFeedbackShape shape,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance)
        {
            var feedbackController = GetComponent<PHSNetworkItemUseFeedbackController>();
            if (feedbackController == null)
            {
                Debug.LogError(
                    $"PHS_ITEM_FEEDBACK_FAILED reason=controller_missing player={name}",
                    this);
                return;
            }

            feedbackController.PublishServerFeedback(
                kind,
                shape,
                origin,
                direction,
                radius,
                distance,
                itemFeedbackTargetPositions.ToArray());
        }

        private void RecordAcceptedItemTarget(
            string itemId,
            GameObject target,
            string reaction,
            Vector3 feedbackPosition,
            uint requestSequence)
        {
            var resolvedPosition = feedbackPosition == Vector3.zero && target != null
                ? target.transform.position
                : feedbackPosition;
            itemFeedbackTargetPositions.Add(resolvedPosition);
            Debug.Log(
                $"PHS_ITEM_TARGET_REACTION item={itemId} target={target?.name ?? "missing"} reaction={reaction} result=accepted sequence={requestSequence} position={resolvedPosition}",
                target);
        }

        private bool HasExpectedHeldItem(string expectedItemId)
        {
            if (IsSpawned)
            {
                var itemRecord = GetComponent<NetworkPlayerItemRecord>();
                return itemRecord != null
                    && itemRecord.IsSpawned
                    && itemRecord.HeldItemId == expectedItemId;
            }

            var itemHolder = GetComponent<TempPlayerItemHolder>();
            return itemHolder != null
                && itemHolder.CurrentItemPrefabData != null
                && itemHolder.CurrentItemPrefabData.ItemId == expectedItemId;
        }
        [ClientRpc]
        private void PlayExtinguisherEffectClientRpc()
        {
            PlayExtinguisherEffectLocal();
        }
        private void PlayExtinguisherEffectLocal()
        {
            if (!TryGetActiveExtinguisherEffect(
                    out var effectRoot,
                    out var particles,
                    out var audioSources,
                    out var effectLights,
                    out var view))
            {
                return;
            }

            if (!effectRoot.activeInHierarchy)
            {
                Debug.LogError($"PHS_EXTINGUISHER_EFFECT_FAILED " + $"reason=effect_inactive_in_hierarchy " + $"player={name}");

                return;
            }
            if (!isExtinguisherEffectPlaying)
            {
                foreach (var particle in particles)
                {
                    if (particle == null)
                    {
                        continue;
                    }

                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                    var particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
                    if (particleRenderer != null)
                    {
                        particleRenderer.enabled = true;
                    }

                    particle.Play(true);
                }
            }
            foreach (var audioSource in audioSources)
            {
                if (audioSource != null &&
                    audioSource.clip != null)
                {
                    audioSource.Play();
                }
            }
            foreach (var effectLight in effectLights)
            {
                if (effectLight != null)
                {
                    effectLight.enabled = true;
                }
            }
            isExtinguisherEffectPlaying = true;

            Debug.Log(
                $"PHS_EXTINGUISHER_EFFECT_STARTED player={name} " +
                $"view={view} particles={particles.Length}");

            extinguisherEffectStopTime = Time.time + extinguisherEffectKeepAliveTime;
        }

        private static void PlayOneShotEffect(ParticleSystem effect)
        {
            if (effect == null)
            {
                return;
            }

            var effectRenderer = effect.GetComponent<ParticleSystemRenderer>();
            if (effectRenderer != null)
            {
                effectRenderer.enabled = true;
            }

            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play(true);
        }




        private void StopExtinguisherEffect()
        {
            if (!TryGetActiveExtinguisherEffect(
                    out _,
                    out var particles,
                    out var audioSources,
                    out var effectLights,
                    out _))
            {
                return;
            }

            foreach (var particle in particles)
            {
                if (particle == null)
                {
                    continue;
                }

                particle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting);
            }

            foreach (var audioSource in audioSources)
            {
                if (audioSource != null)
                {
                    audioSource.Stop();
                }
            }

            foreach (var effectLight in effectLights)
            {
                if (effectLight != null)
                {
                    effectLight.enabled = false;
                }
            }
        }

        private int PrepareExtinguisherEffectRoot(GameObject effectRoot, string view)
        {
            if (effectRoot == null)
            {
                Debug.LogError(
                    $"PHS_EXTINGUISHER_EFFECT_CACHE_FAILED reason=effect_root_missing player={name} view={view}",
                    this);
                return -1;
            }

            if (effectRoot.GetComponentsInChildren<Collider>(true).Length > 0)
            {
                Debug.LogError(
                    $"PHS_EXTINGUISHER_EFFECT_CACHE_FAILED reason=collider_present player={name} view={view}",
                    effectRoot);
                return -1;
            }

            effectRoot.SetActive(true);
            var particles = effectRoot.GetComponentsInChildren<ParticleSystem>(true);
            if (particles.Length == 0)
            {
                Debug.LogError(
                    $"PHS_EXTINGUISHER_EFFECT_CACHE_FAILED reason=particles_missing player={name} view={view}",
                    effectRoot);
                return -1;
            }

            foreach (var particle in particles)
            {
                particle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (var audioSource in effectRoot.GetComponentsInChildren<AudioSource>(true))
            {
                audioSource.Stop();
            }

            foreach (var effectLight in effectRoot.GetComponentsInChildren<Light>(true))
            {
                effectLight.enabled = false;
            }

            return particles.Length;
        }

        private bool TryGetActiveExtinguisherEffect(
            out GameObject effectRoot,
            out ParticleSystem[] particles,
            out AudioSource[] audioSources,
            out Light[] effectLights,
            out string view)
        {
            var firstPerson = !IsSpawned || IsOwner;
            effectRoot = firstPerson
                ? extinguisherSprayEffectRoot
                : extinguisherWorldSprayEffectRoot;
            view = firstPerson ? "first_person" : "world";
            if (effectRoot == null)
            {
                particles = null;
                audioSources = null;
                effectLights = null;
                Debug.LogError(
                    $"PHS_EXTINGUISHER_EFFECT_FAILED reason=effect_root_missing player={name} view={view}",
                    this);
                return false;
            }

            particles = effectRoot.GetComponentsInChildren<ParticleSystem>(true);
            audioSources = effectRoot.GetComponentsInChildren<AudioSource>(true);
            effectLights = effectRoot.GetComponentsInChildren<Light>(true);
            if (particles.Length > 0)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_EXTINGUISHER_EFFECT_FAILED reason=particles_missing player={name} view={view}",
                effectRoot);
            return false;
        }
    }
}
