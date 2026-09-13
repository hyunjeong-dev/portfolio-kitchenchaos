using UnityEngine;

// 들릴 수 있는 오브젝트
public interface IHoldable
{
    // 이 오브젝트를 현재 보유 중인 Holder입니다.
    IHolder Holder { get; }

    Transform Transform { get; }

    // 이동은 HoldableTransferLogic을 통해 수행합니다. 여기서는 보유자와 자체 표시만 갱신합니다.
    void SetHolder(IHolder holder);

    // 현재 Holder에서 분리하고 비활성화/풀 반환 등의 정리 처리를 수행합니다.
    void Release();
}
