using UnityEngine;
using UnityEngine.SceneManagement;

namespace ReAction.Core
{
    /// <summary>메인 화면에서 고른 해석 방식.</summary>
    public enum InterpreterChoice
    {
        /// <summary>메인 화면을 거치지 않았다 (에디터에서 바로 실행 등): LlmSettings를 따른다.</summary>
        FromSettings,
        /// <summary>이 PC의 Ollama 로컬 모델 (안 되면 키워드 해석기로 대체).</summary>
        LocalLlm,
        /// <summary>키워드 해석기만. Ollama에 전혀 연결하지 않는다.</summary>
        Keyword,
    }

    /// <summary>
    /// 메인 화면 → 게임으로 넘기는 선택. 게임을 시작하거나 메인 화면으로 돌아갈 때는 씬을 새로 불러온다
    /// (시도 횟수·안개·월드가 모두 처음부터). 에셋(LlmSettings)은 바꾸지 않는다.
    /// </summary>
    public static class GameLaunchOptions
    {
        /// <summary>true면 메인 화면 없이 바로 게임(Planning)으로 시작한다.</summary>
        public static bool SkipTitle { get; private set; }

        public static InterpreterChoice Interpreter { get; private set; } = InterpreterChoice.FromSettings;

        // 도메인 리로드를 끈 에디터 설정에서도 플레이할 때마다 처음 값으로 시작한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SkipTitle = false;
            Interpreter = InterpreterChoice.FromSettings;
        }

        public static void StartGame(InterpreterChoice interpreter)
        {
            Interpreter = interpreter;
            SkipTitle = true;
            PzLog.Info($"Start game with interpreter {interpreter}");
            ReloadScene();
        }

        public static void ReturnToTitle()
        {
            SkipTitle = false;
            PzLog.Info("Return to title");
            ReloadScene();
        }

        private static void ReloadScene() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
