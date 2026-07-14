using UnityEngine;
using UnityEngine.UI;

namespace Hackathon.WebPort
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class WebPortThemedImage : MonoBehaviour
    {
        public enum StyleSlot
        {
            ScreenBackground,
            Panel,
            HudPanel,
            Input,
            ProgressBackground,
            ProgressFill,
            PrimaryButton,
            SecondaryButton,
            SuccessButton,
            Danger,
        }

        public StyleSlot slot;
        public bool preserveExistingSpriteWhenConfigSpriteIsEmpty = true;

        public void Apply(WebPortVisualConfig config)
        {
            if (config == null)
                return;

            Image image = GetComponent<Image>();
            WebPortVisualConfig.UiImageStyle style = GetStyle(config);
            Sprite fallbackSprite = preserveExistingSpriteWhenConfigSpriteIsEmpty ? image.sprite : null;
            Sprite resolvedSprite = style.sprite != null ? style.sprite : fallbackSprite;

            image.sprite = resolvedSprite;
            image.color = ResolveColor(style, resolvedSprite);
            image.type = style.ResolveImageType(fallbackSprite);
            image.preserveAspect = style.preserveAspect;
            image.raycastTarget = style.raycastTarget;
            image.pixelsPerUnitMultiplier = Mathf.Max(style.pixelsPerUnitMultiplier, 0.01f);
            image.material = style.material;
        }

        private WebPortVisualConfig.UiImageStyle GetStyle(WebPortVisualConfig config)
        {
            return slot switch
            {
                StyleSlot.ScreenBackground => config.GetScreenBackgroundStyle(),
                StyleSlot.Panel => config.GetPanelStyle(),
                StyleSlot.HudPanel => config.GetHudPanelStyle(),
                StyleSlot.Input => config.GetInputStyle(),
                StyleSlot.ProgressBackground => config.GetProgressBackgroundStyle(),
                StyleSlot.ProgressFill => config.GetProgressFillStyle(WebPortVisuals.GoalGreen),
                StyleSlot.PrimaryButton => config.GetButtonStyle(config.uiPrimaryButton),
                StyleSlot.SecondaryButton => config.GetButtonStyle(config.uiSecondaryButton),
                StyleSlot.SuccessButton => config.GetButtonStyle(config.uiSuccessButton),
                StyleSlot.Danger => config.GetButtonStyle(config.uiDanger),
                _ => config.GetPanelStyle(),
            };
        }

        private static Color ResolveColor(WebPortVisualConfig.UiImageStyle style, Sprite resolvedSprite)
        {
            if (resolvedSprite != null && style.colorMode == WebPortVisualConfig.UiColorMode.PreserveSprite)
                return Color.white;
            return style.color;
        }
    }
}
