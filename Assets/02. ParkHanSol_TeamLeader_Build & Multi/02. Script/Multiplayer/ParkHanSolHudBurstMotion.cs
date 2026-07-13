using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolHudBurstMotion : MonoBehaviour
    {
        [SerializeField] private Image[] fragments;
        [SerializeField, Min(0.01f)] private float burstDuration = 0.24f;
        [SerializeField, Min(1f)] private float burstDistance = 38f;
        [SerializeField] private Color primaryColor = new(1f, 0.28f, 0.2f, 1f);
        [SerializeField] private Color secondaryColor = new(1f, 0.82f, 0.3f, 1f);

        public void PlayBurst()
        {
            if (fragments == null || fragments.Length == 0)
            {
                Debug.LogError($"PHS_HUD_BURST_MOTION_FAILED reason=fragments_missing target={name}");
                return;
            }

            for (var i = 0; i < fragments.Length; i++)
            {
                var fragment = fragments[i];
                if (fragment == null)
                {
                    continue;
                }

                var fragmentTransform = fragment.rectTransform;
                var direction = new Vector2(Random.Range(-1f, 1f), Random.Range(0.15f, 1f)).normalized * Random.Range(burstDistance * 0.55f, burstDistance);
                fragment.DOKill();
                fragmentTransform.DOKill();
                fragment.gameObject.SetActive(true);
                fragmentTransform.anchoredPosition = Vector2.zero;
                fragmentTransform.localScale = Vector3.one * Random.Range(0.55f, 1f);
                fragmentTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 45f));
                fragment.color = i % 3 == 0 ? Color.white : (i % 2 == 0 ? primaryColor : secondaryColor);

                fragmentTransform.DOAnchorPos(direction, burstDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject);
                fragmentTransform.DOScale(0.25f, burstDuration)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true)
                    .SetLink(gameObject);
                fragmentTransform.DORotate(new Vector3(0f, 0f, Random.Range(90f, 220f)), burstDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetLink(gameObject);
                fragment.DOFade(0f, burstDuration)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() => fragment.gameObject.SetActive(false));
            }
        }

        private void OnDestroy()
        {
            if (fragments == null)
            {
                return;
            }

            for (var i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] == null)
                {
                    continue;
                }

                fragments[i].DOKill();
                fragments[i].rectTransform.DOKill();
            }
        }
    }
}
