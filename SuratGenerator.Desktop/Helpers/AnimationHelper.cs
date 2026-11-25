using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SuratGenerator.Desktop.Helpers;

internal static class AnimationHelper
{
    private const uint SpiGetUiEffects = 0x103E;

    private static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation && GetUiEffectsEnabled();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

    private static bool GetUiEffectsEnabled()
    {
        var enabled = true;
        SystemParametersInfo(SpiGetUiEffects, 0, ref enabled, 0);
        return enabled;
    }

    public static void PlayEntrance(FrameworkElement element)
    {
        if (!AnimationsEnabled)
        {
            element.Opacity = 1;
            element.RenderTransform = null;
            return;
        }

        element.RenderTransform = new TranslateTransform();
        if (element.TryFindResource("AppSlideUp") is Storyboard storyboard)
        {
            storyboard.Begin(element, true);
        }
    }

    public static void PlayFade(FrameworkElement element)
    {
        if (!AnimationsEnabled)
        {
            element.Opacity = 1;
            return;
        }

        if (element.TryFindResource("AppFadeIn") is Storyboard storyboard)
        {
            storyboard.Begin(element, true);
        }
    }

    public static void PlaySelection(FrameworkElement element)
    {
        if (!AnimationsEnabled)
        {
            return;
        }

        element.RenderTransform = new ScaleTransform(1, 1);
        if (element.TryFindResource("AppSelectScale") is Storyboard storyboard)
        {
            storyboard.Begin(element, true);
        }
    }

    public static void PlayPulse(FrameworkElement element)
    {
        if (!AnimationsEnabled)
        {
            element.Opacity = 1;
            return;
        }

        if (element.TryFindResource("AppLoadingPulse") is Storyboard storyboard)
        {
            storyboard.Begin(element, true);
        }
    }
}
