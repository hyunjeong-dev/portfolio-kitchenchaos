using UnityEngine;

namespace Game.Infrastructure.Platform.Steam
{
    static class SteamLog
    {
        public static void Info(string message)
        {
            Debug.Log($"[Steam] {message}");
        }

        public static void Info(string category, string message)
        {
            Debug.Log($"[Steam][{category}] {message}");
        }

        public static void Warning(string message)
        {
            Debug.LogWarning($"[Steam] {message}");
        }

        public static void Warning(string category, string message)
        {
            Debug.LogWarning($"[Steam][{category}] {message}");
        }

        public static void Error(string message)
        {
            Debug.LogError($"[Steam][Error] {message}");
        }

        public static void Exception(string operation, System.Exception exception)
        {
            Debug.LogError($"[Steam][Error] {operation} failed.\n{exception}");
        }

        public static void Exception(string category, string operation, System.Exception exception)
        {
            Debug.LogError($"[Steam][{category}][Error] {operation} failed.\n{exception}");
        }
    }
}