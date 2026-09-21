using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;

namespace Game.Infrastructure.Platform.Steam
{
    static class SteamAsyncUtility
    {
        public static async UniTask<PlatformOperationResult> AwaitOperationAsync(
            UniTask<PlatformOperationResult> task,
            CancellationToken cancellationToken)
        {
            try
            {
                return await task.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return PlatformOperationResult.Cancelled();
            }
        }
    }
}
