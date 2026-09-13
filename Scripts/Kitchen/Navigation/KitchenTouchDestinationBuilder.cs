using UnityEngine;

public sealed class KitchenTouchDestinationBuilder
{
    const float GROUND_PLANE_Y = 0f;
    const int RAYCAST_HIT_CAPACITY = 16;

    readonly RaycastHit[] _raycastHits = new RaycastHit[RAYCAST_HIT_CAPACITY];

    public bool TryBuild(
        Camera camera,
        KitchenTouchCommand command,
        KitchenNavigationMap navigationMap,
        out KitchenNavigationDestination destination)
    {
        destination = default;
        if (camera == null || !navigationMap.IsReady)
        {
            return false;
        }

        var ray = camera.ScreenPointToRay(command.ScreenPosition);
        if (TryGetCounterFromRay(ray, out var targetCounter))
        {
            destination = KitchenNavigationDestination.InteractWith(
                navigationMap.WorldToCell(targetCounter.transform.position),
                targetCounter);
            return true;
        }

        if (!TryGetGroundPosition(ray, out var groundPosition))
        {
            return false;
        }

        var targetCell = navigationMap.WorldToCell(groundPosition);
        destination = navigationMap.TryGetCounter(targetCell, out targetCounter)
            ? KitchenNavigationDestination.InteractWith(targetCell, targetCounter)
            : KitchenNavigationDestination.MoveTo(targetCell);
        return true;
    }

    bool TryGetCounterFromRay(Ray ray, out BaseCounter counter)
    {
        counter = null;
        var hitCount = Physics.RaycastNonAlloc(ray, _raycastHits);
        var closestDistance = float.MaxValue;

        for (var i = 0; i < hitCount; i++)
        {
            var hit = _raycastHits[i];
            if (hit.distance >= closestDistance)
            {
                continue;
            }

            if (!hit.transform.TryGetComponent(out BaseCounter hitCounter))
            {
                hitCounter = hit.transform.GetComponentInParent<BaseCounter>();
            }

            if (hitCounter == null)
            {
                continue;
            }

            counter = hitCounter;
            closestDistance = hit.distance;
        }

        return counter != null;
    }

    static bool TryGetGroundPosition(Ray ray, out Vector3 position)
    {
        var plane = new Plane(Vector3.up, new Vector3(0f, GROUND_PLANE_Y, 0f));
        if (!plane.Raycast(ray, out var enter))
        {
            position = default;
            return false;
        }

        position = ray.GetPoint(enter);
        return true;
    }
}
