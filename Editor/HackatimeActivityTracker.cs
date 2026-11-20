using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blizz606.UnityHackatime
{
    internal static class HackatimeActivityTracker
    {
        // Hackatime's documented heartbeat rate limit. Never send more often than this.
        private const double IntervalSeconds = 30.0;
        private static bool initialized, queued, queuedWrite, sending;
        private static double nextAllowed;
        private static string lastEntity;
        private static double lastSuccess;
        private static string lastAction = "Waiting for activity";
        private static double lastActionAt;
        private static double lastDirectActionAt;
        private static int observedActionCount;
        private static string lastConnectionMessage = "API key is missing.";
        private static string lastErrorMessage;
        private static string lastConsoleMessage;
        private static double lastConsoleMessageAt;

        internal static string LastHeartbeatText
        {
            get { return lastSuccess <= 0 ? "Never" : FormatAge(EditorTime.Now - lastSuccess); }
        }

        internal static string LastActionText { get { return lastActionAt <= 0 ? lastAction : lastAction + " (" + FormatAge(EditorTime.Now - lastActionAt) + ")"; } }
        internal static string ConnectionText { get { return lastConnectionMessage; } }
        internal static string LastErrorText { get { return string.IsNullOrEmpty(lastErrorMessage) ? "None" : lastErrorMessage; } }
        internal static int ObservedActionCount { get { return observedActionCount; } }
        internal static bool HasQueuedHeartbeat { get { return queued || sending; } }
        internal static string NextHeartbeatText { get { return nextAllowed <= EditorTime.Now ? "Ready" : "in " + FormatAge(nextAllowed - EditorTime.Now); } }

        internal static void Initialize()
        {
            Unregister();
            initialized = true;
            if (!HackatimeSettings.TrackingEnabled) return;
            Register();
        }

        internal static void Shutdown() { Unregister(); initialized = false; }

        internal static void RecordActivity(string action, bool write = false, bool isDirectUserAction = true)
        {
            if (!initialized || !HackatimeSettings.TrackingEnabled || string.IsNullOrWhiteSpace(HackatimeSettings.ApiKey)) return;
            double now = EditorTime.Now;
            // Hierarchy and Project GUI callbacks run once per visible item.
            // Treat their repeated calls for the same input as one action.
            if (action == lastAction && now - lastActionAt < 0.15) return;
            lastAction = action;
            lastActionAt = now;
            if (isDirectUserAction) lastDirectActionAt = now;
            observedActionCount++;
            Request(write);
        }

        internal static void RecordConnectionStatus(HackatimeConnectionStatus status, string message)
        {
            lastConnectionMessage = message;
            if (status == HackatimeConnectionStatus.Connected)
            {
                lastErrorMessage = null;
                return;
            }

            if (status == HackatimeConnectionStatus.NotConfigured) return;
            lastErrorMessage = message;
            bool isRateLimit = status == HackatimeConnectionStatus.RateLimited;
            if (message == lastConsoleMessage && EditorTime.Now - lastConsoleMessageAt < 300) return;
            lastConsoleMessage = message;
            lastConsoleMessageAt = EditorTime.Now;
            if (isRateLimit) Debug.LogWarning("[Hackatime] " + message);
            else Debug.LogError("[Hackatime] " + message);
        }

        private static void Request(bool write)
        {
            if (!initialized || !HackatimeSettings.TrackingEnabled || string.IsNullOrWhiteSpace(HackatimeSettings.ApiKey)) return;
            if (sending || EditorTime.Now < nextAllowed) { queued = true; queuedWrite |= write; return; }
            HackatimeHeartbeat heartbeat = new HackatimeHeartbeat(HackatimeProject.ActiveEntity(), write);
            // Do not discard activity inside the rate-limit window. Queue one
            // heartbeat for the next allowed moment instead, then stop until
            // Unity reports another user action.
            if (!write && heartbeat.entity == lastEntity && EditorTime.Now - lastSuccess < IntervalSeconds)
            {
                queued = true;
                return;
            }
            sending = true;
            HackatimeClient.Instance.Send(heartbeat, (status, message) =>
            {
                sending = false;
                nextAllowed = status == HackatimeConnectionStatus.RateLimited
                    ? Math.Max(EditorTime.Now + IntervalSeconds, HackatimeClient.Instance.RetryAt)
                    : EditorTime.Now + IntervalSeconds;
                if (status == HackatimeConnectionStatus.Connected)
                {
                    lastEntity = heartbeat.entity;
                    lastSuccess = EditorTime.Now;
                }
                RecordConnectionStatus(status, message);
                if (HackatimeSettings.DebugLogging) Debug.Log("[Hackatime] " + message);
                if (queued && EditorTime.Now >= nextAllowed) { bool save = queuedWrite; queued = queuedWrite = false; Request(save); }
            });
        }

        private static void Register()
        {
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.projectChanged += OnProjectChanged;
#if UNITY_6000_0_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyItemGui;
#else
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGui;
#endif
            EditorApplication.projectWindowItemOnGUI += OnProjectItemGui;
            Selection.selectionChanged += OnSelectionChanged;
            Undo.postprocessModifications += OnPostprocessModifications;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            SceneView.duringSceneGui += OnSceneGui;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorSceneManager.sceneDirtied += OnSceneDirtied;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.newSceneCreated += OnSceneCreated;
            EditorApplication.quitting += Shutdown;
        }

        private static void Unregister()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
#if UNITY_6000_0_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnHierarchyItemGui;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyItemGui;
#endif
            EditorApplication.projectWindowItemOnGUI -= OnProjectItemGui;
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.postprocessModifications -= OnPostprocessModifications;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            SceneView.duringSceneGui -= OnSceneGui;
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneDirtied -= OnSceneDirtied;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosing -= OnSceneClosing;
            EditorSceneManager.newSceneCreated -= OnSceneCreated;
            EditorApplication.quitting -= Shutdown;
        }

        private static void OnUpdate() { if (queued && !sending && EditorTime.Now >= nextAllowed) { bool save = queuedWrite; queued = queuedWrite = false; Request(save); } }
        private static void OnPlayModeChanged(PlayModeStateChange _) { RecordActivity("Play mode changed"); }
        private static void OnHierarchyChanged() { RecordActivity("Hierarchy changed"); }
        private static void OnProjectChanged()
        {
            // Project imports can happen in the background. Only count one when
            // the user has interacted with Unity recently.
            if (lastDirectActionAt > 0 && EditorTime.Now - lastDirectActionAt <= 120)
                RecordActivity("Project assets changed", false, false);
        }
        private static void OnSelectionChanged() { RecordActivity("Selection changed"); }
        private static UndoPropertyModification[] OnPostprocessModifications(UndoPropertyModification[] modifications) { RecordActivity("Inspector or scene changed"); return modifications; }
        private static void OnUndoRedoPerformed() { RecordActivity("Undo or redo"); }
        private static void OnSceneDirtied(Scene _) { RecordActivity("Scene modified"); }
        private static void OnSceneSaved(Scene _) { RecordActivity("Scene saved", true); }
        private static void OnSceneOpened(Scene _, OpenSceneMode __) { RecordActivity("Scene opened"); }
        private static void OnSceneClosing(Scene _, bool __) { RecordActivity("Scene closing"); }
        private static void OnSceneCreated(Scene _, NewSceneSetup __, NewSceneMode ___) { RecordActivity("Scene created"); }
        private static void OnActiveSceneChanged(Scene _, Scene __) { RecordActivity("Active scene changed"); }

#if UNITY_6000_0_OR_NEWER
        private static void OnHierarchyItemGui(EntityId _, Rect __) { if (IsUserInput(Event.current)) RecordActivity("Hierarchy interaction"); }
#else
        private static void OnHierarchyItemGui(int _, Rect __) { if (IsUserInput(Event.current)) RecordActivity("Hierarchy interaction"); }
#endif
        private static void OnProjectItemGui(string _, Rect __) { if (IsUserInput(Event.current)) RecordActivity("Project window interaction"); }
        private static void OnSceneGui(SceneView _) { if (IsUserInput(Event.current)) RecordActivity("Scene view interaction"); }

        private static bool IsUserInput(Event current)
        {
            if (current == null) return false;
            switch (current.type)
            {
                case EventType.KeyDown:
                case EventType.KeyUp:
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseDrag:
                case EventType.ScrollWheel:
                    return true;
                default:
                    return false;
            }
        }

        private static string FormatAge(double seconds)
        {
            if (seconds < 2) return "just now";
            if (seconds < 60) return Math.Floor(seconds) + " seconds ago";
            if (seconds < 3600) return Math.Floor(seconds / 60) + " minutes ago";
            return Math.Floor(seconds / 3600) + " hours ago";
        }
    }
}
