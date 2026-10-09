using UnityEngine;

namespace ReAction.Player
{
    /// <summary>
    /// 외부 히어로 에셋의 애니메이션 이벤트를 받는 자리. 에셋 데모 스크립트를 쓰지 않으므로
    /// 받는 쪽이 없으면 "no receiver" 오류가 난다. 먼지 연출은 아직 없어서 비워 둔다.
    /// </summary>
    public sealed class HeroAnimationEvents : MonoBehaviour
    {
        // HeroKnight_WallSlide 클립이 호출한다.
        public void AE_SlideDust() { }
    }
}
