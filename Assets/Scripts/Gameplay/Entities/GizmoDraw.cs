using UnityEngine;

namespace DoudizhuTower.Gameplay.Entities
{
    /// <summary>
    /// 统一的 Gizmo 绘制工具：
    /// 让编辑期看到的范围与运行期真实判定口径一致。
    /// - Circle：适用于 OverlapCircle 类判定（以自身中心为圆心的圆）
    /// - ExpandedBounds：适用于「边缘距离」类判定（碰撞体 + 半径的 Minkowski 外形）
    /// - Band：适用于内圈到外圈之间的环形区间（如飞扑）
    /// </summary>
    public static class GizmoDraw
    {
        /// <summary>是否只在选中时绘制（Tools/PixelWasteland 菜单可切换）</summary>
        public static bool OnlyWhenSelected
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool("PixelWasteland.GizmoOnlySelected", false);
#else
                return false;
#endif
            }
        }

        /// <summary>当前对象是否需要绘制 Gizmo</summary>
        public static bool ShouldDraw(GameObject go)
        {
            if (!OnlyWhenSelected) return true;
#if UNITY_EDITOR
            return go != null && UnityEditor.Selection.Contains(go);
#else
            return false;
#endif
        }

        /// <summary>圆形（OverlapCircle 口径）</summary>
        public static void Circle(Vector3 center, float radius, Color color)
        {
            if (radius <= 0f) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(center, radius);
        }

        /// <summary>
        /// 膨胀外形（边缘距离口径）：碰撞体外形向四周膨胀 radius。
        /// 与运行时 GetUnitEdgeDistance ≤ radius 的判定一致。
        /// </summary>
        public static void ExpandedBounds(Vector3 center, Vector2 size, float radius, Color color)
        {
            if (radius < 0f) return;
            Gizmos.color = color;

            float hx = Mathf.Max(size.x, 0.01f) * 0.5f;
            float hy = Mathf.Max(size.y, 0.01f) * 0.5f;
            float ex = hx + radius;
            float ey = hy + radius;

            Vector3 tl = center + new Vector3(-ex, ey);
            Vector3 tr = center + new Vector3(ex, ey);
            Vector3 bl = center + new Vector3(-ex, -ey);
            Vector3 br = center + new Vector3(ex, -ey);

            Gizmos.DrawLine(tl, tr);
            Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl);
            Gizmos.DrawLine(bl, tl);

            if (radius <= 0.001f) return;

            // 四角圆弧
            Vector3 cTL = center + new Vector3(-hx, hy);
            Vector3 cTR = center + new Vector3(hx, hy);
            Vector3 cBL = center + new Vector3(-hx, -hy);
            Vector3 cBR = center + new Vector3(hx, -hy);
            Arc(cTL, radius, 90f, 180f, 6);
            Arc(cTR, radius, 0f, 90f, 6);
            Arc(cBL, radius, 180f, 270f, 6);
            Arc(cBR, radius, 270f, 360f, 6);
        }

        /// <summary>
        /// 环形区间：内圈（不会被触发）到外圈（最大触发距离）。
        /// 用于「射程外 ~ 某距离内」的触发型技能（如飞扑）。
        /// </summary>
        public static void Band(Vector3 center, float innerRadius, float outerRadius, Color color)
        {
            if (outerRadius <= 0f) return;
            Gizmos.color = color;
            float inner = Mathf.Clamp(innerRadius, 0f, outerRadius);
            if (inner > 0.001f) Gizmos.DrawWireSphere(center, inner);
            Gizmos.DrawWireSphere(center, outerRadius);

            // 放射线，便于看出是"环"而不是实心圆
            if (inner > 0.001f)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 90f * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    Gizmos.DrawLine(center + dir * inner, center + dir * outerRadius);
                }
            }
        }

        /// <summary>扇形轮廓（角度以 +X 为 0，逆时针为正）</summary>
        public static void Fan(Vector3 center, Vector3 dir, float angle, float radius, Color color, int segments = 24)
        {
            if (radius <= 0f) return;
            Gizmos.color = color;
            float half = angle * 0.5f;
            int seg = Mathf.Clamp(segments, 4, 64);

            Vector3 prev = center + Rotate(dir, -half) * radius;
            for (int i = 1; i <= seg; i++)
            {
                float a = -half + angle * i / seg;
                Vector3 next = center + Rotate(dir, a) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
            Gizmos.DrawLine(center, center + Rotate(dir, -half) * radius);
            Gizmos.DrawLine(center, center + Rotate(dir, half) * radius);
        }

        /// <summary>绕 Z 轴旋转角度（度）</summary>
        public static Vector3 Rotate(Vector3 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector3(v.x * cos - v.y * sin, v.x * sin + v.y * cos, v.z);
        }

        private static void Arc(Vector3 center, float radius, float startDeg, float endDeg, int segments)
        {
            int seg = Mathf.Max(1, segments);
            float step = (endDeg - startDeg) / seg;
            Vector3 prev = center + new Vector3(
                Mathf.Cos(startDeg * Mathf.Deg2Rad), Mathf.Sin(startDeg * Mathf.Deg2Rad), 0f) * radius;
            for (int i = 1; i <= seg; i++)
            {
                float a = (startDeg + step * i) * Mathf.Deg2Rad;
                Vector3 next = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
