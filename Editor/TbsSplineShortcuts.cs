using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace TBSplineS.Editor
{
    static class TbsSplineShortcuts
    {
        [Shortcut("TBSplineS/Toggle Spline Editor", typeof(SceneView), KeyCode.E, ShortcutModifiers.Alt)]
        static void ToggleEditor()
        {
            if (ToolManager.activeToolType == typeof(TbsSplineComputerTool))
            {
                TbsSplineEditorActions.ExitEditor();
            }
            else
            {
                TbsSplineEditorActions.ActivateEditTool();
            }
        }
    }
}
