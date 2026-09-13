using System.Collections.Generic;
using UnityEngine;

public partial class PlatesCounter
{
    const float PLATE_OFFSET_Y = .1f;

    readonly List<GameObject> _plateVisualObjects = new(4);
    PlateObject _plateTemplate;

    void InitializeVisual()
    {
        ResolvePlateTemplate();
        SyncPlateVisual();
    }

    void UninitializeVisual()
    {
        _plateTemplate = null;
    }

    void SyncPlateVisual()
    {
        while (_plateVisualObjects.Count < _platesSpawnedAmount)
        {
            AddPlateVisual();
        }

        while (_plateVisualObjects.Count > _platesSpawnedAmount)
        {
            RemoveLastPlateVisual();
        }
    }

    void AddPlateVisual()
    {
        var plateObject = Instantiate(_plateTemplate, HoldPoint, false);
        if (!plateObject.gameObject.activeSelf)
        {
            plateObject.gameObject.SetActive(true);
        }

        var plateTransform = plateObject.transform;

        plateTransform.localPosition = new Vector3(0f, PLATE_OFFSET_Y * _plateVisualObjects.Count, 0f);
        plateTransform.localRotation = Quaternion.identity;
        plateTransform.localScale = Vector3.one;
        PlateObject.DisableVisualGameplayComponents(plateTransform.gameObject);

        _plateVisualObjects.Add(plateTransform.gameObject);
    }

    void RemoveLastPlateVisual()
    {
        if (_plateVisualObjects.Count == 0)
        {
            return;
        }

        var plateObject = _plateVisualObjects[_plateVisualObjects.Count - 1];
        _plateVisualObjects.RemoveAt(_plateVisualObjects.Count - 1);
        Destroy(plateObject);
    }

    void ResolvePlateTemplate()
    {
        if (_plateTemplate != null)
        {
            return;
        }

        var plateObjects = counterTopPoint.GetComponentsInChildren<PlateObject>(true);
        _plateTemplate = plateObjects[0];
        if (_plateTemplate.gameObject.activeSelf)
        {
            _plateTemplate.gameObject.SetActive(false);
        }
    }
}
