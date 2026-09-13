using UnityEngine;

public class ClearCounter : BaseCounter
{
    public override Transform HoldPoint => CurrentHoldable is ToolObject ? _toolHoldPoint : base.HoldPoint;

    [SerializeField] Transform _toolHoldPoint;

    public override bool CanAccept(IHoldable holdable)
    {
        return base.CanAccept(holdable) && (holdable is not ToolObject || _toolHoldPoint != null);
    }

    public override void Interact(PlayerBehaviour player)
    {
        if (!HasHoldable)
        {
            HoldableTransferLogic.TryMoveTo(player.CurrentHoldable, this);
            return;
        }

        if (!player.HasHoldable)
        {
            HoldableTransferLogic.TryMoveTo(CurrentHoldable, player);
            return;
        }

        var playerHoldable = player.CurrentHoldable;
        if (playerHoldable is PlateObject playerPlate)
        {
            KitchenPlateTransferLogic.TryTransferToPlate(playerPlate, CurrentHoldable);
        }
        else if (CurrentHoldable is PlateObject counterPlate)
        {
            KitchenPlateTransferLogic.TryTransferToPlate(counterPlate, playerHoldable);
        }
        else if (CurrentHoldable is ToolObject tool && playerHoldable is IngredientObject ingredient &&
                 KitchenIngredientLogic.CanTrackAction(tool.ActionType) &&
                 !ingredient.IsActionProcessing && !ingredient.IsBurned &&
                 !ingredient.HasCompletedAction(tool.ActionType))
        {
            HoldableTransferLogic.TryMoveTo(ingredient, tool);
        }
    }
}
