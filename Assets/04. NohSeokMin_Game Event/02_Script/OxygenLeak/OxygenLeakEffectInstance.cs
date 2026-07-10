using System;
using System.Collections.Generic;
using UnityEngine;
using LastJumpCrew.Common;

namespace SM
{
    public class OxygenLeakEffectInstance : MonoBehaviour, IInteractable
    {
        private float _outerPullRadius;
        private float _innerDamageRadius;
        private float _pullSpeed;
        private int _centerDamage;
        private float _damageTickInterval;
        private float _maxRepairProgress;

        private float _repairProgress;
        private float _damageTimer;

        public bool IsSealed { get; private set; }
        public event Action<OxygenLeakEffectInstance> OnSealed;

        private readonly HashSet<Transform> _playersInRange = new HashSet<Transform>();

        public void Activate(OxygenLeakEventDataSO data)
        {
            _outerPullRadius = data.outerPullRadius;
            _innerDamageRadius = data.innerDamageRadius;
            _pullSpeed = data.pullSpeed;
            _centerDamage = data.centerDamage;
            _damageTickInterval = data.damageTickInterval;
            _maxRepairProgress = data.maxRepairProgress;

            _repairProgress = 0f;
            _damageTimer = 0f;
            IsSealed = false;

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            _playersInRange.Clear();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsSealed) return;

            FindPlayersInRange();
            PullPlayers();
            ApplyCenterDamage();
        }

        private void FindPlayersInRange()
        {
            _playersInRange.Clear();

            var hits = Physics.OverlapSphere(transform.position, _outerPullRadius);

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponentInParent<IDamageable>();

                if (damageable != null && damageable.IsAlive)
                {
                    _playersInRange.Add(hit.transform);
                }
            }
        }

        private void PullPlayers()
        {
            // TODO :: Player 이동 방식(CharacterController)에 따라 바뀔 수 있음
            foreach (var player in _playersInRange)
            {
                player.position = Vector3.MoveTowards(
                    player.position,
                    transform.position,
                    _pullSpeed * Time.deltaTime);
            }
        }

        private void ApplyCenterDamage()
        {
            _damageTimer += Time.deltaTime;
            if (_damageTimer < _damageTickInterval) return;
            _damageTimer = 0f;

            foreach (var player in _playersInRange)
            {
                float dist = Vector3.Distance(transform.position, player.position);

                if (dist <= _innerDamageRadius)
                {
                    var damageable = player.GetComponentInParent<IDamageable>();

                    if (damageable != null && damageable.IsAlive)
                    {
                        damageable.ApplyDamage(_centerDamage, gameObject);
                    }
                }
            }
        }

        public string InteractionPrompt { get { return "렌치 필요"; } }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return false;
        }

        public void Interact(IItemHolder itemHolder)
        {
        }

        public void ApplyRepair(float amount)
        {
            if (IsSealed) return;

            _repairProgress += amount;

            if (_repairProgress >= _maxRepairProgress)
            {
                IsSealed = true;
                OnSealed?.Invoke(this);
            }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.1f, 0.9f); // 선명한 보라색
            Gizmos.DrawWireSphere(transform.position, _outerPullRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _innerDamageRadius);
        }
    }
}