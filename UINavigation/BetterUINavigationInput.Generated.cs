using System.Collections.Generic;
using Game.Input.UINavigation.HighlightSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Input.UINavigation
{
    public partial class BetterUINavigationInput
    {
        [Header("Auto Selection")]
        [Tooltip("If current selection is invalid, automatically select the nearest accessible item. If off, deselect instead.")]
        [SerializeField] private bool _autoSelectionEnabled = true;
        [Header("Smooth Scroll on Edge (Horizontal)")]
        [SerializeField] private float _smoothScrollStep = 0.05f;
        [SerializeField] private float _smoothScrollDuration = 0.25f;
        
        private MoveDirection _lastMoveDir = MoveDirection.None;
        private int _consecutiveMoveCount = 0;
        private float _lastMoveTime = 0f;
        
        private readonly List<Selectable> _filtered = new();
        private static readonly List<RaycastResult> _raycastCache = new();
        
        private InputSystemUIInputModule _inputModule;
        private Vector2 _moveValue;
        private InputActionReference _move;
        
        private Scrollbar _cachedScrollbar;
        private GameObject _lastSelectedObject;
        
        private ScrollRect _scrollingRect;
        private float _scrollFrom;
        private float _scrollTo;
        private float _scrollElapsed;
        
        private static SelectableHighlighter _selectableHighlighter;

        private void Awake()
        {
            DontDestroyOnLoad(this);
            _selectableHighlighter = new SelectableHighlighter();
        }
        
        private void OnEnable()
        {
            _inputModule = GetComponent<InputSystemUIInputModule>();
            if (_inputModule != null)
            {
                _move = _inputModule.move;
                _inputModule.move = null;
            }
            
            if (_move?.action != null)
            {
                _move.action.Enable();
                _move.action.performed += OnMove;
                _move.action.canceled  += OnMove;
            }
        }

        private void OnDisable()
        {
            if (_move?.action != null)
            {
                _move.action.performed -= OnMove;
                _move.action.canceled  -= OnMove;
                _move.action.Disable();
            }
        }
        
        private void OnMove(InputAction.CallbackContext ctx)
        {
            _moveValue = ctx.ReadValue<Vector2>();
        }
        
        private void Update()
        {
            UpdateSmoothScroll();
            var es = EventSystem.current;
            if (es == null || !es.sendNavigationEvents)
                return;

            var listWasUpdated = false;
            AutoSelectionStep(es, ref listWasUpdated);
            NavigationStep(es, ref listWasUpdated);
        }
        
        private void AutoSelectionStep(EventSystem es, ref bool listWasUpdated)
        {
            if (!_autoSelectionEnabled) return;
            
            var current = es.currentSelectedGameObject ? es.currentSelectedGameObject.GetComponent<Selectable>() : null;
            
            if (current == null || !IsAccessible(current))
            {
                EnsureFilteredAccessibleList(_filtered, ref listWasUpdated);
                Selectable bestAutoMatch = FindBestForAutoselection(_filtered);
                es.SetSelectedGameObject(bestAutoMatch?.gameObject);
                _selectableHighlighter.SelectElement(bestAutoMatch);
            }
        }
        
        private void NavigationStep(EventSystem es, ref bool listWasUpdated)
        {
            var current = es.currentSelectedGameObject ? es.currentSelectedGameObject.GetComponent<Selectable>() : null;
            
            _selectableHighlighter.SelectElement(current);
            
            var mv = _moveValue;
            if (Mathf.Approximately(mv.x, 0f) && Mathf.Approximately(mv.y, 0f))
            {
                _consecutiveMoveCount = 0;
                return;
            }

            var moveDir = MoveDirection.None;
            if (mv.sqrMagnitude > 0f)
            {
                if (Mathf.Abs(mv.x) > Mathf.Abs(mv.y))
                    moveDir = mv.x > 0 ? MoveDirection.Right : MoveDirection.Left;
                else
                    moveDir = mv.y > 0 ? MoveDirection.Up : MoveDirection.Down;
            }

            if (moveDir != _lastMoveDir)
                _consecutiveMoveCount = 0;

            if (moveDir == MoveDirection.None)
            {
                _consecutiveMoveCount = 0;
                return;
            }

            var time = Time.unscaledTime;
            bool allow;
            if (_consecutiveMoveCount == 0) allow = true;
            else if (_consecutiveMoveCount > 1) allow = time > _lastMoveTime + _inputModule.moveRepeatRate;
            else allow = time > _lastMoveTime + _inputModule.moveRepeatDelay;

            if (!allow)
                return;

            if (current != null)
            {
                if (_lastSelectedObject != current.gameObject)
                {
                    _cachedScrollbar = current.GetComponent<Scrollbar>();
                    _lastSelectedObject = current.gameObject;
                }
                
                if (_cachedScrollbar != null)
                {
                    var eventData = new AxisEventData(EventSystem.current);
                    eventData.moveDir = moveDir;
                    _cachedScrollbar.OnMove(eventData);
                    AfterMoveTick(moveDir, time);
                    return;
                }
            }

            if (current != null && !IsAccessible(current))
            {
                if (_autoSelectionEnabled)
                {
                    EnsureFilteredAccessibleList(_filtered, ref listWasUpdated);
                    var nearest = FindNearestAccessible(current, _filtered);
                    es.SetSelectedGameObject(nearest ? nearest.gameObject : null);
                    _selectableHighlighter.SelectElement(nearest);
                }
                else
                {
                    es.SetSelectedGameObject(null);
                    _selectableHighlighter.SelectElement(null);
                }
                AfterMoveTick(moveDir, time);
                return;
            }

            bool moved = false;
            if (current != null)
            {
                if (current.navigation.mode == Navigation.Mode.Explicit)
                {
                    moved = NavigateExplicit(current, moveDir);
                    _selectableHighlighter.SelectElement(current);
                }
                else
                {
                    EnsureFilteredAccessibleList(_filtered, ref listWasUpdated);
                    moved = NavigateAutomatic(current, moveDir, _filtered);
                    _selectableHighlighter.SelectElement(current);
                }
            }
            
            if (!moved && current != null)
            {
                if (TryScrollParentScrollRect(current, moveDir))
                {
                    AfterMoveTick(moveDir, time);
                    return;
                }
            }
            
            AfterMoveTick(moveDir, time);
        }

        private void AfterMoveTick(MoveDirection dir, float time)
        {
            _consecutiveMoveCount++;
            _lastMoveTime = time;
            _lastMoveDir = dir;
        }

        private bool TryScrollParentScrollRect(Selectable current, MoveDirection dir)
        {
            if (dir != MoveDirection.Left && dir != MoveDirection.Right)
                return false;

            var sr = current.GetComponentInParent<ScrollRect>();
            if (sr == null || !sr.horizontal || sr.content == null)
                return false;

            var viewport = sr.viewport != null ? sr.viewport : sr.transform as RectTransform;
            if (viewport == null)
                return false;

            float contentWidth = sr.content.rect.width;
            float viewportWidth = viewport.rect.width;
            if (contentWidth <= viewportWidth)
                return false;

            float delta = dir == MoveDirection.Right ? _smoothScrollStep : -_smoothScrollStep;
            float target = Mathf.Clamp01(sr.horizontalNormalizedPosition + delta);

            if (Mathf.Approximately(target, sr.horizontalNormalizedPosition))
                return false;

            StartSmoothHorizontalScroll(sr, target);
            return true;
        }

        private void StartSmoothHorizontalScroll(ScrollRect sr, float target)
        {
            _scrollingRect = sr;
            _scrollFrom = sr.horizontalNormalizedPosition;
            _scrollTo = target;
            _scrollElapsed = 0f;
        }

        private void UpdateSmoothScroll()
        {
            if (_scrollingRect == null)
                return;

            _scrollElapsed += Time.unscaledDeltaTime;
            float duration = Mathf.Max(0.0001f, _smoothScrollDuration);
            float t = Mathf.Clamp01(_scrollElapsed / duration);
            float eased = 1f - (1f - t) * (1f - t);
            _scrollingRect.horizontalNormalizedPosition = Mathf.Lerp(_scrollFrom, _scrollTo, eased);
            if (t >= 1f)
            {
                _scrollingRect = null;
            }
        }
    }
}
