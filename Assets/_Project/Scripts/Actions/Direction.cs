namespace ReAction.Actions
{
    /// <summary>
    /// Forward/Backward는 현재 바라보는 방향 기준, Left/Right는 화면 기준. Up/Down은 사다리를 오를지 내려갈지.
    /// 기록에 정수로 저장되므로 새 값은 항상 맨 뒤에 추가한다.
    /// </summary>
    public enum Direction
    {
        None,
        Forward,
        Backward,
        Left,
        Right,
        Up,
        Down,
    }

    public static class DirectionExtensions
    {
        /// <summary>바라보는 방향(facing: -1 또는 1)을 기준으로 실제 x 부호를 구한다. None이면 0.</summary>
        public static int ToSign(this Direction direction, int facing)
        {
            switch (direction)
            {
                case Direction.Forward: return facing;
                case Direction.Backward: return -facing;
                case Direction.Left: return -1;
                case Direction.Right: return 1;
                default: return 0;
            }
        }
    }
}
