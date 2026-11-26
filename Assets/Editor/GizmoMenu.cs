using UnityEditor;
using UnityEngine;

/// <summary>Gizmo 显示范围切换（全部单位 / 仅选中单位）</summary>
public static class GizmoMenu
{
    private const string Key = "PixelWasteland.GizmoOnlySelected";
    private const string MenuPath = "Tools/PixelWasteland/8 - Gizmos 仅选中显示";

    [MenuItem(MenuPath, false, 8)]
    private static void Toggle()
    {
        bool v = EditorPrefs.GetBool(Key, false);
        EditorPrefs.SetBool(Key, !v);
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(Key, false));
        return true;
    }
}
