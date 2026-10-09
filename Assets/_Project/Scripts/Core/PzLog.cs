using UnityEngine;

namespace ReAction.Core
{
    /// <summary>
    /// 게임 로그는 [PZ] 접두사를 붙여 콘솔에서 필터링할 수 있게 한다.
    /// </summary>
    public static class PzLog
    {
        private const string Prefix = "[PZ] ";

        public static void Info(string message) => Debug.Log(Prefix + message);
        public static void Warn(string message) => Debug.LogWarning(Prefix + message);
        public static void Error(string message) => Debug.LogError(Prefix + message);
    }
}
