public sealed class PlayerDashState : State<PlayerBehaviour>
{
    public override void OnEnter()
    {
        base.OnEnter();

        Owner.SetWalking(true);
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);

        Owner.Move(Owner.DashDirection, Owner.DashSpeed, deltaTime);

        if (Time < Owner.DashDuration)
        {
            return;
        }

        if (Owner.HasMoveInput)
        {
            Owner.Fsm.ActivateState<PlayerMoveState>();
            return;
        }

        Owner.Fsm.ActivateState<PlayerIdleState>();
    }
}
