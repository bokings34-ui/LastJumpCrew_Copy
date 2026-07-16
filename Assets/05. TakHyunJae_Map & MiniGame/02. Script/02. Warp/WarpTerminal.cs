using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

public class WarpTerminal : MonoBehaviour,
    LastJumpCrew.ParkHanSol.Interaction.IInteractable,
    LastJumpCrew.Common.IInteractable
{
    [Header("상호작용 안내 문구")]
    [SerializeField] private string promptText = "워프 장치 가동하기";

    [Header("워프 진행 연결")]
    [SerializeField] private NetworkTravelConsoleController travelConsole;
    [SerializeField] private Renderer availabilityRenderer;
    [SerializeField] private Collider interactionCollider;

    private bool lastAvailability;

    public string InteractionPrompt => promptText;

    private void Awake()
    {
        if (travelConsole == null || availabilityRenderer == null || interactionCollider == null)
        {
            Debug.LogError($"[{nameof(WarpTerminal)}] Inspector 연결이 누락되었습니다.", this);
            enabled = false;
            return;
        }

        RefreshAvailability(true);
    }

    private void Update()
    {
        RefreshAvailability(false);
    }

    public bool CanInteract(LastJumpCrew.ParkHanSol.Interaction.IItemHolder itemHolder)
    {
        return IsWarpControllerAvailable()
            && travelConsole != null
            && travelConsole.CanExecute(itemHolder);
    }

    public void Interact(LastJumpCrew.ParkHanSol.Interaction.IItemHolder itemHolder)
    {
        if (CanInteract(itemHolder))
        {
            travelConsole.Execute(itemHolder);
        }
    }

    bool LastJumpCrew.Common.IInteractable.CanInteract(LastJumpCrew.Common.IItemHolder itemHolder)
    {
        return itemHolder is Component component
            && component is LastJumpCrew.ParkHanSol.Interaction.IItemHolder parkHanSolHolder
            && CanInteract(parkHanSolHolder);
    }

    void LastJumpCrew.Common.IInteractable.Interact(LastJumpCrew.Common.IItemHolder itemHolder)
    {
        if (itemHolder is Component component
            && component is LastJumpCrew.ParkHanSol.Interaction.IItemHolder parkHanSolHolder)
        {
            Interact(parkHanSolHolder);
        }
    }

    private bool IsWarpControllerAvailable()
    {
        var runFlow = NetworkRunFlowCoordinator.Instance;
        return runFlow != null && runFlow.Phase == NetworkRunPhase.WarpReady;
    }

    private void RefreshAvailability(bool force)
    {
        var isAvailable = IsWarpControllerAvailable();
        if (!force && isAvailable == lastAvailability)
        {
            return;
        }

        lastAvailability = isAvailable;
        availabilityRenderer.enabled = isAvailable;
        interactionCollider.enabled = isAvailable;
    }
}
