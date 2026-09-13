using System;

public sealed class KitchenDeliveryModuleEvents
{
    public event Action<KitchenMenuData> OnMenuSpawned;
    public event Action OnMenuCompleted;
    public event Action<KitchenMenuData> OnMenuExpired;
    public event Action<GameReportData> OnScoreChanged;

    public void InvokeMenuSpawned(KitchenMenuData menuData)
    {
        OnMenuSpawned?.Invoke(menuData);
    }

    public void InvokeMenuCompleted()
    {
        OnMenuCompleted?.Invoke();
    }

    public void InvokeMenuExpired(KitchenMenuData menuData)
    {
        OnMenuExpired?.Invoke(menuData);
    }

    public void InvokeScoreChanged(GameReportData gameReportData)
    {
        OnScoreChanged?.Invoke(gameReportData);
    }
}
