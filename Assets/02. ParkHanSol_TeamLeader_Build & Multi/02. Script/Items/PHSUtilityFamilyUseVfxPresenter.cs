using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSUtilityFamilyUseVfxPresenter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem wrenchUseEffect;
        [SerializeField] private GameObject extinguisherFirstPersonEffectRoot;
        [SerializeField] private GameObject extinguisherWorldEffectRoot;
        [SerializeField, Min(0.05f)] private float extinguisherDuration = 0.65f;

        private NetworkObject networkObject;
        private Coroutine extinguisherStopRoutine;
        private GameObject activeExtinguisherRoot;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            StopExtinguisherImmediate(extinguisherFirstPersonEffectRoot);
            StopExtinguisherImmediate(extinguisherWorldEffectRoot);
            StopParticleImmediate(wrenchUseEffect);
        }

        public void Play(PHSItemUseActionKind actionKind)
        {
            switch (actionKind)
            {
                case PHSItemUseActionKind.Wrench:
                    RestartParticle(wrenchUseEffect);
                    break;
                case PHSItemUseActionKind.FireExtinguisher:
                    PlayExtinguisher();
                    break;
            }
        }

        private void PlayExtinguisher()
        {
            var useFirstPerson = networkObject == null
                || !networkObject.IsSpawned
                || networkObject.IsOwner;
            var activeRoot = useFirstPerson
                ? extinguisherFirstPersonEffectRoot
                : extinguisherWorldEffectRoot;
            var inactiveRoot = useFirstPerson
                ? extinguisherWorldEffectRoot
                : extinguisherFirstPersonEffectRoot;

            StopExtinguisherImmediate(inactiveRoot);
            if (activeExtinguisherRoot != activeRoot ||
                !HasPlayingParticle(activeRoot))
            {
                RestartParticles(activeRoot);
            }

            activeExtinguisherRoot = activeRoot;
            if (extinguisherStopRoutine != null)
            {
                StopCoroutine(extinguisherStopRoutine);
            }

            extinguisherStopRoutine = StartCoroutine(
                StopExtinguisherAfterDelay(activeRoot));
        }

        private IEnumerator StopExtinguisherAfterDelay(GameObject effectRoot)
        {
            yield return new WaitForSeconds(extinguisherDuration);
            StopExtinguisherImmediate(effectRoot);
            if (activeExtinguisherRoot == effectRoot)
            {
                activeExtinguisherRoot = null;
            }

            extinguisherStopRoutine = null;
        }

        private static bool HasPlayingParticle(GameObject effectRoot)
        {
            if (effectRoot == null)
            {
                return false;
            }

            foreach (var particle in
                     effectRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle != null && particle.isPlaying)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RestartParticles(GameObject effectRoot)
        {
            if (effectRoot == null)
            {
                return;
            }

            foreach (var particle in effectRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                RestartParticle(particle);
            }
        }

        private static void RestartParticle(ParticleSystem particle)
        {
            if (particle == null)
            {
                return;
            }

            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }

        private static void StopExtinguisherImmediate(GameObject effectRoot)
        {
            if (effectRoot == null)
            {
                return;
            }

            foreach (var particle in effectRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                StopParticleImmediate(particle);
            }
        }

        private static void StopParticleImmediate(ParticleSystem particle)
        {
            if (particle != null)
            {
                particle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnDisable()
        {
            if (extinguisherStopRoutine != null)
            {
                StopCoroutine(extinguisherStopRoutine);
                extinguisherStopRoutine = null;
            }

            StopExtinguisherImmediate(extinguisherFirstPersonEffectRoot);
            StopExtinguisherImmediate(extinguisherWorldEffectRoot);
            StopParticleImmediate(wrenchUseEffect);
            activeExtinguisherRoot = null;
        }
    }
}
