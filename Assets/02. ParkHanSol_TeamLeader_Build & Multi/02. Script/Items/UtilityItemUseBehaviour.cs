using LastJumpCrew.Common;
using System.Collections;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    // 아이템 사용 기능의 공통 부모다. 개별 아이템은 시작/종료 훅만 override해서 확장한다.
    // 입력, 쿨다운, 사용 시간 흐름은 여기서 통일하고 아이템별 효과만 하위 클래스가 맡는다.
    public abstract class UtilityItemUseBehaviour : MonoBehaviour, IUsableItem
    {
        [SerializeField, Min(0f)] private float useDuration = 0.25f;
        [SerializeField, Min(0f)] private float cooldown = 0.2f;

        private bool isUsing;
        private float nextUsableTime;
        private Coroutine useRoutine;

        protected IItemHolder CurrentUser { get; private set; }
        protected IInteractable CurrentTarget { get; private set; }
        protected bool IsUsing => isUsing;

        public bool CanUse(IItemHolder user, IInteractable target)
        {
            if (user == null)
            {
                Debug.LogWarning($"PHS_ITEM_USE_FAILED reason=user_missing item={name}");
                return false;
            }

            if (isUsing)
            {
                Debug.LogWarning($"PHS_ITEM_USE_FAILED reason=already_using item={name}");
                return false;
            }

            if (Time.time < nextUsableTime)
            {
                Debug.LogWarning($"PHS_ITEM_USE_FAILED reason=cooldown item={name}");
                return false;
            }

            return CanUseItem(user, target);
        }

        public void Use(IItemHolder user, IInteractable target)
        {
            if (!CanUse(user, target))
            {
                return;
            }

            BeginUse(user, target);
        }

        protected virtual bool CanUseItem(IItemHolder user, IInteractable target)
        {
            return true;
        }

        // 사용 시작 시점 효과다. 즉시 판정이 필요한 아이템은 여기서 처리한다.
        protected virtual void OnUseStarted(IItemHolder user, IInteractable target)
        {
        }

        // useDuration 이후 마무리 효과다. 지속 사용 연출/판정이 필요하면 여기서 처리한다.
        protected virtual void OnUseFinished(IItemHolder user, IInteractable target)
        {
        }

        // Raycast 대상이 공용 IInteractable만 노출해도, 실제 컴포넌트에서 아이템 전용 계약을 찾는다.
        protected bool TryGetTarget<TTarget>(IInteractable target, out TTarget typedTarget)
        {
            if (target is TTarget directTarget)
            {
                typedTarget = directTarget;
                return true;
            }

            if (target is Component targetComponent)
            {
                foreach (var component in targetComponent.GetComponentsInParent<Component>(true))
                {
                    if (component is TTarget componentTarget)
                    {
                        typedTarget = componentTarget;
                        return true;
                    }
                }
            }

            typedTarget = default;
            return false;
        }

        private void BeginUse(IItemHolder user, IInteractable target)
        {
            if (useRoutine != null)
            {
                StopCoroutine(useRoutine);
            }

            CurrentUser = user;
            CurrentTarget = target;
            isUsing = true;
            nextUsableTime = Time.time + cooldown;

            OnUseStarted(user, target);

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                EndUse();
                return;
            }

            if (Mathf.Approximately(useDuration, 0f))
            {
                EndUse();
                return;
            }

            useRoutine = StartCoroutine(UseRoutine());
        }

        private IEnumerator UseRoutine()
        {
            yield return new WaitForSeconds(useDuration);
            EndUse();
        }

        private void EndUse()
        {
            var user = CurrentUser;
            var target = CurrentTarget;

            useRoutine = null;
            isUsing = false;
            CurrentUser = null;
            CurrentTarget = null;

            OnUseFinished(user, target);
        }
    }
}
