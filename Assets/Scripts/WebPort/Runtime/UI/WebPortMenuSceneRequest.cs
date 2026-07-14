using System.Collections;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hackathon.WebPort
{
    public static class WebPortMenuSceneRequest
    {
        private enum RequestKind
        {
            None,
            CreateRoom,
            JoinRoom,
        }

        private static RequestKind _pendingKind;
        private static string _pendingRoomCode;
        private static bool _pendingAutoStart;
        private static bool _sceneRequestInProgress;

        public static bool HasPendingSceneRequest => _pendingKind != RequestKind.None || _sceneRequestInProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public static void LoadAndCreateRoom(string sceneName, bool autoStart)
        {
            _pendingKind = RequestKind.CreateRoom;
            _pendingRoomCode = string.Empty;
            _pendingAutoStart = autoStart;
            SceneManager.LoadScene(sceneName);
        }

        public static void LoadAndJoinRoom(string sceneName, string roomCode)
        {
            _pendingKind = RequestKind.JoinRoom;
            _pendingRoomCode = roomCode;
            _pendingAutoStart = false;
            SceneManager.LoadScene(sceneName);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_pendingKind == RequestKind.None)
                return;

            GameObject runnerObject = new("WebPort Menu Scene Request Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            WebPortMenuSceneRequestRunner runner = runnerObject.AddComponent<WebPortMenuSceneRequestRunner>();
            _sceneRequestInProgress = true;
            runner.Run(_pendingKind == RequestKind.CreateRoom, _pendingRoomCode, _pendingAutoStart);

            _pendingKind = RequestKind.None;
            _pendingRoomCode = string.Empty;
            _pendingAutoStart = false;
        }

        private sealed class WebPortMenuSceneRequestRunner : MonoBehaviour
        {
            private bool _createRoom;
            private string _roomCode;
            private bool _autoStart;

            public void Run(bool createRoom, string roomCode, bool autoStart)
            {
                _createRoom = createRoom;
                _roomCode = roomCode;
                _autoStart = autoStart;
                StartCoroutine(InvokeExistingUiRequest());
            }

            private IEnumerator InvokeExistingUiRequest()
            {
                const float timeoutSeconds = 5f;
                float startedAt = Time.realtimeSinceStartup;

                while (Time.realtimeSinceStartup - startedAt < timeoutSeconds)
                {
                    yield return null;

                    WebPortUiController ui = FindAnyObjectByType<WebPortUiController>(FindObjectsInactive.Include);
                    if (ui == null)
                        continue;

                    ui.HidePanelsForSceneRequest();

                    if (_createRoom)
                    {
                        InvokeUiEvent(ui, "CreateRoomRequested");
                        if (_autoStart)
                        {
                            ui.HidePanelsForSceneRequest();
                            yield return StartCoroutine(StartHostGameWhenReady(ui));
                        }
                        else
                        {
                            _sceneRequestInProgress = false;
                        }

                        Destroy(gameObject);
                        yield break;
                    }

                    InvokeUiEvent(ui, "JoinRoomRequested", _roomCode);
                    _sceneRequestInProgress = false;
                    Destroy(gameObject);
                    yield break;
                }

                Debug.LogWarning("[WebPortMenuSceneRequest] Could not find the existing WebPort menu request controls after loading the gameplay scene.");
                _sceneRequestInProgress = false;
                Destroy(gameObject);
            }

            private static IEnumerator StartHostGameWhenReady(WebPortUiController ui)
            {
                const float timeoutSeconds = 10f;
                float startedAt = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - startedAt < timeoutSeconds)
                {
                    yield return null;

                    if (IsHostLobbyReady())
                    {
                        InvokeUiEvent(ui, "StartGameRequested");
                        _sceneRequestInProgress = false;
                        yield break;
                    }
                }

                _sceneRequestInProgress = false;
                Debug.LogWarning("[WebPortMenuSceneRequest] Created a room, but host lobby state was not ready before the auto-start timeout.");
            }

            private static bool IsHostLobbyReady()
            {
                WebPortGameManager manager = FindAnyObjectByType<WebPortGameManager>(FindObjectsInactive.Include);
                if (manager == null)
                    return false;

                Type type = typeof(WebPortGameManager);
                FieldInfo phaseField = type.GetField("_phase", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo selfIdField = type.GetField("_selfId", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo hostIdField = type.GetField("_hostId", BindingFlags.Instance | BindingFlags.NonPublic);
                if (phaseField == null || selfIdField == null || hostIdField == null)
                    return false;

                GamePhase phase = (GamePhase)phaseField.GetValue(manager);
                int selfId = (int)selfIdField.GetValue(manager);
                int hostId = (int)hostIdField.GetValue(manager);
                return phase == GamePhase.Lobby && selfId == hostId;
            }

            private static void InvokeUiEvent(WebPortUiController ui, string eventName, params object[] args)
            {
                FieldInfo eventField = typeof(WebPortUiController).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (eventField?.GetValue(ui) is not Delegate callback)
                    return;

                callback.DynamicInvoke(args);
            }
        }
    }
}
