using UnityEngine;

// 무언가를 들 수 있는 슬롯/주체
public interface IHolder
{
    // 현재 이 Holder가 보유 중인 오브젝트입니다.
    IHoldable CurrentHoldable { get; }

    // Holdable이 붙을 기준 위치입니다.
    Transform HoldPoint { get; }

    // 현재 보유 중인 Holdable이 있는지 여부입니다.
    bool HasHoldable { get; }

    // 보유 관계를 변경하기 전에 수용 가능 여부를 판정합니다.
    bool CanAccept(IHoldable holdable);

    // CanAccept가 통과한 대상만 등록합니다. 이 메서드에서 다시 거절하지 않습니다.
    // 양방향 참조와 Transform 이동은 HoldableTransferLogic이 함께 갱신합니다.
    void Attach(IHoldable holdable);

    // 이동/Release에서 실제로 제거할 때만 호출합니다. 다른 대상이면 아무것도 하지 않습니다.
    // 슬롯에서 실행하던 작업은 중단할 수 있지만 누적 진행량은 보존합니다.
    // Holdable 쪽 참조 변경은 이동/Release 호출자가 완료합니다.
    void Detach(IHoldable holdable);
}
