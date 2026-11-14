using UnityEditor;

namespace Blizz606.UnityHackatime
{
    [InitializeOnLoad]
    internal static class HackatimeOnboarding
    {
        static HackatimeOnboarding()
        {
            EditorApplication.delayCall += ShowSetupOnce;
        }

        private static void ShowSetupOnce()
        {
            if (HackatimeSettings.OnboardingSeen || !string.IsNullOrWhiteSpace(HackatimeSettings.ApiKey)) return;

            HackatimeSettings.OnboardingSeen = true;
            HackatimeSettingsWindow.Open();
        }
    }
}
