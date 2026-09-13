using UnityEngine;

public class TrashCounter : BaseCounter
{
    public override void Interact(PlayerBehaviour player)
    {
        if (!player.HasHoldable)
        {
            return;
        }

        if (player.CurrentHoldable is ToolObject toolObject)
        {
            if (toolObject.Ingredient == null)
            {
                return;
            }

            toolObject.ResetContent();
        }
        else
        {
            player.CurrentHoldable.Release();
        }

        SoundManager.Instance.PlaySound(SoundKeys.TRASH, transform.position);
    }

}
