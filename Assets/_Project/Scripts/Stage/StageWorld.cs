namespace ReAction.Stage
{
    /// <summary>
    /// 행동이 실행 중에 참고하는 스테이지 정보: 구역 위치, 문·창문 상태.
    /// 행동은 이 인터페이스로만 월드 오브젝트를 조작한다.
    /// </summary>
    public interface IStageWorld
    {
        ZoneData GetZone(string zoneId);
        Door GetDoor(string zoneId);
        BreakableWindow GetWindow(string zoneId);

        /// <summary>문이 열렸거나 부서졌는지, 창문이 깨졌는지. 문·창문이 아닌 구역이면 true.</summary>
        bool IsEntryOpen(string zoneId);
    }
}
