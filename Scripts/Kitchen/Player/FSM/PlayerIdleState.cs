public sealed class PlayerIdleState : State<PlayerBehaviour>
{
    public override void OnEnter()
    {
        base.OnEnter();

        Owner.SetWalking(false);
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        if (Owner.HasMoveInput)
        {
            Owner.Fsm.ActivateState<PlayerMoveState>();
            Owner.Move(Owner.MoveDirection, deltaTime);
        }
    }
}
