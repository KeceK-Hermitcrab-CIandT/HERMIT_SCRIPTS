using UnityEditor;
using UnityEngine.Localization.Settings;
using UnityEngine;

/// <summary>
/// Utility to cycle through available localization languages in the Unity Editor. Helps the Step 2 and Dev Testing
/// </summary>
[InitializeOnLoad]
public static class LocalizationTester
{
    private const string _prefKey = "LocalizationTester.isCycling";
    private const string _menuPath = "Hermitcrab/Toggle Cycle Languages";
    private static bool _isCycling;
    private static double _nextSwitchTime;
    private static int _currentIndex;

    static LocalizationTester()
    {
        _isCycling = EditorPrefs.GetBool(_prefKey, false);
        Menu.SetChecked(_menuPath, _isCycling);
        EditorApplication.update += OnEditorUpdate;
    }
    
    [MenuItem(_menuPath)]
    private static void ToggleCycling()
    {
        var locales = LocalizationSettings.AvailableLocales?.Locales;
        if (locales == null || locales.Count == 0)
        {
            Debug.LogWarning("No locales available to cycle.");
            return;
        }

        _isCycling = !_isCycling;
        EditorPrefs.SetBool(_prefKey, _isCycling);
        Menu.SetChecked(_menuPath, _isCycling);

        if (_isCycling)
        {
            _currentIndex = locales.IndexOf(LocalizationSettings.SelectedLocale);
            _nextSwitchTime = EditorApplication.timeSinceStartup + 1.0;
            Debug.Log("Started cycling locales every second.");
        }
        else
        {
            Debug.Log("Stopped cycling locales.");
            // Set to English when stopped
            var englishLocale = locales.Find(l => l.Identifier.Code.StartsWith("en"));
            if (englishLocale != null)
                LocalizationSettings.SelectedLocale = englishLocale;
        }
    }

    [MenuItem(_menuPath, true)]
    private static bool ToggleCyclingValidate()
    {
        Menu.SetChecked(_menuPath, _isCycling);
        return LocalizationSettings.AvailableLocales != null &&
               LocalizationSettings.AvailableLocales.Locales.Count > 0;
    }

    private static void OnEditorUpdate()
    {
        if (!_isCycling) return;

        if (EditorApplication.timeSinceStartup < _nextSwitchTime) return;

        var locales = LocalizationSettings.AvailableLocales?.Locales;
        if (locales == null || locales.Count == 0)
        {
            _isCycling = false;
            EditorPrefs.SetBool(_prefKey, false);
            Menu.SetChecked(_menuPath, false);
            return;
        }

        _currentIndex = (_currentIndex + 1) % locales.Count;
        LocalizationSettings.SelectedLocale = locales[_currentIndex];
        // Debug.Log($"Switched to locale: {locales[currentIndex].Identifier.Code}");
        _nextSwitchTime = EditorApplication.timeSinceStartup + 1.0;
    }
}
