using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSHullBreachRepairSite : MonoBehaviour, IHullBreachRepairSite
    {
        [SerializeField] private string siteId = "hull_breach_site";
        [SerializeField] private Transform repairPoint;
        [SerializeField] private Collider repairBounds;
        [SerializeField] private Transform presentationRoot;

        private bool runtimeActive;

        public string SiteId => siteId?.Trim() ?? string.Empty;
        public Vector3 RepairPosition => repairPoint == null
            ? transform.position
            : repairPoint.position;
        public bool IsAvailable => !runtimeActive;

        public bool TryActivate(out string reason)
        {
            if (!TryValidate(out reason) || runtimeActive)
            {
                reason ??= "site_already_active";
                return false;
            }

            runtimeActive = true;
            presentationRoot.gameObject.SetActive(true);
            reason = null;
            return true;
        }

        public void Deactivate()
        {
            runtimeActive = false;
            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(false);
            }
        }

        public void SetPresentationVisible(bool visible)
        {
            if (presentationRoot != null)
            {
                presentationRoot.gameObject.SetActive(visible);
            }
        }

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(SiteId))
            {
                reason = "site_id_missing";
                return false;
            }

            if (repairPoint == null)
            {
                reason = "repair_point_missing";
                return false;
            }

            if (repairBounds == null || !repairBounds.isTrigger)
            {
                reason = "repair_bounds_trigger_missing";
                return false;
            }

            if (presentationRoot == null)
            {
                reason = "presentation_root_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private void Awake()
        {
            Deactivate();
        }
    }

    [DisallowMultipleComponent]
    public sealed class PHSHullBreachRepairSiteProvider :
        MonoBehaviour,
        IHullBreachRepairSiteProvider
    {
        [SerializeField] private PHSHullBreachRepairSite[] sites =
            System.Array.Empty<PHSHullBreachRepairSite>();

        private readonly List<PHSHullBreachRepairSite> availableSites = new();

        public bool TryAcquireSite(
            out IHullBreachRepairSite site,
            out string reason)
        {
            site = null;
            if (!TryValidate(out reason))
            {
                return false;
            }

            availableSites.Clear();
            foreach (var candidate in sites)
            {
                if (candidate.IsAvailable)
                {
                    availableSites.Add(candidate);
                }
            }

            if (availableSites.Count == 0)
            {
                reason = "site_unavailable";
                return false;
            }

            var selected = availableSites[Random.Range(0, availableSites.Count)];
            if (!selected.TryActivate(out reason))
            {
                return false;
            }

            site = selected;
            reason = null;
            return true;
        }

        public bool TryValidate(out string reason)
        {
            if (sites == null || sites.Length == 0)
            {
                reason = "sites_missing";
                return false;
            }

            var siteIds = new HashSet<string>();
            foreach (var site in sites)
            {
                if (site == null)
                {
                    reason = "site_reference_missing";
                    return false;
                }

                if (!site.TryValidate(out var siteReason))
                {
                    reason = $"site_invalid:{siteReason}";
                    return false;
                }

                if (!siteIds.Add(site.SiteId))
                {
                    reason = $"site_id_duplicate:{site.SiteId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }
    }
}
