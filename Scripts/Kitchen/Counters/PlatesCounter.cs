using UnityEngine;

public partial class PlatesCounter : BaseCounter
{
    [SerializeField] int _platesSpawnedAmountMax = 4;
    [SerializeField] float _spawnPlateTimerMax = 4f;
    
    KitchenPlateModule _plateModule;
    int _platesSpawnedAmount;
    float _spawnPlateTimer;
    bool _isSpawnTimerActive;

    protected override void OnInitialize(KitchenGameContext context)
    {
        _plateModule = context.GetModule<KitchenPlateModule>();
        InitializeVisual();
        StopSpawnTimer();
        SetPlatesSpawnedAmount(_platesSpawnedAmountMax);
    }

    protected override void OnUninitialize()
    {
        SetPlatesSpawnedAmount(0);
        StopSpawnTimer();
        UninitializeVisual();
        _plateModule = null;
    }

    void Update()
    {
        if (!_isSpawnTimerActive || !Context.IsGamePlaying || _platesSpawnedAmount >= _platesSpawnedAmountMax)
        {
            return;
        }

        _spawnPlateTimer += Time.deltaTime;
        if (_spawnPlateTimer <= _spawnPlateTimerMax)
        {
            return;
        }

        _spawnPlateTimer = 0f;
        SetPlatesSpawnedAmount(_platesSpawnedAmount + 1);

        if (_platesSpawnedAmount >= _platesSpawnedAmountMax)
        {
            StopSpawnTimer();
        }
    }

    public override void Interact(PlayerBehaviour player)
    {
        if (!Context.IsGamePlaying || player.HasHoldable || _platesSpawnedAmount <= 0)
        {
            return;
        }

        _plateModule.SpawnPlateObject(_plateTemplate, player);
        RemoveLastPlateVisual();
        _platesSpawnedAmount--;
        StartSpawnTimer();
    }

    void SetPlatesSpawnedAmount(int amount)
    {
        amount = Mathf.Clamp(amount, 0, _platesSpawnedAmountMax);

        while (_platesSpawnedAmount < amount)
        {
            _platesSpawnedAmount++;
            AddPlateVisual();
        }

        while (_platesSpawnedAmount > amount)
        {
            _platesSpawnedAmount--;
            RemoveLastPlateVisual();
        }
    }

    void StartSpawnTimer()
    {
        if (_isSpawnTimerActive)
        {
            return;
        }

        _spawnPlateTimer = 0f;
        _isSpawnTimerActive = true;
    }

    void StopSpawnTimer()
    {
        _spawnPlateTimer = 0f;
        _isSpawnTimerActive = false;
    }
}
