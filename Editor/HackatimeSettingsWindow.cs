using UnityEditor;
using UnityEngine;

namespace Blizz606.UnityHackatime
{
    internal sealed class HackatimeSettingsWindow : EditorWindow
    {
        private string status = "Not configured";
        private string apiKey, apiUrl, projectOverride;
        private bool enabled, debug;
        private bool showSettings = true;

        [MenuItem("Window/Hackatime")]
        internal static void Open() { GetWindow<HackatimeSettingsWindow>("Hackatime"); }

        private void OnEnable() { Load(); }
        private void OnInspectorUpdate() { Repaint(); }
        private void Load()
        {
            apiKey = HackatimeSettings.ApiKey; apiUrl = HackatimeSettings.ApiUrl; projectOverride = HackatimeSettings.ProjectOverride;
            enabled = HackatimeSettings.TrackingEnabled; debug = HackatimeSettings.DebugLogging;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Hackatime for Unity", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                EditorGUILayout.HelpBox("One quick step: paste your Hackatime API key below, then click Test Connection. Tracking starts automatically.", MessageType.Info);
                if (GUILayout.Button("Open Hackatime to get an API key")) Application.OpenURL("https://hackatime.hackclub.com");
                EditorGUILayout.Space(6);
            }
            DrawOverview();
            EditorGUILayout.Space(8);
            showSettings = EditorGUILayout.Foldout(showSettings, "Settings", true);
            if (showSettings) DrawSettings();
        }

        private void DrawOverview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Tracking overview", EditorStyles.boldLabel);
            DrawRow("Connection", HackatimeActivityTracker.ConnectionText);
            DrawRow("Project", HackatimeProject.ProjectName);
            DrawRow("Branch", string.IsNullOrEmpty(HackatimeProject.Branch) ? "Not detected" : HackatimeProject.Branch);
            DrawRow("Active entity", HackatimeProject.ActiveEntity());
            DrawRow("Last action", HackatimeActivityTracker.LastActionText);
            DrawRow("Last heartbeat", HackatimeActivityTracker.LastHeartbeatText);
            DrawRow("Heartbeat", HackatimeActivityTracker.HasQueuedHeartbeat ? "Queued or sending" : HackatimeActivityTracker.NextHeartbeatText);
            DrawRow("Observed actions", HackatimeActivityTracker.ObservedActionCount.ToString());
            DrawRow("Last error", HackatimeActivityTracker.LastErrorText);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Connection")) TestConnection();
            if (GUILayout.Button("Open Dashboard")) Application.OpenURL("https://hackatime.hackclub.com");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("Status", status);
            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            apiKey = EditorGUILayout.PasswordField("API Key", apiKey);
            projectOverride = EditorGUILayout.TextField("Project Name", projectOverride);
            enabled = EditorGUILayout.Toggle("Enable Tracking", enabled);
            debug = EditorGUILayout.Toggle("Debug Logging", debug);
            apiUrl = EditorGUILayout.TextField("API URL", apiUrl);
            if (EditorGUI.EndChangeCheck()) Save(true);
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox("Only project-relative activity, project, branch, time, language, editor and normalized OS are sent. The API key is stored in Unity EditorPrefs and is never written to the project.", MessageType.Info);
        }

        private static void DrawRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(112));
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.label, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        private void Save(bool reinitialize)
        {
            HackatimeSettings.ApiKey = apiKey == null ? string.Empty : apiKey.Trim();
            HackatimeSettings.ProjectOverride = projectOverride;
            HackatimeSettings.ApiUrl = apiUrl;
            HackatimeSettings.TrackingEnabled = enabled;
            HackatimeSettings.DebugLogging = debug;
            if (!string.IsNullOrWhiteSpace(HackatimeSettings.ApiKey)) HackatimeSettings.OnboardingSeen = true;
            if (reinitialize) HackatimePlugin.Reinitialize();
            status = enabled && !string.IsNullOrWhiteSpace(apiKey) ? "Configured" : "Not configured";
            Repaint();
        }

        private void TestConnection()
        {
            Save(false);
            if (string.IsNullOrWhiteSpace(HackatimeSettings.ApiKey)) { status = "Not configured"; Repaint(); return; }
            status = "Testing…"; Repaint();
            HackatimeClient.Instance.Send(new HackatimeHeartbeat(HackatimeProject.ActiveEntity(), false), (result, message) => { HackatimeActivityTracker.RecordConnectionStatus(result, message); status = message; Repaint(); });
        }
    }
}
