using UnityEngine;

public sealed class PlayerMoveState : State<PlayerBehaviour>
{
    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        Vector3 moveDir = Owner.MoveDirection;
        if (moveDir == Vector3.zero)
        {
            Owner.Fsm.ActivateState<PlayerIdleState>();
            return;
        }

        Owner.Move(moveDir, deltaTime);
    }
}
