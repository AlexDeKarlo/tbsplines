using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using TBSplineS;

namespace TBSplineS.Editor
{
    [InitializeOnLoad]
    public static class TbsSelectionWatcher
    {
        const string WasActiveKey = "TBSplineS.ToolWasActive";
        static bool _suppressed;

        static TbsSelectionWatcher()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void SuppressUntilSelectionChange()
        {
            _suppressed = true;
        }

        static void OnSelectionChanged()
        {
            _suppressed = false;
            EditorApplication.delayCall += Apply;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SessionState.SetBool(WasActiveKey, ToolManager.activeToolType == typeof(TbsSplineComputerTool));
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(WasActiveKey, false))
            {
                _suppressed = false;
                EditorApplication.delayCall += Apply;
            }
        }

        static void Apply()
        {
            if (_suppressed) return;
            GameObject active = Selection.activeGameObject;
            var computer = active != null ? active.GetComponent<TbsSplineComputer>() : null;
            bool toolActive = ToolManager.activeToolType == typeof(TbsSplineComputerTool);
            if (computer != null && !toolActive) ToolManager.SetActiveTool<TbsSplineComputerTool>();
            else if (computer == null && toolActive) ToolManager.RestorePreviousPersistentTool();
        }
    }
}
