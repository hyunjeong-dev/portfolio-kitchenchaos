using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Generated.GameData;

public partial class ContainerCounter : BaseCounter
{
    [SerializeField] string ingredientCode;
    [SerializeField] SpriteRenderer _topSpriteRenderer;

    IngredientPoolModule _poolModule;
    CancellationTokenSource _dispenseCancellationTokenSource;
    bool _isDispensing;

    public static int ComparePosition(ContainerCounter left, ContainerCounter right)
    {
        if (left == null && right == null)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        var leftPosition = left.transform.position;
        var rightPosition = right.transform.position;
        var zCompare = rightPosition.z.CompareTo(leftPosition.z);
        if (zCompare != 0)
        {
            return zCompare;
        }

        return leftPosition.x.CompareTo(rightPosition.x);
    }

    protected override void OnInitialize(KitchenGameContext context)
    {
        _poolModule = context.GetModule<IngredientPoolModule>();
        _dispenseCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
    }

    protected override void OnUninitialize()
    {
        CancelDispense();
        _poolModule = null;
    }
    
    public void SetIngredient(string code)
    {
        SetIngredient(code, null);
    }

    public void SetIngredient(string code, Sprite spriteIcon)
    {
        ingredientCode = code;

        if (_topSpriteRenderer == null)
        {
            Debug.LogError("[ContainerCounter] Top SpriteRenderer is not found.");
            return;
        }

        _topSpriteRenderer.sprite = spriteIcon;
    }

    public override void Interact(PlayerBehaviour player)
    {
        if (!IsInitialized || player == null || player.HasHoldable || _isDispensing || _poolModule == null)
        {
            return;
        }

        if (!TryGetIngredientData(out var ingredientData))
        {
            return;
        }

        DispenseIngredientAsync(
                ingredientData,
                player,
                _dispenseCancellationTokenSource.Token)
            .Forget(Debug.LogException);
    }

    async UniTask DispenseIngredientAsync(
        IngredientInfoData ingredientData,
        PlayerBehaviour player,
        CancellationToken cancellationToken)
    {
        _isDispensing = true;
        try
        {
            await PlayOpenCloseVisualAsync(cancellationToken);

            if (!IsInitialized || !Context.IsPlayable || player == null || player.HasHoldable || _poolModule == null)
            {
                return;
            }

            _poolModule.GetIngredientObject(ingredientData, player);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_dispenseCancellationTokenSource != null &&
                _dispenseCancellationTokenSource.Token == cancellationToken)
            {
                _isDispensing = false;
            }
        }
    }

    void CancelDispense()
    {
        _isDispensing = false;
        if (_dispenseCancellationTokenSource == null)
        {
            return;
        }

        _dispenseCancellationTokenSource.Cancel();
        _dispenseCancellationTokenSource.Dispose();
        _dispenseCancellationTokenSource = null;
    }

    bool TryGetIngredientData(out IngredientInfoData ingredientData)
    {
        var code = !string.IsNullOrWhiteSpace(ingredientCode) ? ingredientCode : GetIngredientCodeFromName();
        ingredientData = GameData.Instance.GetIngredientInfoDataByCode(code);
        if (ingredientData != null)
        {
            return true;
        }

        Debug.LogError($"[ContainerCounter] Ingredient GameData not found. code: {code}");
        return false;
    }

    string GetIngredientCodeFromName()
    {
        var objectName = gameObject.name;
        var separatorIndex = objectName.LastIndexOf('_');
        var suffix = separatorIndex >= 0 && separatorIndex < objectName.Length - 1
            ? objectName.Substring(separatorIndex + 1)
            : objectName;

        return KitchenIngredientLogic.TryGetIngredientCodeByResourceSuffix(suffix, out var code)
            ? code
            : KitchenIngredientLogic.ToCamelCase(suffix);
    }

}
