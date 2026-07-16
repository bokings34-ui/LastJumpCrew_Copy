using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    //우클릭으로 버리는 일반 아이템의 충돌 처리
    public sealed class ThorwnItemImpact : NetworkBehaviour
    {
        [Header("Knockback")]
        [SerializeField, Min(0f)]
        private float impactKnockbackForce = 1.5f; //넉백 파워

        [SerializeField, Min(0f)]
        private float minimumImpactSpeed = 1.5f; //최소 충돌 속도
        [SerializeField, Min(0f)]
        private float ownerCollisionIgnoreTime = 0.25f; //투척 직후 충돌을 무시하는 시간

        private GameObject attacker; //아이템 던진 플레이어

        private float throwStartTime; // 아이템 던진 시각

        private bool isThrown; //아이템 투척 했는지 

        public void InitialzeThrow(GameObject throwAttacker)
        {
            if (!IsServer)//충돌 판정 + 넉백 적용은 서버 담당
            {
                return;
            }

            attacker = throwAttacker;
            throwStartTime = Time.time; 
            isThrown = true;

            Debug.Log($"PHS_THROW_IMPACT_READY " + $"item={name}");
        }
        private void OnCillisionEnter(Collision collision)
        {
            if (!IsServer || !isThrown)
            {
                return;
            } 

            var impactSpeed = collision.relativeVelocity.magnitude; //너무 느리면 판정 제외

            if(impactSpeed < minimumImpactSpeed)
            {
                return;
            }

            var target = collision.collider.gameObject; //실제 충돌한 Collider의 GameObject

            if (attacker != null && Time.time - throwStartTime < ownerCollisionIgnoreTime && target.transform.root == attacker.transform.root)
            {
                return;
            }
            var knockbackable = target.GetComponentInParent<IKnockbackable>(); //충돌 대상한테서 넉백 인터페이스 찾기

            if(knockbackable == null)
            {
                return ;
            }
            if (!knockbackable.CanReceiveKnockback)
            {
                return ;
            }

            var direction = collision.relativeVelocity.sqrMagnitude > 0.001f ? collision.relativeVelocity.normalized : transform.forward;

            knockbackable.ApplyKnockback(direction, impactKnockbackForce, attacker);

            Debug.Log($"PHS_THROW_IMPACT_HIT" + $"item{name}" + $"target = {target.name}" + $"speed = {impactSpeed:F2}");
            //한번 맞힌 뒤에는 같은 투척으로 추가 넉백 X
            isThrown = false;
        }
    }
}
