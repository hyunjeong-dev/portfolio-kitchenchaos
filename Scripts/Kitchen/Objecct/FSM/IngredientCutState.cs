public sealed class IngredientCutState : IngredientActionState
{
    const float CUT_EFFECT_INTERVAL = .2f;

    float _cutEffectTimer;

    public override void OnEnter()
    {
        base.OnEnter();

        _cutEffectTimer = CUT_EFFECT_INTERVAL;
        Owner.OnActionEffect(ActionType);
    }

    protected override void OnActionTick(float deltaTime)
    {
        _cutEffectTimer -= deltaTime;
        if (_cutEffectTimer > 0f)
        {
            return;
        }

        _cutEffectTimer = CUT_EFFECT_INTERVAL;
        Owner.OnActionEffect(ActionType);
    }

    protected override void CompleteAction()
    {
        Owner.CompleteCuttingAction();
    }
}
