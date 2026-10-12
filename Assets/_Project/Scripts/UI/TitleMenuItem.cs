using UnityEngine;
using UnityEngine.EventSystems;

namespace ReAction.UI
{
    /// <summary>메인 화면 메뉴 한 줄: 마우스를 올리면 선택, 클릭하면 결정.</summary>
    public sealed class TitleMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        private TitleScreen _screen;
        private int _index;

        public void Setup(TitleScreen screen, int index)
        {
            _screen = screen;
            _index = index;
        }

        public void OnPointerEnter(PointerEventData eventData) => _screen?.Hover(_index);

        public void OnPointerClick(PointerEventData eventData) => _screen?.Choose(_index);
    }
}
