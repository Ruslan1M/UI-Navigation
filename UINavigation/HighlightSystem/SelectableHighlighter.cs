// SelectableHighlighter.cs

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Input.UINavigation.HighlightSystem
{
    public sealed class SelectableHighlighter : IDisposable
    {
        private GameObject _go;
        private RectTransform _rt;

        private Image _fillImage;
        private Outline _outline;

        private Image _borderImage;

        private Selectable _cachedSelectable;
        private readonly HighlightPreset _defaultPreset;

        public SelectableHighlighter(HighlightPreset defaultPreset = null)
        {
            _defaultPreset = defaultPreset; 
        }

        public void SelectElement(Selectable selectable, HighlightPreset overridePreset = null)
        {
            if (selectable == null)
            {
                Hide();
                _cachedSelectable = null;
                return;
            }

            if (_cachedSelectable == selectable && _go != null && _go.activeSelf)
                return;

            EnsureCreated();

            var selectedRect = selectable.GetComponent<RectTransform>();
            if (!selectedRect)
            {
                Hide();
                _cachedSelectable = null;
                return;
            }

            var fromProvider = selectable.GetComponent<HighlightPresetProvider>()?.preset;
            var preset = overridePreset ?? fromProvider ?? _defaultPreset ?? BuildFallbackPreset();

            if (_go.transform.parent != selectedRect)
                _go.transform.SetParent(selectedRect, false);

            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;

            _fillImage.maskable = preset.respectMask;
            _borderImage.maskable = preset.respectMask;

            if (preset.drawOnTop) _go.transform.SetAsLastSibling();
            else _go.transform.SetAsFirstSibling();

            ApplyPresetVisual(preset);

            _go.SetActive(true);
            _cachedSelectable = selectable;
        }

        private void Hide()
        {
            if (_go) _go.SetActive(false);
            _cachedSelectable = null;
        }

        public void Dispose()
        {
            if (_go) UnityEngine.Object.Destroy(_go);
            _go = null;
            _rt = null;
            _fillImage = null;
            _outline = null;
            _borderImage = null;
        }


        private void EnsureCreated()
        {
            if (_go) return;

            _go = new GameObject("SelectionHighlight", typeof(RectTransform));
            _rt = _go.GetComponent<RectTransform>();

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_go.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            _fillImage = fillGo.GetComponent<Image>();
            _fillImage.raycastTarget = false;

            _outline = fillGo.AddComponent<Outline>();
            _outline.enabled = false;
            _outline.useGraphicAlpha = false;

            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(_go.transform, false);
            var borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;

            _borderImage = borderGo.GetComponent<Image>();
            _borderImage.raycastTarget = false;
            _borderImage.enabled = false;

            _go.SetActive(false);
        }

        private void ApplyPresetVisual(HighlightPreset p)
        {
            _outline.enabled = false;
            _borderImage.enabled = false;

            _fillImage.raycastTarget = p.raycastTarget;
            _borderImage.raycastTarget = p.raycastTarget;

            switch (p.type)
            {
                case HighlightType.FillOverlay:
                    _fillImage.color = p.fillColor;
                    _fillImage.enabled = true;
                    break;

                case HighlightType.BorderSprite:
                    ActiveBorderPreset(p);
                    break;

                case HighlightType.OutlineEffect:
                    ActiveOutlinePreset(p);
                    break;
            }
        }

        private void ActiveOutlinePreset(HighlightPreset p)
        {
            _fillImage.enabled = true;
            _fillImage.color = p.showFillAlongsideBorder ? p.fillColor : new Color(0, 0, 0, 0);

            _outline.enabled = true;
            _outline.effectColor = p.outlineColor;

            float d = Mathf.Max(0f, p.outlineThickness);
            _outline.effectDistance = new Vector2(d, d);
        }

        private void ActiveBorderPreset(HighlightPreset p)
        {
            _borderImage.enabled = true;
            _borderImage.sprite = p.borderSprite;
            _borderImage.type = p.borderSprite ? Image.Type.Sliced : Image.Type.Simple;
            _borderImage.color = p.borderColor;
            _borderImage.material = p.borderMaterialOverride ? p.borderMaterialOverride : null;
            _borderImage.preserveAspect = p.preserveAspect;
            _borderImage.pixelsPerUnitMultiplier = p.pixelsPerUnitMultiplier;

            _fillImage.enabled = p.showFillAlongsideBorder;
            _fillImage.color = p.fillColor;
        }

        private static HighlightPreset BuildFallbackPreset()
        {
            var tmp = ScriptableObject.CreateInstance<HighlightPreset>();
            tmp.type = HighlightType.FillOverlay;
            tmp.fillColor = new Color(1f, 0.92f, 0.016f, 0.5f);
            tmp.drawOnTop = true;
            tmp.respectMask = true;
            return tmp;
        }
    }
}
