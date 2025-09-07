using UnityEngine;

namespace Game.Input.UINavigation.HighlightSystem
{
    [CreateAssetMenu(fileName = "HighlightPreset", menuName = "UI/Highlight Preset")]
    public class HighlightPreset : ScriptableObject
    {
        [Header("General")]
        public HighlightType type = HighlightType.FillOverlay;
        public bool drawOnTop = true;
        public bool respectMask = true;
        public bool raycastTarget = false;

        [Header("Fill")]
        public Color fillColor = new Color(1f, 0.92f, 0.016f, 0.5f);
        public bool showFillAlongsideBorder = false;

        [Header("Border (Sprite)")]
        public Sprite borderSprite;         
        public Color borderColor = Color.white;
        public float pixelsPerUnitMultiplier = 1f; 
        [Tooltip("Optional")]
        public Material borderMaterialOverride;
        public bool preserveAspect = false;

        [Header("Outline Effect")]
        [Range(0f, 16f)] public float outlineThickness = 2f;
        public Color outlineColor = Color.white;
    }
}