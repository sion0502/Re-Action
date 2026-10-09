namespace ReAction.Enemies
{
    /// <summary>플레이어 공격에 맞을 수 있는 대상. 1히트에 죽는다.</summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        void Kill();
    }
}
