using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 인터페이스(계약) 기준 서비스 등록/조회. 완성된 기능 + Mock→실구현 교체의 핵심.
    /// 등록/재등록은 라이프사이클 이벤트(스폰·씬 전환·접속 종료) 때만. 플레이 중 매 프레임 접근 금지.
    /// </summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> services = new();

        /// <summary>계약 T의 구현을 등록. 이미 있으면 덮어쓴다(Mock → 실구현 교체).</summary>
        public void Register<T>(T impl) where T : class
        {
            if (impl == null)
            {
                Debug.LogError($"[ServiceRegistry] {typeof(T).Name} null 등록 시도");
                return;
            }
            services[typeof(T)] = impl;
        }

        /// <summary>계약 T의 구현을 조회. 미등록이면 에러 로그 후 null.</summary>
        public T Get<T>() where T : class
        {
            if (services.TryGetValue(typeof(T), out var v)) return (T)v;
            Debug.LogError($"[ServiceRegistry] {typeof(T).Name} 미등록");
            return null;
        }

        /// <summary>계약 T의 구현을 안전 조회(누락 방어).</summary>
        public bool TryGet<T>(out T impl) where T : class
        {
            if (services.TryGetValue(typeof(T), out var v))
            {
                impl = (T)v;
                return true;
            }
            impl = null;
            return false;
        }

        /// <summary>
        /// Removes a service only when the caller still owns the current registration.
        /// This prevents a despawning network replica from removing a newer replacement.
        /// </summary>
        public bool TryUnregister<T>(T expected) where T : class
        {
            if (expected == null
                || !services.TryGetValue(typeof(T), out var current)
                || !ReferenceEquals(current, expected))
            {
                return false;
            }

            return services.Remove(typeof(T));
        }
    }
}
