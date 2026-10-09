using System;
using System.IO;
using UnityEngine;

namespace ReAction.Interpretation.Llm
{
    /// <summary>
    /// API 키는 코드·저장소에 넣지 않는다. 다음 순서로 찾는다:
    /// 1) 환경 변수 ANTHROPIC_API_KEY
    /// 2) 프로젝트의 UserSettings/api_keys.json  ({"anthropic": "sk-ant-..."})  — UserSettings/는 .gitignore 대상
    /// </summary>
    public static class ApiKeyStore
    {
        public const string EnvVar = "ANTHROPIC_API_KEY";
        public const string LocalFile = "UserSettings/api_keys.json";

        public static string GetAnthropicKey()
        {
            string fromEnv = Environment.GetEnvironmentVariable(EnvVar);
            if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();

            try
            {
                // 에디터·빌드 모두 프로젝트(또는 실행 파일) 폴더 기준
                string path = Path.Combine(Directory.GetCurrentDirectory(), LocalFile);
                if (!File.Exists(path)) return null;
                var keys = JsonUtility.FromJson<KeyFile>(File.ReadAllText(path));
                return string.IsNullOrWhiteSpace(keys?.anthropic) ? null : keys.anthropic.Trim();
            }
            catch (Exception)
            {
                return null;
            }
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class KeyFile
        {
            public string anthropic;
        }
#pragma warning restore 0649
    }
}
