using UnityEngine;

public static class HoldableTransferLogic
{
    public static bool TryMoveTo(IHoldable holdable, IHolder holder)
    {
        if (holdable == null || holder == null)
        {
            return false;
        }

        if (ReferenceEquals(holdable.Holder, holder))
        {
            // 실제 이동이 없는 요청은 실패로 보고 기존 관계를 유지합니다.
            return false;
        }

        if (!holder.CanAccept(holdable))
        {
            return false;
        }

        holdable.Holder?.Detach(holdable);
        holdable.SetHolder(holder);
        holder.Attach(holdable);

        var transform = holdable.Transform;
        var worldRotation = transform.rotation;
        transform.SetParent(holder.HoldPoint, false);
        transform.localPosition = Vector3.zero;
        transform.rotation = worldRotation;
        return true;
    }
}
