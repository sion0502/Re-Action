using UnityEngine;

namespace ReAction.UI
{
    /// <summary>
    /// 사각형 외곽선. 픽셀 아트에 맞게 얇은 사각형 4개로 그린다.
    /// </summary>
    public sealed class ZoneOutline
    {
        private readonly Transform[] _edges = new Transform[4];
        private readonly SpriteRenderer[] _renderers = new SpriteRenderer[4];
        private readonly float _thickness;

        public ZoneOutline(Transform parent, string name, Sprite sprite, Color color, float thickness, int sortingOrder)
        {
            _thickness = thickness;
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"Edge{i}");
                go.transform.SetParent(root, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = color;
                renderer.sortingOrder = sortingOrder;
                _edges[i] = go.transform;
                _renderers[i] = renderer;
            }
            Hide();
        }

        public void Show(Rect rect)
        {
            float t = _thickness;
            Place(0, new Vector2(rect.center.x, rect.yMax - t / 2f), new Vector2(rect.width, t));
            Place(1, new Vector2(rect.center.x, rect.yMin + t / 2f), new Vector2(rect.width, t));
            Place(2, new Vector2(rect.xMin + t / 2f, rect.center.y), new Vector2(t, rect.height));
            Place(3, new Vector2(rect.xMax - t / 2f, rect.center.y), new Vector2(t, rect.height));
            foreach (var r in _renderers) r.enabled = true;
        }

        public void Hide()
        {
            foreach (var r in _renderers) r.enabled = false;
        }

        private void Place(int index, Vector2 center, Vector2 size)
        {
            _edges[index].position = center;
            _edges[index].localScale = new Vector3(size.x, size.y, 1f);
        }
    }
}
