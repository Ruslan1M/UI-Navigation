using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Input.UINavigation
{
    [RequireComponent(typeof(InputSystemUIInputModule))]
    [DisallowMultipleComponent]
    public partial class BetterUINavigationInput : MonoBehaviour
    {
        public static class ProfilerMarkers
        {
            public static readonly ProfilerMarker IsAccessibleMarker = new("IsAccessible");
            public static readonly ProfilerMarker RaycastAll = new("RaycastAll");
            public static readonly ProfilerMarker BuildFilteredAccessibleListMarker = new("BuildFilteredAccessibleList");
        }

        private static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir)
        {
            if (rect == null)
                return Vector3.zero;
            if (dir != Vector2.zero)
                dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            dir = rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
            return dir;
        }

        public static Selectable FindSelectable(Selectable from, Vector3 dir, List<Selectable> selectables)
        {
            dir = dir.normalized;
            var localDir = Quaternion.Inverse(from.transform.rotation) * dir;
            var pos = from.transform.TransformPoint(GetPointOnRectEdge(from.transform as RectTransform, localDir));
            var maxScore = Mathf.NegativeInfinity;
            var maxFurthestScore = Mathf.NegativeInfinity;
            float score = 0;

            var wantsWrapAround = from.navigation.wrapAround && (from.navigation.mode == Navigation.Mode.Vertical ||
                                                                 from.navigation.mode == Navigation.Mode.Horizontal);

            Selectable bestPick = null;
            Selectable bestFurthestPick = null;

            for (var i = 0; i < selectables.Count; ++i)
            {
                Selectable sel = selectables[i];

                if (sel == from)
                    continue;

                if (!sel.IsInteractable() || sel.navigation.mode == Navigation.Mode.None)
                    continue;

#if UNITY_EDITOR
                if (Camera.current != null &&
                    !UnityEditor.SceneManagement.StageUtility.IsGameObjectRenderedByCamera(sel.gameObject,
                        Camera.current))
                    continue;
#endif

                var selRect = sel.transform as RectTransform;
                var selCenter = selRect != null ? (Vector3)selRect.rect.center : Vector3.zero;
                var myVector = sel.transform.TransformPoint(selCenter) - pos;

                var dot = Vector3.Dot(dir, myVector);

                if (wantsWrapAround && dot < 0)
                {
                    score = -dot * myVector.sqrMagnitude;

                    if (score > maxFurthestScore)
                    {
                        maxFurthestScore = score;
                        bestFurthestPick = sel;
                    }

                    continue;
                }
                
                if (dot <= 0)
                    continue;
                
                score = dot / myVector.sqrMagnitude;

                if (score > maxScore)
                {
                    maxScore = score;
                    bestPick = sel;
                }
            }

            if (wantsWrapAround && null == bestPick) return bestFurthestPick;

            return bestPick;
        }

        private static bool IsAccessibleImplementation(Selectable selectable)
        {
            if (selectable == null || !selectable.IsActive() || !selectable.interactable) return false;

            var rectTransform = selectable.transform as RectTransform;

            var center = GetScreenCenter(rectTransform);
            if (center.x < 0 || center.y < 0 || center.x > Screen.width || center.y > Screen.height)
                return false;


            ProfilerMarkers.RaycastAll.Begin();
            var ped = new PointerEventData(EventSystem.current) { position = center };
            _raycastCache.Clear();
            EventSystem.current.RaycastAll(ped, _raycastCache);
            ProfilerMarkers.RaycastAll.End();

            if (_raycastCache.Count == 0) return false;

            var top = _raycastCache[0].gameObject?.transform;
            return top != null && (top == selectable.transform || top.IsChildOf(selectable.transform));
        }

        public static bool IsAccessible(Selectable selectable)
        {
            ProfilerMarkers.IsAccessibleMarker.Begin();
            var result = IsAccessibleImplementation(selectable);
            ProfilerMarkers.IsAccessibleMarker.End();
            return result;
        }

        private static Vector2 GetScreenCenter(RectTransform rt)
        {
            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);
            var centerWorld = 0.5f * (c[0] + c[2]);

            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            return RectTransformUtility.WorldToScreenPoint(cam, centerWorld);
        }

        private static Vector3 MoveToVector(MoveDirection d) =>
            d switch
            {
                MoveDirection.Left => Vector3.left,
                MoveDirection.Right => Vector3.right,
                MoveDirection.Up => Vector3.up,
                MoveDirection.Down => Vector3.down,
                _ => Vector3.zero
            };

        private static Selectable FindNearestAccessible(Selectable from, List<Selectable> pool)
        {
            var fromRt = from.transform as RectTransform;
            if (!fromRt) return null;

            var fromC = GetScreenCenter(fromRt);
            float best = float.PositiveInfinity;
            Selectable bestSel = null;

            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[i];
                if (!s || s == from) continue;

                var rt = s.transform as RectTransform;
                if (!rt) continue;

                var c = GetScreenCenter(rt);
                float d = (c - fromC).sqrMagnitude;
                if (d < best) { best = d; bestSel = s; }
            }
            return bestSel;
        }

        private static void EnsureFilteredAccessibleList(List<Selectable> selectables, ref bool listWasUpdated)
        {
            if (listWasUpdated) return;

            BuildFilteredAccessibleList(selectables);
            listWasUpdated = true;
        }

        private static void BuildFilteredAccessibleList(List<Selectable> pool)
        {
            ProfilerMarkers.BuildFilteredAccessibleListMarker.Begin();
            pool.Clear();
            for (var i = 0; i < Selectable.allSelectableCount; i++)
            {
                var selectable = SelectableAccessor.allSelectablesArrayNonAlloc[i];
                if (IsAccessible(selectable)) pool.Add(selectable);
            }
            ProfilerMarkers.BuildFilteredAccessibleListMarker.End();
        }

        private static bool NavigateExplicit(Selectable current, MoveDirection dir)
        {
            var next = GetExplicitNeighbor(current, dir);
            if (next && IsAccessible(next))
            {
                EventSystem.current.SetSelectedGameObject(next.gameObject);
                _selectableHighlighter.SelectElement(next);
                return true;
            }
            return false;
        }

        private static bool NavigateAutomatic(Selectable current, MoveDirection dir, List<Selectable> pool)
        {
            var next = FindSelectable(current, MoveToVector(dir), pool);
            if (next != null)
            {
                EventSystem.current.SetSelectedGameObject(next.gameObject);
                _selectableHighlighter.SelectElement(next);
                return true;
            }
            return false;
        }

        private static Selectable GetExplicitNeighbor(Selectable selectable, MoveDirection dir)
        {
            var n = selectable.navigation;
            if (n.mode != Navigation.Mode.Explicit) return null;
            return dir switch
            {
                MoveDirection.Left => n.selectOnLeft,
                MoveDirection.Right => n.selectOnRight,
                MoveDirection.Up => n.selectOnUp,
                MoveDirection.Down => n.selectOnDown,
                _ => null
            };
        }

        private static Selectable FindBestForAutoselection(List<Selectable> pool)
        {
            if (pool == null || pool.Count == 0) return null;

            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            var bestPriority = int.MinValue;
            var bestDist = float.PositiveInfinity;
            Selectable best = null;

            foreach (var selectable in pool)
            {
                if (!selectable) continue;
                if (!IsAccessible(selectable)) continue;

                var meta = selectable.GetComponent<BetterUINavigationElement>();
                if (meta != null && meta.IgnoreFromAutoSelection) continue;

                var pri = meta != null ? meta.AutoSelectPriority : 0;

                var rt = selectable.transform as RectTransform;
                if (!rt) continue;

                var center = GetScreenCenter(rt);
                var d = (center - screenCenter).sqrMagnitude;

                if (pri > bestPriority || (pri == bestPriority && d < bestDist))
                {
                    bestPriority = pri;
                    bestDist = d;
                    best = selectable;
                }
            }

            return best;
        }
    }
}