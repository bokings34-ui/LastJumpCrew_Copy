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

    // ?åÎ†à?¥Ïñ¥ Í≥µÍ≤© ?îÏ≤≠Í≥??úÎ≤Ñ ?êÏ†ï???¥Îãπ?úÎã§.
    // ?§Ï†ú OverlapSphere, ?∞Î?ÏßÄ, ?âÎ∞± ?êÏ†ï?Ä ?úÎ≤ÑÍ∞Ä ?òÌñâ?úÎã§.

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerCombatController : NetworkBehaviour
    {
        private TempPlayerItemHolder itemHolder;

        [Header("Wrench Attack")]

        [SerializeField]
        private Transform wrenchAttackPoint;

        [SerializeField, Min(0.1f)]
        private float wrenchRepairRadius = 2.4f;

        [Header("Fire Extinguisher Spray")]
        [SerializeField]
        private Transform extinguisherSprayOrigin; //Î∂ÑÏÇ¨ ?úÏûë?ÑÏπò

        private float nextExtinguisherDamageTime; //?úÎ≤ÑÍ∞Ä Í¥ÄÎ¶¨Ìïò???§Ïùå Î∂ÑÏÇ¨?êÏ†ï Í∞Ä???úÍ∞Ñ

        [Header("Battery Throw")] //Î∞∞ÌÑ∞Î¶??ÑÎìú
        [SerializeField] private Transform batteryThrowOrigin;

        private float nextBatteryThrowTime;

        [Header("General Item Throw")] //?ºÎ∞ò ?¨Ï≤ô

        [SerializeField]
        private Transform generalThrowOrigin; //?ºÎ∞ò ?¨Ï≤ô ?úÏûë?ÑÏπò
        [SerializeField, Min(0f)]
        private float minimumThrowForce = 5f; //?¨Ï≤ô ÏµúÏÜå ?çÎèÑ
        [SerializeField, Min(0f)]
        private float maximumThrowForce = 13f;//?ÑÏ†Ñ Ï∂©Ï†Ñ ?¨Ï≤ô ?çÎèÑ
        [SerializeField, Min(0.1f)]
        private float fullChargeTime = 2.5f; //ÏµúÎ? Ï∂©Ï†Ñ ?úÍ∞Ñ
        [SerializeField, Min(0f)]
        private float generalThrowCooldown = 0.3f; //?ºÎã® ?¨Ï≤ô Ïø®Ì???

        [Header("Broken Item Ejection")]
        [SerializeField, Min(0f)] private float brokenItemThrowForce = 2.5f;
        [SerializeField, Min(0f)] private float brokenItemUpwardForce = 0.5f;
        [SerializeField, Min(0f)] private float brokenItemDespawnDelay = 5f;

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

        private readonly HashSet<GameObject> processedTargets = new();
        private readonly List<Vector3> itemFeedbackTargetPositions = new();

        private float nextWrenchAttackTime;

        private uint utilityAttackSequence;

        private bool isExtinguisherEffectPlaying;
        private void Awake()
        {
            itemHolder = GetComponent<TempPlayerItemHolder>();

            if(itemHolder == null)
            {
                Debug.LogError($"PHS_COMBAT_SETUP_FAILED " + $"reason=item_holder_missing " + $"player={name}", this);
            }
            CacheExtinguisherEffects();
        }
        private void Update()
        {
            UpdateExtinguisherEffect();

        }
        private bool TryGetCurrentItemData(out UtilityItemDataSO itemData)
        {
            itemData = null;
            if(itemHolder == null)
            {
                return false;
            }
            itemData = itemHolder.CurrentItemPrefabData;

            return itemData != null;
        }
        private bool TryGetHeldItemData(ItemUseType expectedUseType, out UtilityItemDataSO itemData)
        {
            itemData = null;
            if(itemHolder == null)
            {
                Debug.LogError($"PHS_ITEM_USE_FAILED " + $"reason=item_holder_missing " + $"player={name}", this);

                return false;
            }
            itemData = itemHolder.CurrentItemPrefabData;

            if(itemData == null)
            {
                Debug.LogWarning($"PHS_ITEM_USE_FAILED " + $"reason=held_item_data_missing " + $"player={name}", this);

                return false;
            }
            if(itemData.UseType != expectedUseType)
            {
                Debug.LogWarning($"PHS_ITEM_USE_FAILED " + $"reason=use_type_mismatch " + $"player={name} " + $"item={itemData.ItemId} " +
                    $"expected={expectedUseType} " + $"actual={itemData.UseType}", this);

                itemData = null;
                return false;
            }
            return true;
        }
        private bool HasExpectedHeldItem(ItemUseType expectedUseType, out UtilityItemDataSO itemData)
        {
            itemData = null;

            if(!TryGetCurrentItemData(out itemData))
            {
                return false;
            }

            return itemData.UseType == expectedUseType;
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
        private void RequestWrenchAttackServerRpc() //?úÎ≤Ñ?êÏÑú ?§Ï†ú ?åÏπò Í≥µÍ≤© ?êÏ†ï ?§Ìñâ
        {
            PerformWrenchAttack();
        }
        private void PerformWrenchAttack()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }
            /// Î≥ÄÍ≤?: Í≥†Ï†ï??ItemId Í≤Ä???Ä???ÑÏû¨ ?ÑÏù¥??UseType ??Melee?∏Ï? Í≤Ä?¨ÌïòÍ≥?SO ?∞Ïù¥?∞Î? Í∞Ä?∏Ïò®??
            if(!TryGetHeldItemData(ItemUseType.Melee, out var itemData))
            {
                Debug.LogWarning($"PHS_WRENCH_ATTACK_FAILED " + $"reason=item_data_or_use_type_invalid " + $"player={name}");

                return;
            }
            if (wrenchAttackPoint == null)
            {
                Debug.LogError($"PHS_WRENCH_ATTACK_FAILED " +
                $"reason=attack_point_missing " +
                $"player={name}");

                return;
            }
            if (Time.time < nextWrenchAttackTime) 
            {
                return;
            }
            if(!CanUseHeldItemDurabiliy(itemData, out uint expectedRevision))
            {
                Debug.LogWarning($"PHS_WRENCH_ATTACK_FAILED " + $"reason=durability_unavailable " + $"player={name} " + $"item={itemData.ItemId}", this);
                return;
            }
            nextWrenchAttackTime = Time.time + itemData.Cooldown;
            //Î≥ÄÍ≤??§Î≤Ñ???§Ìîº??Î≤îÏúÑÎ•?SO ?∞Ïù¥??Í∞íÏùÑ ?ΩÍ≤å ?òÏ†ï
            

            bool successfulUse = false;
            var hits = Physics.OverlapSphere(
                wrenchAttackPoint.position,
                Mathf.Max(itemData.AttackRadius, wrenchRepairRadius),
                itemData.TargetLayers,
                QueryTriggerInteraction.Collide);

            processedTargets.Clear();
            itemFeedbackTargetPositions.Clear();

            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                // ÎßûÏ? ColliderÍ∞Ä ?çÌïú ?§Ï†ú ?Ä?ÅÏùÑ Ï∞æÎäî??
                // ?òÎ¶¨ ?Ä?? ?åÎ†à?¥Ïñ¥, Î™¨Ïä§???±ÏùÑ Î™®Îëê Ï∞æÎäî??
                var targetObject = CombatHitResolver.ResolveTargetObject(hit.gameObject);
                

                if (targetObject == null)
                {
                    continue;
                }

                // ?êÍ∏∞ ?êÏã†?Ä Í≥µÍ≤©?òÍ±∞???òÎ¶¨?òÏ? ?äÎäî??
                if (CombatHitResolver.IsSameTarget(targetObject, gameObject))
                 
                {
                    continue;
                }

                // ColliderÍ∞Ä ?¨Îü¨ Í∞??àÏñ¥????Î≤àÎßå Ï≤òÎ¶¨?úÎã§.
                if (!processedTargets.Add(targetObject))
                {
                    continue;
                }

                var requestSequence = NextUtilityAttackSequence();
                var hitDistance = Vector3.Distance(
                    wrenchAttackPoint.position,
                    hit.ClosestPoint(wrenchAttackPoint.position));
                if (hitDistance <= wrenchRepairRadius)
                {
                    bool utilityAccepted = CombatHitResolver.TryResolveUtilityAttack(
                        targetObject,
                        gameObject,
                        itemData.ItemId,
                        requestSequence);
                    bool repairApplied = utilityAccepted
                        || RepairResolver.TryRepair(itemData, targetObject, gameObject);
                    if (repairApplied)
                    {
                        successfulUse = true;
                        RecordAcceptedItemTarget(
                            itemData.ItemId,
                            targetObject,
                            "utility_repair",
                            hit.ClosestPoint(wrenchAttackPoint.position),
                            requestSequence);
                        continue;
                    }
                }

                if (hitDistance > itemData.AttackRadius)
                {
                    continue;
                }

                // ?ÅÌò∏?ëÏö© ?Ä?ÅÏù¥ ?ÑÎãà?ºÎ©¥ ?ÑÌà¨ ?Ä?ÅÏùÑ Í≤Ä?¨Ìïú??
                var damageable = targetObject.GetComponentInParent<IDamageable>();
               

                var knockbackable = targetObject.GetComponentInParent<IKnockbackable>();
               
             
                bool acceptsCombatReaction = (damageable != null && damageable.IsAlive) || (knockbackable != null && knockbackable.CanReceiveKnockback);
             

                if (!acceptsCombatReaction)
                {
                    continue;
                }

                // ?åÏπò Í≥µÍ≤© ÏßÄ?êÏóê???Ä??Î∞©Ìñ•?ºÎ°ú ?âÎ∞±?úÎã§.
                var knockbackDirection = targetObject.transform.position - wrenchAttackPoint.position;


                bool effectApplied = ItemEffectResolver.ApplyEffects(itemData, targetObject, knockbackDirection, gameObject);
                if (!effectApplied)
                {
                    continue;
                }

                successfulUse = true;

                RecordAcceptedItemTarget(itemData.ItemId, targetObject, "damage_or_knockback", hit.ClosestPoint(wrenchAttackPoint.position), requestSequence);
            }
            if (successfulUse)
            {
                if(!TryConsumeHeldItemDurability(itemData, expectedRevision))
                {
                    Debug.LogWarning($"PHS_WRENCH_DURABILITY_CONSUME_FAILED " + $"player={name} " + $"item={itemData.ItemId}", this);
                }
            }
            PublishItemUseFeedback(
                PHSItemUseFeedbackKind.Wrench,
                PHSItemUseFeedbackShape.Sphere,
                wrenchAttackPoint.position,
                wrenchAttackPoint.forward,
                itemData.AttackRadius,
                0f);
            Debug.Log(
                $"PHS_WRENCH_ATTACK player={name} candidates={processedTargets.Count} acceptedTargets={itemFeedbackTargetPositions.Count}",
                this);
        }
        public void RequestExtinguisherSpray() //?êÍ∏∞ ?åÎ†à?¥Ïñ¥Îß??åÌôîÍ∏??¨Ïö© ?îÏ≤≠??Î≥¥ÎÇº ???àÏùå
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
        public void RequestBatteryThrow() //Î∞∞ÌÑ∞Î¶?
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
            if(!TryGetHeldItemData(ItemUseType.Throwable, out var itemData))
            {
                Debug.LogWarning($"PHS_BATTERY_THROW_FAILED " + $"reason=item_data_or_use_type_invalid " + $"player={name}");
                return;
            }
            if(itemData.ItemId != batteryItemId)//?§Î•∏ ?¨Ï≤ô???ÑÏù¥?úÏùò Î∞∞ÌÑ∞Î¶?Í≥µÍ≤© Î∞©Ï?
            {
                Debug.LogWarning($"PHS_BATTERY_THROW_FAILED " + $"reason=battery_not_held " + $"player={name} " + $"actual={itemData.ItemId}");
                return;
            }
            if(Time.time < nextBatteryThrowTime) //Ïø®Ì????ÑÏßÅ ?ùÎÇòÏßÄ ?äÏúºÎ©?Ï§ëÎ≥µ ?¨Ï≤ô ?îÏ≤≠ x
            {
                return ;
            }
            nextBatteryThrowTime = Time.time + itemData.Cooldown;
            PlayOneShotEffect(batteryUseEffect);

            var direction = batteryThrowOrigin.forward.normalized; //?åÎ†à?¥Ïñ¥Í∞Ä Î∞îÎùºÎ≥¥Îäî Î∞©Ìñ•

            Debug.Log($"PHS_BATTERY_THROW_INPUT_ACCEPTED " + $"player={name} " + $"item={itemData.ItemId} " + $"position={batteryThrowOrigin.position} " + $"direction={direction}");

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
            if (IsSpawned && !IsServer) //Î©Ä??Ï§ëÏóê???úÎ≤ÑÎß??¨Ï≤ôÏ≤òÎ¶¨
            {
                return;
            }
            var itemHolder = GetComponent<TempPlayerItemHolder>();

            if(itemHolder == null)
            {
                Debug.LogError($"PHS_ITEM_THROW_FAILED " + $"reason=item_holder_missing " + $"player={name}");
                return;
            }
            if (!itemHolder.HasItem) //?êÏùò ?ÑÎ¨¥Í≤ÉÎèÑ ?ÜÏúºÎ©??¨Ï≤ôx
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

            var thrownItemData = itemHolder.CurrentItemPrefabData; //?ÑÏù¥???úÍ±∞ ?ÑÏóê SO ?Ä??
            if(thrownItemData == null)
            {
                Debug.LogError($"PHS_ITEM_THROW_FAILED " + $"reason=item_data_missing " + $"player={name}");

                return;
            }
            var isBatteryThrow = thrownItemData.ItemId == batteryItemId;
            GameObject thrownItem;
        
            var created = isBatteryThrow
                ? itemHolder.TryCreateThrownItem(
                    throwPosition,
                    Quaternion.LookRotation(direction),
                    UtilityItemActionKind.BatteryDischarge,
                    out thrownItem,
                    out _)
                : itemHolder.TryCreateThrownItem(
                    throwPosition,
                    Quaternion.LookRotation(direction),
                    out thrownItem);
            if (!created) //?ÑÏû¨ ???ÑÏù¥?úÏùò DroppedPrefab???ùÏÑ±
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

                batteryImpact.InitializeAttackThrow(gameObject, thrownItemData);
            }

            var spiderWebImpact = thrownItem.GetComponent<SpiderWebBombImpact>();
            if (spiderWebImpact != null)
            {
                spiderWebImpact.InitializeAttackThrow(gameObject, thrownItemData);
            }

            //Ïπ¥Î©î??Î∞©Ìñ•?ºÎ°ú Í≥ÑÏÇ∞????ÎßåÌÅº ?†Î¶∞??
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
            if(!TryGetHeldItemData(ItemUseType.Throwable, out var batteryItemData))
            {
                Debug.LogWarning($"PHS_BATTERY_THROW_FAILED " + $"reason=item_data_or_use_type_invalid " + $"player={name}");
                return;
            }

            if(batteryItemData == null)
            {
                Debug.LogError($"PHS_BATTERY_THROW_FAILED" + $"reason = item_data_missing" + $"player = {name}");
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
                    out _ )) //?®Í≥º ?òÏπò??SO HitEffectsÍ∞Ä Ï≤òÎ¶¨
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

            impact.InitializeAttackThrow(gameObject, batteryItemData);

            var throwVelocity = direction * batteryItemData.ThrowForce + Vector3.up * batteryItemData.UpwardForce;

            body.linearVelocity = throwVelocity;
            Debug.Log($"PHS_BATTERY_THROW_EXECUTED " + $"player={name} " + $"battery={batteryInstance.name} " + $"item={batteryItemData.ItemId} " + $"throwForce={batteryItemData.ThrowForce:F2} " +
              $"upwardForce={batteryItemData.UpwardForce:F2} " + $"rangeFeedback=on_first_impact", this);
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
            }//?§Ìä∏?åÌÅ¨ ?åÎ†à??Ï§ëÏóê???úÎ≤ÑÎß?Í≥µÍ≤©?êÏ†ï ?òÌñâ
            if(!TryGetHeldItemData(ItemUseType.Spray, out var itemData))//Í≥†Ï†ï ID ?Ä??UseType Í≥?SO ?∞Ïù¥??Í≤Ä??
            {
                Debug.LogWarning($"PHS_EXTINGUISHER_SPRAY_FAILED " + $"reason=item_data_or_use_type_invalid " + $"player={name}");
                return false;
            }

            if (extinguisherSprayOrigin == null)
            {
                Debug.LogError($"PHS_EXTINGUISHER_SPRAY_FAILED" + $"reason=spray_origin_missing " + $"player={name}");

                return false;
            }
            if(itemData.AttackRange <= 0f)
            {
                Debug.LogError($"PHS_EXTINGUISHER_SPRAY_FAILED " + $"reason=attack_distance_invalid " + $"player={name} " + $"item={itemData.ItemId} " + $"distance={itemData.AttackRange:F2}");
                return false;

            }
            if(itemData.AttackAngle <= 0f)
            {
                Debug.LogError($"PHS_EXTINGUISHER_SPRAY_FAILED " + $"reason=attack_angle_invalid " + $"player={name} " + $"item={itemData.ItemId} " + $"angle={itemData.AttackAngle:F2}");
                return false;
            }

            //?úÎ≤Ñ ?êÏ†ï Í∞ÑÍ≤© Í≤Ä??
            if (Time.time < nextExtinguisherDamageTime)
            {
                return false;
            }
            if(!CanUseHeldItemDurabiliy(itemData, out uint expectedRevision))
            {
                Debug.LogWarning($"PHS_EXTINGUISHER_SPRAY_FAILED " + $"reason=durability_unavailable " + $"player={name} " + $"item={itemData.ItemId}", this);

                return false;   
            }
            nextExtinguisherDamageTime = Time.time + itemData.Cooldown; //SO Ïø®Ì????¨Ïö©

            bool successfulUse = false;

            //Î∂ÑÏÇ¨ Î≤îÏúÑ ?êÏ†ï
            var hits = Physics.OverlapSphere(extinguisherSprayOrigin.position, itemData.AttackRange, itemData.TargetLayers, QueryTriggerInteraction.Collide); ;

            processedTargets.Clear();
            itemFeedbackTargetPositions.Clear();

            float halfAttackAngle = Mathf.Clamp(itemData.AttackAngle, 0f, 360f) * 0.5f;

            foreach (var hit in hits)
            {
                // SphereCast Í≤∞Í≥º??ColliderÍ∞Ä ?ÜÏúºÎ©?Ï≤òÎ¶¨?òÏ? ?äÎäî??
                if (hit == null)
                {
                    continue;
                }
                Vector3 feedbackPosition = hit.ClosestPoint(extinguisherSprayOrigin.position);

                Vector3 directionToHit = feedbackPosition - extinguisherSprayOrigin.position; 

                if(directionToHit.sqrMagnitude <= 0.0001f)
                {
                    directionToHit = hit.transform.position - extinguisherSprayOrigin.position; 
                }
                if(directionToHit.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }
                float targetAngle = Vector3.Angle(extinguisherSprayOrigin.forward, directionToHit.normalized); //?ïÎ©¥Í≥??Ä???¨Ïù¥ Í∞ÅÎèÑ Í≥ÑÏÇ∞

                if(targetAngle > halfAttackAngle)//Î∂ÄÏ±ÑÍº¥ Î≤îÏúÑÎ∞??Ä?ÅÏ? ?úÏô∏
                {
                    continue;
                }
                // ÎßûÏ? ColliderÍ∞Ä ?çÌïú ?§Ï†ú ?Ä???§Î∏å?ùÌä∏Î•?Ï∞æÎäî??
                // ?îÏû¨, ?∞ÏÜå ?ÑÏ∂ú, ?åÎ†à?¥Ïñ¥, Î™¨Ïä§???±ÏùÑ Î™®Îëê Ï∞æÏùÑ ???àÎã§.
                var targetObject = CombatHitResolver.ResolveTargetObject(hit.gameObject);
                
                if (targetObject == null)
                {
                    continue;
                }

                // ?åÌôîÍ∏∞Î? ?¨Ïö©?òÎäî ?êÍ∏∞ ?êÏã†?Ä ÎßûÏ? ?äÎäî??
                if (CombatHitResolver.IsSameTarget(targetObject, gameObject))   
                {
                    continue;
                }

                // ???§Î∏å?ùÌä∏??ColliderÍ∞Ä ?¨Îü¨ Í∞??àÏñ¥??
                // ??Î≤àÏùò Î∂ÑÏÇ¨ ?êÏ†ï?êÏÑú????Î≤àÎßå Ï≤òÎ¶¨?úÎã§.
                if (!processedTargets.Add(targetObject))
                {
                    continue;
                }

                // ?¥Î≤à ?åÌôîÍ∏??êÏ†ï??Í≥†Ïú† Î≤àÌò∏Î•?ÎßåÎì†??
                var requestSequence =
                    NextUtilityAttackSequence();

                // Î®ºÏ? ?îÏû¨, ?∞ÏÜå ?ÑÏ∂ú ?±Ïùò ?ÅÌò∏?ëÏö© ?Ä?ÅÏùÑ Í≤Ä?¨Ìïú??
                bool utilityAccepted = CombatHitResolver.TryResolveUtilityAttack(targetObject, gameObject, itemData.ItemId, requestSequence);

                bool repairApplied = utilityAccepted
                    || RepairResolver.TryRepair(itemData, targetObject, gameObject);
                if (repairApplied)
                {
                    successfulUse = true;
                    RecordAcceptedItemTarget(itemData.ItemId, targetObject, "fire_suppression", feedbackPosition, requestSequence);
                    continue;
                }

                // ?åÌôîÍ∏∞Í? Î∂ÑÏÇ¨?òÎäî Î∞©Ìñ•?ºÎ°ú ?âÎ∞±?úÌÇ®??
                var sprayDirection = extinguisherSprayOrigin.forward;

                bool effectApplied = ItemEffectResolver.ApplyEffects(itemData, targetObject, sprayDirection, gameObject);

                if (!effectApplied)//?ÑÎ¨¥ ?®Í≥º???ÅÏö©?òÏ? ?äÏúºÎ©??ºÎìú?©Î????úÏô∏
                {
                    continue;
                }

                successfulUse = true;
        
                RecordAcceptedItemTarget(itemData.ItemId, targetObject, "damage_or_knockback", feedbackPosition, requestSequence);
                

            }
            if (successfulUse)
            {
                if (!TryConsumeHeldItemDurability(itemData, expectedRevision))
                {
                    Debug.LogWarning($"PHS_EXTINGUISHER_DURABILITY_CONSUME_FAILED " + $"player={name} " + $"item={itemData.ItemId}", this);
                }
            }
            PublishItemUseFeedback(
                PHSItemUseFeedbackKind.FireExtinguisher,
                PHSItemUseFeedbackShape.Cast,
                extinguisherSprayOrigin.position,
                extinguisherSprayOrigin.forward,
                0f, itemData.AttackRange);
            Debug.Log(
                $"PHS_EXTINGUISHER_SPRAY" + $"player = {name}" + $"item={itemData.ItemId}" + $"distance = {itemData.AttackRange:F2}" + $"angle={itemData.AttackAngle:F2}" 
                + $"candidates = {processedTargets.Count}" +$"acceptedTargets = {itemFeedbackTargetPositions.Count}");
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
        private bool TryConsumeHeldItemDurability(UtilityItemDataSO itemData, uint expectedRevision)
        {
            if(itemData == null)
            {
                return false;
            }
            if (!itemData.UsesDurability)
            {
                return true;
            }
            int cost = itemData.DurabilityCostPerUse;

            if(cost <= 0)
            {
                return true;
            }
            var itemRecord = GetComponent<NetworkPlayerItemRecord>();

            if(itemRecord == null)
            {
                Debug.LogError($"PHS_ITEM_DURABILITY_CONSUME_FAILED " + $"reason=item_record_missing" + $"player={name}" + $"item={itemData.ItemId}", this);

                return false;   
            }
            if (!IsSpawned)
            {
                return true;
            }
            if (!IsServer)
            {
                Debug.LogError($"PHS_ITEM_DURABILITY_CONSUME_FAILED " + $"reason=server_required " + $"player={name} " + $"item={itemData.ItemId}", this);

                return false;
            }
            if (!itemRecord.TrySpendHeldItemDurabilityServer(itemData.ItemId, expectedRevision, cost))
            {
                return false;
            }

            if (itemRecord.CurrentDurability <= 0 && !TryEjectBrokenHeldItem(itemData))
            {
                Debug.LogError($"PHS_BROKEN_ITEM_PROCESS_FAILED player={name} item={itemData.ItemId}", this);
            }

            return true;
        }
        private bool TryEjectBrokenHeldItem(UtilityItemDataSO itemData)
        {
            if (itemData == null || itemHolder == null)
            {
                Debug.LogError($"PHS_BROKEN_ITEM_EJECT_FAILED reason=contract player={name}", this);
                return false;
            }

            if (IsSpawned && !IsServer)
            {
                Debug.LogError($"PHS_BROKEN_ITEM_EJECT_FAILED reason=server_required player={name} item={itemData.ItemId}", this);
                return false;
            }

            var throwOrigin = generalThrowOrigin != null ? generalThrowOrigin : transform;
            var direction = throwOrigin.forward.sqrMagnitude > 0.001f
                ? throwOrigin.forward.normalized
                : transform.forward;
            var spawnPosition = throwOrigin.position + direction * 0.25f;

            if (!itemHolder.TryCreateThrownItem(
                    spawnPosition,
                    Quaternion.LookRotation(direction),
                    out var brokenItem))
            {
                Debug.LogError($"PHS_BROKEN_ITEM_EJECT_FAILED reason=create_failed player={name} item={itemData.ItemId}", this);
                return false;
            }

            var body = brokenItem.GetComponent<Rigidbody>();
            var autoDespawn = brokenItem.GetComponent<BrokenItemAutoDespawn>();
            if (body == null || autoDespawn == null)
            {
                Debug.LogError($"PHS_BROKEN_ITEM_EJECT_FAILED reason=component_missing item={brokenItem.name}", brokenItem);
                RemoveFailedThrownObject(brokenItem);
                return false;
            }

            body.isKinematic = false;
            body.detectCollisions = true;
            body.linearVelocity = direction * brokenItemThrowForce + Vector3.up * brokenItemUpwardForce;
            autoDespawn.ArmServer(brokenItemDespawnDelay);

            Debug.Log($"PHS_BROKEN_ITEM_EJECTED player={name} item={itemData.ItemId} delay={brokenItemDespawnDelay:F2}", brokenItem);
            return true;
        }

        private bool CanUseHeldItemDurabiliy(UtilityItemDataSO itemData, out uint expectedRevision)
        {
            expectedRevision = 0U;

            if(itemData == null)
            {
                return false;
            }
            if(!itemData.UsesDurability || itemData.DurabilityCostPerUse <= 0)
            {
                return true;
            }
            if (!IsSpawned)
            {
                return false ;
            }
            if (!IsServer)
            {
                return false;
            }
            var itemRecord = GetComponent<NetworkPlayerItemRecord>();

            if(itemRecord == null || !itemRecord.IsSpawned)
            {
                Debug.LogError($"PHS_ITEM_DURABILITY_CHECK_FAILED " + $"reason=item_record_missing " + $"player={name} " + $"item={itemData.ItemId}", this);
                return false;
            }
            expectedRevision = itemRecord.Revision;

            return itemRecord.CanSpendHeldItemDurabilityServer(itemData.ItemId, expectedRevision, itemData.DurabilityCostPerUse);
        }
    }
}
