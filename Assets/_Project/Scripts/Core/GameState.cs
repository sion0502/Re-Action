namespace ReAction.Core
{
    public enum GameState
    {
        Boot,
        Planning,
        Interpreting,
        Executing,
        Dead,
        Rewind,
        Reset,
        Cleared,
        FinalRun,
        Result,
        /// <summary>메인 화면. 배경으로 스테이지가 정상 속도로 돌고 고블린은 순찰만 한다.</summary>
        Title,
    }
}
