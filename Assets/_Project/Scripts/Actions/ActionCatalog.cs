using System.Collections.Generic;
using UnityEngine;

namespace ReAction.Actions
{
    /// <summary>
    /// 현재 사용 가능한 행동 정의 목록. 여기에 없는 ActionType은 실행할 수 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "ReAction/Action Catalog", fileName = "ActionCatalog")]
    public sealed class ActionCatalog : ScriptableObject
    {
        [SerializeField] private List<ActionDefinition> _definitions = new List<ActionDefinition>();

        public IReadOnlyList<ActionDefinition> Definitions => _definitions;

        public bool TryGet(ActionType type, out ActionDefinition definition)
        {
            foreach (var d in _definitions)
            {
                if (d != null && d.Type == type)
                {
                    definition = d;
                    return true;
                }
            }
            definition = null;
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetDefinitions(List<ActionDefinition> definitions) => _definitions = definitions;
#endif
    }
}
