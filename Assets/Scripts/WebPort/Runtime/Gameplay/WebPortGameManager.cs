using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hackathon.WebPort
{
    public sealed class WebPortGameManager : MonoBehaviour
    {
        [SerializeField] private bool _useWebSocketTransport = true;
        [SerializeField] private string _serverUrl = "ws://192.168.1.148:8081";
        [SerializeField] private WebPortSceneLayout _layout;

        private readonly Dictionary<int, PlayerState> _players = new();
        private readonly Dictionary<int, PackageState> _packages = new();
        private readonly List<PackageSlot> _slots = new();
        private readonly List<EffectEvent> _effects = new();
        private readonly List<int> _memberIds = new();

        private IGameTransport _transport;
        private LocalGameTransport _localTransport;
        private WebPortRenderSystem _renderSystem;
        private WebPortUiController _ui;
        private WebPortCameraRig _cameraRig;
        private UnityEngine.Camera _camera;
        private Vector3 _mouseWorld;
        private Vector3 _start = WebPortConstants.Start;
        private Vector3 _goal = WebPortConstants.GoalPositions[0];
        private GamePhase _phase = GamePhase.Menu;
        private string _roomCode = "LOCAL";
        private int _selfId;
        private int _hostId;
        private int _goalIndex;
        private int _nextPackageId;
        private int _deliveredSinceTruck;
        private float _goalTimer;
        private float _sessionEndTime;
        private float _sessionRemainMs;
        private float _sendTimer;
        private float _truckBannerUntil;
        private bool _cameraTargetBound;

        private PlayerState Self => _players.TryGetValue(_selfId, out PlayerState player) ? player : null;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (!SetupSceneObjects())
                return;

            IGameTransport transport = _useWebSocketTransport ? (IGameTransport)new WebSocketGameTransport(_serverUrl) : new LocalGameTransport();
            SetupTransport(transport);
            _transport.Connect();
            _ui.ShowMenu(null);
        }

        private void OnDestroy()
        {
            if (_transport != null)
            {
                _transport.Connected -= OnConnected;
                _transport.RoomStateChanged -= OnRoomStateChanged;
                _transport.GameStarted -= OnGameStarted;
                _transport.GameEnded -= OnGameEnded;
                _transport.GameMessageReceived -= OnGameMessage;
                _transport.Dispose();
            }
        }

        private void Update()
        {
            _transport?.Pump();

            if (_phase != GamePhase.Playing || Self == null)
                return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float now = Time.time;

            UpdateMouseWorld();
            HandleInput(now);
            TickGame(dt, now);
            Render(now);
            UpdateHud();
        }

        private bool SetupSceneObjects()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white * 0.75f;

            if (_layout == null)
                _layout = GetComponentInChildren<WebPortSceneLayout>(true);
            if (_layout == null)
                _layout = FindAnyObjectByType<WebPortSceneLayout>(FindObjectsInactive.Include);

            if (_layout == null)
            {
                Debug.LogError("WebPortGameManager requires a WebPortSceneLayout. Select the manager and click Generate Edit Mode Layout in the Inspector.");
                enabled = false;
                return false;
            }

            if (!_layout.HasRequiredReferences(out string layoutError))
            {
                Debug.LogError(layoutError);
                enabled = false;
                return false;
            }

            WebPortVisuals.SetConfig(_layout.VisualConfig);
            _camera = _layout.MainCamera;
            DisableLegacyCameraControllersOnMainCamera();

            _cameraRig = _layout.CameraRig;
            _renderSystem = new WebPortRenderSystem(_layout);

            _ui = _layout.UiController;
            _ui.Build();
            _ui.CreateRoomRequested += () => _transport.CreateRoom();
            _ui.JoinRoomRequested += JoinRoom;
            _ui.StartGameRequested += () => _transport.StartGame();
            _ui.BackToLobbyRequested += BackToLobby;
            return true;
        }

        private void DisableLegacyCameraControllersOnMainCamera()
        {
            foreach (MonoBehaviour behaviour in _camera.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour is WebPortCameraRig)
                    continue;

                Type type = behaviour.GetType();
                if (type.FullName == "Camera.IsoFollowCamera")
                    behaviour.enabled = false;
            }
        }

        private void SetupTransport(IGameTransport transport)
        {
            _transport = transport;
            _localTransport = transport as LocalGameTransport;
            _transport.Connected += OnConnected;
            _transport.RoomStateChanged += OnRoomStateChanged;
            _transport.GameStarted += OnGameStarted;
            _transport.GameEnded += OnGameEnded;
            _transport.GameMessageReceived += OnGameMessage;
        }

        private void JoinRoom(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Trim().Length < 4)
            {
                _ui.ShowMenu("방 코드는 4자리입니다.");
                return;
            }

            _transport.JoinRoom(code);
        }

        private void BackToLobby()
        {
            _phase = GamePhase.Lobby;
            _ui.ShowLobby(_roomCode, _memberIds, _hostId, _selfId, _selfId == _hostId);
        }

        private void OnConnected(int id)
        {
            _selfId = id;
        }

        private void OnRoomStateChanged(RoomStatePayload payload)
        {
            _roomCode = payload.Code;
            _hostId = payload.HostId;
            _phase = GamePhase.Lobby;
            _memberIds.Clear();
            if (payload.MemberIds != null)
                _memberIds.AddRange(payload.MemberIds);

            _ui.ShowLobby(_roomCode, _memberIds, _hostId, _selfId, _selfId == _hostId);
        }

        private void OnGameStarted(GameStartPayload payload)
        {
            _phase = GamePhase.Playing;
            bool useAuthoredAnchors = _layout != null && _localTransport != null;
            _start = useAuthoredAnchors ? _layout.StartPosition : payload.Start;
            _goal = useAuthoredAnchors ? _layout.GetGoalPosition(0) : payload.Goal;
            _goalIndex = 0;
            _goalTimer = 0f;
            _sessionEndTime = payload.SessionEndTime;
            _sessionRemainMs = WebPortConstants.SessionDurationSeconds * 1000f;
            _nextPackageId = 0;
            _deliveredSinceTruck = 0;
            _truckBannerUntil = -1f;
            _cameraTargetBound = false;

            _players.Clear();
            _packages.Clear();
            _slots.Clear();
            _effects.Clear();

            if (payload.Players != null)
            {
                foreach (KeyValuePair<int, PlayerState> entry in payload.Players)
                    _players[entry.Key] = CopyPlayer(entry.Value);
            }

            if (!_players.ContainsKey(_selfId))
                _players[_selfId] = new PlayerState(_selfId, _start);
            else if (useAuthoredAnchors)
                ResetPlayerPosition(_players[_selfId], _start);

            if (payload.Packages != null)
            {
                foreach (KeyValuePair<int, PackageState> entry in payload.Packages)
                    _packages[entry.Key] = CopyPackage(entry.Value);
            }

            SetupSlots();
            _ui.ShowPlaying();
            Render(Time.time);
            TryBindCameraTarget();
        }

        private void OnGameEnded(IReadOnlyList<ScoreEntry> results)
        {
            _phase = GamePhase.Results;
            _ui.ShowResults(results, _selfId, _selfId == _hostId, _roomCode);
        }

        // Dispatches in-session relay messages (everything server.js forwards besides the
        // room-lifecycle ones already covered by Connected/RoomStateChanged/GameStarted/
        // GameEnded). Mirrors client/src/game/gameState.js's onMessage switch 1:1.
        private void OnGameMessage(JObject msg)
        {
            if (_phase != GamePhase.Playing)
                return;

            float now = Time.time;
            switch (msg["type"]?.Value<string>())
            {
                case "spawn": OnSpawnReceived(msg); break;
                case "move": OnMoveReceived(msg, now); break;
                case "boxUpdate": OnBoxUpdateReceived(msg, now); break;
                case "grab": OnGrabReceived(msg, now); break;
                case "push": OnPushReceived(msg, now); break;
                case "hit": OnHitReceived(msg); break;
                case "explode": OnExplodeReceived(msg, now); break;
                case "pickup": OnPickupReceived(msg); break;
                case "pickupRejected": OnPickupRejected(msg); break;
                case "truckDeparted": OnTruckDeparted(now); break;
                case "goalChanged": OnGoalChanged(msg); break;
                case "tick": OnTick(msg); break;
                case "leave": OnLeave(msg); break;
            }
        }

        private void OnSpawnReceived(JObject msg)
        {
            int id = msg["id"]!.Value<int>();
            PackageKind kind = PackageKindWire.FromWireString(msg["boxType"]!.Value<string>());
            Vector3 position = new(msg["x"]!.Value<float>(), msg["y"]!.Value<float>(), msg["z"]!.Value<float>());
            _packages[id] = new PackageState(id, kind, position);
        }

        private void OnMoveReceived(JObject msg, float now)
        {
            int from = msg["from"]!.Value<int>();
            if (from == _selfId)
                return;

            float newX = msg["x"]!.Value<float>();
            float newZ = msg["z"]!.Value<float>();

            if (!_players.TryGetValue(from, out PlayerState player))
            {
                player = new PlayerState(from, new Vector3(newX, 0f, newZ));
                _players[from] = player;
            }
            else if (player.TargetTime > 0f)
            {
                float dtS = now - player.TargetTime;
                if (dtS > 0.01f)
                    player.Velocity = new Vector3((newX - player.TargetPosition.x) / dtS, 0f, (newZ - player.TargetPosition.z) / dtS);
            }

            player.TargetPosition = new Vector3(newX, 0f, newZ);
            player.TargetTime = now;
            player.Angle = msg["angle"]!.Value<float>();
            player.Stunned = msg["stunned"]?.Value<bool>() ?? false;
            player.Rolling = msg["rolling"]?.Value<bool>() ?? false;
            player.Deliveries = msg["deliveries"]?.Value<int>() ?? 0;
        }

        private void OnBoxUpdateReceived(JObject msg, float now)
        {
            int from = msg["from"]!.Value<int>();
            if (from == _selfId)
                return;

            int id = msg["id"]!.Value<int>();
            // 내가 지금 실제로 들고 있는 상자는 경합으로 잘못 도착한 메시지로 뺏기지 않는다.
            if (_packages.TryGetValue(id, out PackageState existing) && existing.HeldBy == _selfId)
                return;

            float x = msg["x"]!.Value<float>();
            float y = msg["y"]!.Value<float>();
            float z = msg["z"]!.Value<float>();

            bool created = false;
            if (!_packages.TryGetValue(id, out PackageState package))
            {
                package = new PackageState(id, PackageKindWire.FromWireString(msg["boxType"]!.Value<string>()), new Vector3(x, y, z));
                _packages[id] = package;
                created = true;
            }

            package.Position = new Vector3(x, y, z);
            package.TargetPosition = package.Position;
            package.TargetTime = now;
            package.Velocity = new Vector3(msg["vx"]!.Value<float>(), msg["vy"]!.Value<float>(), msg["vz"]!.Value<float>());
            package.Rotation = new Vector3(
                msg["rx"]?.Value<float>() ?? 0f,
                msg["ry"]?.Value<float>() ?? 0f,
                msg["rz"]?.Value<float>() ?? 0f);
            if (created)
                package.RenderRotation = package.Rotation;
            package.TargetRotation = package.Rotation;
            package.AngularVelocity = new Vector3(
                msg["avx"]?.Value<float>() ?? 0f,
                msg["avy"]?.Value<float>() ?? 0f,
                msg["avz"]?.Value<float>() ?? 0f);
            package.Kind = PackageKindWire.FromWireString(msg["boxType"]!.Value<string>());
            package.HeldBy = msg["heldBy"]!.Type == JTokenType.Null ? null : msg["heldBy"]!.Value<int?>();
            package.Timer = msg["timer"]!.Value<float>();
            package.Delivered = msg["delivered"]!.Value<bool>();
            package.OwnerId = from;
        }

        private void OnGrabReceived(JObject msg, float now)
        {
            if (msg["targetId"]!.Value<int>() != _selfId)
                return;

            PlayerState self = Self;
            if (self == null)
                return;

            self.GrabbedBy = msg["from"]!.Value<int>();
            self.GrabTimer = WebPortConstants.GrabDuration;
            AddEffect(EffectKind.GrabFlash, self.Position, now, 0.3f);
        }

        private void OnPushReceived(JObject msg, float now)
        {
            if (msg["targetId"]!.Value<int>() != _selfId)
                return;

            PlayerState self = Self;
            if (self == null)
                return;

            float dirX = msg["dirX"]!.Value<float>();
            float dirZ = msg["dirZ"]!.Value<float>();
            float scale = msg["scale"]?.Value<float>() ?? 1f;

            self.ExternalVelocity += new Vector3(dirX, 0f, dirZ) * WebPortConstants.PushForce * scale;
            AddEffect(EffectKind.Impact, self.Position, now, 0.35f);

            int heldNow = CountHeld(self.Id);
            self.Instability = 100f;
            if (heldNow > 0 && !self.Stunned)
                Stun(self, WebPortConstants.StumbleStunDuration, WebPortConstants.StumbleStunDuration * 0.5f);

            DropHeldBoxes(self.Id);
        }

        private void OnHitReceived(JObject msg)
        {
            if (msg["targetId"]!.Value<int>() != _selfId)
                return;

            PlayerState self = Self;
            if (self == null)
                return;

            Stun(self, WebPortConstants.StunDuration, 0f);
            DropHeldBoxes(self.Id);
        }

        private void OnExplodeReceived(JObject msg, float now)
        {
            Vector3 position = new(msg["x"]!.Value<float>(), 0f, msg["z"]!.Value<float>());
            ApplyBlast(position, now);
            AddEffect(EffectKind.Explosion, position, now, 0.5f);
            _packages.Remove(msg["id"]!.Value<int>());
        }

        private void OnPickupReceived(JObject msg)
        {
            int from = msg["from"]!.Value<int>();
            if (from == _selfId)
                return;

            int id = msg["id"]!.Value<int>();
            if (!_packages.TryGetValue(id, out PackageState package))
            {
                package = new PackageState(id, PackageKind.Normal, Vector3.zero);
                _packages[id] = package;
            }

            package.HeldBy = from;
            package.OwnerId = from;
            package.Velocity = Vector3.zero;
            PlayerState holder = _players.TryGetValue(from, out PlayerState remoteHolder) ? remoteHolder : null;
            ResetPackageCarryRotation(package);
            Vector3 carryStartPosition = holder != null
                ? new Vector3(holder.Position.x, package.Position.y, holder.Position.z)
                : package.Position;
            package.Position = carryStartPosition;
            package.RenderPosition = package.Position;
            package.TargetPosition = package.Position;
        }

        private void OnPickupRejected(JObject msg)
        {
            int id = msg["id"]!.Value<int>();
            if (_packages.TryGetValue(id, out PackageState package) && package.HeldBy == _selfId)
            {
                package.HeldBy = null;
                package.OwnerId = null;
                package.Velocity = Vector3.zero;
                package.AngularVelocity = Vector3.zero;
                package.Position = new Vector3(package.Position.x, 0f, package.Position.z);
                package.RenderPosition = package.Position;
                package.RenderRotation = package.Rotation;
                package.TargetPosition = package.Position;
                package.TargetRotation = package.Rotation;
            }
        }

        private void OnTruckDeparted(float now)
        {
            foreach (int id in _packages.Where(p => p.Value.Delivered).Select(p => p.Key).ToList())
                _packages.Remove(id);

            _renderSystem.PlayTruck();
            _truckBannerUntil = now + 2.2f;
        }

        private void OnGoalChanged(JObject msg)
        {
            JObject goal = (JObject)msg["goal"];
            _goal = new Vector3(goal["x"]!.Value<float>(), 0f, goal["z"]!.Value<float>());
        }

        private void OnTick(JObject msg)
        {
            _sessionRemainMs = msg["remainMs"]!.Value<float>();
        }

        private void OnLeave(JObject msg)
        {
            _players.Remove(msg["id"]!.Value<int>());
        }

        private void UpdateMouseWorld()
        {
            if (_camera == null || Mouse.current == null)
                return;

            Vector2 screen = Mouse.current.position.ReadValue();
            Ray ray = _camera.ScreenPointToRay(screen);
            Plane plane = new(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance))
                _mouseWorld = ray.GetPoint(distance);
        }

        private void HandleInput(float now)
        {
            PlayerState self = Self;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            if (keyboard.eKey.wasPressedThisFrame)
                TryPickup();
            if (keyboard.qKey.wasPressedThisFrame)
                DoGrab(now);
            if (keyboard.spaceKey.wasPressedThisFrame)
                StartPushCharge(now);
            if (keyboard.spaceKey.wasReleasedThisFrame)
                ReleasePushCharge(now);
            if (mouse.leftButton.wasPressedThisFrame && !self.Stunned && !self.GrabbedBy.HasValue)
            {
                self.ChargingThrow = true;
                self.ThrowChargeStartedAt = now;
            }
            if (mouse.leftButton.wasReleasedThisFrame && self.ChargingThrow)
            {
                self.ChargingThrow = false;
                if (!self.Stunned && !self.GrabbedBy.HasValue)
                    ThrowHeldBoxes(now);
            }
        }

        private void TickGame(float dt, float now)
        {
            PlayerState self = Self;
            TickSession(now);
            TickRefills(now);
            TickPlayer(self, dt, now);
            ResolvePlayerObstacleCollision(self);
            ResolvePlayerPackageCollision(self);
            SimulateOwnedPackages(self, dt, now);
            SendNetworkState(dt, self);
            SmoothRenderPositions(dt);
            CleanupEffects(now);
        }

        // Goal rotation and session-end are server-authoritative when networked (driven by
        // 'goalChanged'/'gameEnded' messages via OnGameMessage/OnGameEnded). The local transport
        // has no real server, so it simulates both itself here.
        private void TickSession(float now)
        {
            if (_localTransport == null)
                return;

            _goalTimer += Time.deltaTime;
            if (_goalTimer >= WebPortConstants.GoalRotateSeconds)
            {
                _goalTimer = 0f;
                _goalIndex = (_goalIndex + 1) % WebPortConstants.GoalPositions.Length;
                _goal = _layout != null ? _layout.GetGoalPosition(_goalIndex) : WebPortConstants.GoalPositions[_goalIndex];
            }

            _sessionRemainMs = Mathf.Max(_sessionEndTime - now, 0f) * 1000f;
            if (now >= _sessionEndTime)
                EndGame();
        }

        // Throttled to ~30/s to match the reference client (gameState.js tickGame's send block).
        private void SendNetworkState(float dt, PlayerState self)
        {
            _sendTimer += dt;
            if (_sendTimer <= 0.033f)
                return;

            _sendTimer = 0f;
            _transport.Send(new MoveCommand
            {
                X = self.Position.x,
                Z = self.Position.z,
                Angle = self.Angle,
                Stunned = self.Stunned,
                Rolling = self.Rolling,
                Deliveries = self.Deliveries,
            });

            foreach (PackageState package in _packages.Values)
            {
                if (package.OwnerId == _selfId)
                    SendBoxUpdate(package);
            }
        }

        private void SendBoxUpdate(PackageState package)
        {
            _transport.Send(new BoxUpdateCommand
            {
                Id = package.Id,
                BoxType = PackageKindWire.ToWireString(package.Kind),
                X = package.Position.x,
                Y = package.Position.y,
                Z = package.Position.z,
                Vx = package.Velocity.x,
                Vy = package.Velocity.y,
                Vz = package.Velocity.z,
                Rx = package.Rotation.x,
                Ry = package.Rotation.y,
                Rz = package.Rotation.z,
                Avx = package.AngularVelocity.x,
                Avy = package.AngularVelocity.y,
                Avz = package.AngularVelocity.z,
                HeldBy = package.HeldBy,
                Timer = package.Timer,
                Delivered = package.Delivered,
            });
        }

        private void TickRefills(float now)
        {
            foreach (PackageSlot slot in _slots)
            {
                if (slot.PackageId.HasValue || slot.RefillAt <= 0f || now < slot.RefillAt)
                    continue;

                slot.PackageId = SpawnPackageAt(slot.Position);
                slot.RefillAt = 0f;
            }
        }

        private void TickPlayer(PlayerState self, float dt, float now)
        {
            if (self.Stunned)
            {
                self.StunTimer -= dt;
                if (self.StunTimer <= 0f)
                    self.Stunned = false;
            }

            if (self.RollTimer > 0f)
                self.RollTimer -= dt;
            self.Rolling = self.RollTimer > 0f;

            if (self.GrabCooldown > 0f)
                self.GrabCooldown -= dt;
            if (self.PushCooldown > 0f)
                self.PushCooldown -= dt;

            int heldCount = CountHeld(self.Id);
            self.SpeedMultiplier = 0f;
            self.CarefulWalk = false;

            if (!self.Stunned && !self.GrabbedBy.HasValue)
            {
                Vector2 input = ReadMoveInput();
                if (input.sqrMagnitude > 0.0001f)
                {
                    float speedMultiplier = Mathf.Max(1f - heldCount * WebPortConstants.HoldSpeedPenaltyPerBox, WebPortConstants.HoldSpeedFloor);
                    bool careful = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                    if (careful)
                        speedMultiplier *= WebPortConstants.CarefulWalkMultiplier;

                    if (self.PushCharging)
                    {
                        float ratio = Mathf.Clamp01((now - self.PushChargeStartedAt) / WebPortConstants.PushMaxChargeSeconds);
                        speedMultiplier *= Mathf.Max(1f - ratio * WebPortConstants.PushChargeSpeedPenaltyMax, WebPortConstants.PushChargeSpeedFloor);
                    }

                    self.SpeedMultiplier = speedMultiplier;
                    self.CarefulWalk = careful;
                    Vector3 move = GetCameraRelativeMove(input);
                    self.Position += move.normalized * WebPortConstants.PlayerSpeed * speedMultiplier * dt;
                    self.Position = WebPortConstants.ClampToCross(self.Position);
                }

                self.Angle = Mathf.Atan2(_mouseWorld.z - self.Position.z, _mouseWorld.x - self.Position.x);
            }

            TickInstability(self, heldCount, dt, now);
            TickGrab(self, dt);
            TickExternalVelocity(self, dt);
        }

        private static Vector2 ReadMoveInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed)
                input.y += 1f;
            if (keyboard.sKey.isPressed)
                input.y -= 1f;
            if (keyboard.aKey.isPressed)
                input.x -= 1f;
            if (keyboard.dKey.isPressed)
                input.x += 1f;
            return input;
        }

        private Vector3 GetCameraRelativeMove(Vector2 input)
        {
            if (_camera == null)
                return new Vector3(input.x, 0f, -input.y);

            Vector3 screenUp = _camera.transform.forward;
            screenUp.y = 0f;
            if (screenUp.sqrMagnitude < 0.0001f)
                screenUp = Vector3.back;
            else
                screenUp.Normalize();

            Vector3 screenRight = _camera.transform.right;
            screenRight.y = 0f;
            if (screenRight.sqrMagnitude < 0.0001f)
                screenRight = Vector3.right;
            else
                screenRight.Normalize();

            return screenRight * input.x + screenUp * input.y;
        }

        private void TickInstability(PlayerState self, int heldCount, float dt, float now)
        {
            int overBy = heldCount - WebPortConstants.StableHoldCount;
            if (overBy > 0 && self.SpeedMultiplier > 0f)
            {
                float carefulMultiplier = self.CarefulWalk ? WebPortConstants.CarefulInstabilityMultiplier : 1f;
                self.Instability = Mathf.Min(100f, self.Instability + overBy * WebPortConstants.InstabilityFillPerSecond * self.SpeedMultiplier * carefulMultiplier * dt);
            }
            else
            {
                self.Instability = Mathf.Max(0f, self.Instability - WebPortConstants.InstabilityDrainPerSecond * dt);
            }

            if (self.Instability >= 100f && !self.Stunned)
            {
                self.Instability = 0f;
                Stun(self, WebPortConstants.StumbleStunDuration, WebPortConstants.StumbleStunDuration * 0.5f);
                DropHeldBoxes(self.Id);
            }
        }

        private void TickGrab(PlayerState self, float dt)
        {
            if (self.GrabbedBy.HasValue)
            {
                self.GrabTimer -= dt;
                if (self.GrabTimer <= 0f)
                {
                    self.GrabbedBy = null;
                }
                else if (_players.TryGetValue(self.GrabbedBy.Value, out PlayerState attacker))
                {
                    float k = 1f - Mathf.Pow(WebPortConstants.GrabPullBase, dt);
                    self.Position = Vector3.Lerp(self.Position, attacker.RenderPosition, k);
                    self.Position = WebPortConstants.ClampToCross(self.Position);
                }
            }

            if (self.DraggingId.HasValue)
            {
                self.DragTimer -= dt;
                if (self.DragTimer <= 0f)
                    self.DraggingId = null;
            }
        }

        private void TickExternalVelocity(PlayerState self, float dt)
        {
            if (self.ExternalVelocity.sqrMagnitude <= 0.001f)
                return;

            self.Position += self.ExternalVelocity * dt;
            self.Position = WebPortConstants.ClampToCross(self.Position);
            float decay = Mathf.Pow(0.02f, dt);
            self.ExternalVelocity *= decay;
            if (self.ExternalVelocity.magnitude < 2f)
                self.ExternalVelocity = Vector3.zero;
        }

        private void ResolvePlayerObstacleCollision(PlayerState player)
        {
            for (int i = 0; i < GetObstacleCount(); i++)
            {
                ObstacleData obstacle = GetObstacle(i);
                Vector3 delta = player.Position - obstacle.Position;
                delta.y = 0f;
                float distance = delta.magnitude;
                float minimum = WebPortConstants.PlayerRadius + obstacle.Radius;
                if (distance < minimum && distance > 0.001f)
                {
                    player.Position += delta / distance * (minimum - distance);
                    player.Position = WebPortConstants.ClampToCross(player.Position);
                }
            }
        }

        private void ResolvePlayerPackageCollision(PlayerState self)
        {
            foreach (PackageState package in _packages.Values)
            {
                if (package.HeldBy.HasValue || package.Delivered || package.Position.y > 5f)
                    continue;

                Vector3 packageHalfExtents = GetPackageHalfExtents(package);
                float closestX = Mathf.Clamp(self.Position.x, package.Position.x - packageHalfExtents.x, package.Position.x + packageHalfExtents.x);
                float closestZ = Mathf.Clamp(self.Position.z, package.Position.z - packageHalfExtents.z, package.Position.z + packageHalfExtents.z);
                Vector3 delta = self.Position - new Vector3(closestX, 0f, closestZ);
                float distance = delta.magnitude;
                if (distance >= WebPortConstants.PlayerRadius || distance <= 0.001f)
                    continue;

                Vector3 normal = delta / distance;
                self.Position += normal * (WebPortConstants.PlayerRadius - distance);
                self.Position = WebPortConstants.ClampToCross(self.Position);

                if (!package.OwnerId.HasValue || package.OwnerId.Value == self.Id)
                {
                    package.OwnerId = self.Id;
                    package.Velocity = new Vector3(-normal.x * WebPortConstants.BoxPushSpeed, package.Velocity.y, -normal.z * WebPortConstants.BoxPushSpeed);
                    AddSpinFromPlanarVelocity(package, package.Velocity, 1f);
                }
            }
        }

        private void SimulateOwnedPackages(PlayerState self, float dt, float now)
        {
            List<PackageState> owned = _packages.Values.Where(p => p.OwnerId == self.Id).OrderBy(p => p.Id).ToList();
            foreach (PackageState package in owned)
                SimulatePackage(self, package, dt, now);
        }

        private void SimulatePackage(PlayerState self, PackageState package, float dt, float now)
        {
            if (package.HeldBy.HasValue)
            {
                PlayerState holder = _players.TryGetValue(package.HeldBy.Value, out PlayerState foundHolder) ? foundHolder : self;
                SimulateHeldPackage(holder, package, GetHeldStackBottomY(package, package.HeldBy.Value), dt);
                return;
            }

            package.Position += package.Velocity * dt;

            if (package.Position.y > 0f || Mathf.Abs(package.Velocity.y) > 0.001f)
            {
                package.Velocity += Vector3.down * WebPortConstants.ThrowGravity * dt;
                if (package.Position.y <= 0f)
                {
                    package.Position = new Vector3(package.Position.x, 0f, package.Position.z);
                    package.Velocity = new Vector3(package.Velocity.x, 0f, package.Velocity.z);
                }
            }
            else
            {
                float decay = Mathf.Pow(WebPortConstants.FrictionRetain, dt);
                package.Velocity = new Vector3(package.Velocity.x * decay, 0f, package.Velocity.z * decay);
            }

            ApplyGravityPackages(package, dt);
            package.Position = WebPortConstants.ClampToCross(package.Position, 10f);
            ResolvePackageObstacleCollision(package);
            ResolvePackagePackageCollision(package);
            TickPackageRotation(package, dt);
            CheckDelivery(self, package, now);
            CheckPackageHitPlayers(package);
            TickBomb(package, now);
        }

        private static void SimulateHeldPackage(PlayerState holder, PackageState package, float targetY, float dt)
        {
            Vector3 position = new(holder.Position.x, package.Position.y, holder.Position.z);
            Vector3 velocity = new(0f, package.Velocity.y, 0f);

            if (position.y > targetY)
            {
                position += Vector3.up * velocity.y * dt;
                velocity += Vector3.down * WebPortConstants.ThrowGravity * dt;

                if (position.y <= targetY)
                {
                    position = new Vector3(position.x, targetY, position.z);
                    velocity = Vector3.zero;
                }
            }
            else if (position.y < targetY)
            {
                velocity.y = Mathf.MoveTowards(velocity.y, WebPortConstants.PackageCarryLiftSpeed, WebPortConstants.PackageCarryLiftAcceleration * dt);
                position += Vector3.up * velocity.y * dt;

                if (position.y >= targetY)
                {
                    position = new Vector3(position.x, targetY, position.z);
                    velocity = Vector3.zero;
                }
            }
            else
            {
                position = new Vector3(position.x, targetY, position.z);
                velocity = Vector3.zero;
            }

            package.Position = position;
            package.Velocity = velocity;
            ResetPackageCarryRotation(package);
            package.RenderPosition = position;
        }

        private float GetHeldStackBottomY(PackageState package, int holderId)
        {
            float bottomY = WebPortConstants.CarryHeight;
            foreach (PackageState sibling in _packages.Values.Where(p => p.HeldBy == holderId).OrderBy(p => p.Id))
            {
                if (sibling == package)
                    return bottomY;

                bottomY += GetPackageStackHeight(sibling);
            }

            return bottomY;
        }

        private static float GetPackageStackHeight(PackageState package)
        {
            Vector3 halfExtents = GetPackageHalfExtents(package);
            return Mathf.Max(halfExtents.y * 2f, 1f);
        }

        private static void ResetPackageCarryRotation(PackageState package)
        {
            package.Rotation = Vector3.zero;
            package.RenderRotation = Vector3.zero;
            package.TargetRotation = Vector3.zero;
            package.AngularVelocity = Vector3.zero;
        }

        private static void TickPackageRotation(PackageState package, float dt)
        {
            bool grounded = package.Position.y <= 0.01f;
            Vector3 planarVelocity = new(package.Velocity.x, 0f, package.Velocity.z);

            if (!grounded)
            {
                if (package.AngularVelocity.sqrMagnitude > 0.01f)
                    package.Rotation = NormalizeEuler(package.Rotation + package.AngularVelocity * dt);

                package.AngularVelocity *= Mathf.Pow(WebPortConstants.PackageAirAngularRetain, dt);
            }
            else
            {
                if (planarVelocity.sqrMagnitude <= WebPortConstants.PackageGroundStopSpeed * WebPortConstants.PackageGroundStopSpeed)
                    package.Velocity = new Vector3(0f, package.Velocity.y, 0f);

                Quaternion currentRotation = Quaternion.Euler(package.Rotation);
                if (Mathf.Abs(package.AngularVelocity.y) > 0.01f)
                    currentRotation = Quaternion.AngleAxis(package.AngularVelocity.y * dt, Vector3.up) * currentRotation;

                Quaternion restRotation = GetClosestPackageRestRotation(package.Kind, currentRotation);
                float uprightBlend = 1f - Mathf.Exp(-WebPortConstants.PackageGroundUprightLerp * dt);
                Quaternion settledRotation = Quaternion.Slerp(currentRotation, restRotation, uprightBlend);
                if (Quaternion.Angle(settledRotation, restRotation) <= WebPortConstants.PackageGroundSnapAngle)
                    settledRotation = restRotation;

                package.Rotation = NormalizeEuler(settledRotation.eulerAngles);

                float yawVelocity = package.AngularVelocity.y * Mathf.Pow(WebPortConstants.PackageGroundAngularRetain, dt);
                package.AngularVelocity = new Vector3(0f, yawVelocity, 0f);
            }

            if (package.AngularVelocity.sqrMagnitude < 1f)
                package.AngularVelocity = Vector3.zero;

            package.RenderRotation = package.Rotation;
        }

        private static Quaternion GetClosestPackageRestRotation(PackageKind kind, Quaternion simulationRotation)
        {
            Quaternion baseRotation = WebPortVisuals.Config.GetPackageVisualBaseRotation(kind);
            Quaternion visualRotation = baseRotation * simulationRotation;
            Vector3 localUp = Quaternion.Inverse(visualRotation) * Vector3.up;
            Vector3 faceNormal;

            if (Mathf.Abs(localUp.x) >= Mathf.Abs(localUp.y) && Mathf.Abs(localUp.x) >= Mathf.Abs(localUp.z))
                faceNormal = localUp.x >= 0f ? Vector3.right : Vector3.left;
            else if (Mathf.Abs(localUp.y) >= Mathf.Abs(localUp.z))
                faceNormal = localUp.y >= 0f ? Vector3.up : Vector3.down;
            else
                faceNormal = localUp.z >= 0f ? Vector3.forward : Vector3.back;

            Quaternion alignFaceToGround = Quaternion.FromToRotation(visualRotation * faceNormal, Vector3.up);
            Quaternion restingVisualRotation = alignFaceToGround * visualRotation;
            return Quaternion.Inverse(baseRotation) * restingVisualRotation;
        }

        private static void AddSpinFromPlanarVelocity(PackageState package, Vector3 velocity, float scale)
        {
            Vector3 planarVelocity = new(velocity.x, 0f, velocity.z);
            if (planarVelocity.sqrMagnitude < 1f)
                return;

            Vector3 rollAxis = Vector3.Cross(Vector3.up, planarVelocity.normalized);
            float speed = planarVelocity.magnitude;
            float yawSign = Mathf.Sin(package.Id * 12.9898f + planarVelocity.x * 0.031f + planarVelocity.z * 0.047f) >= 0f ? 1f : -1f;
            Vector3 spin = rollAxis * speed * WebPortConstants.PackageSpinVelocityScale * scale;
            spin += Vector3.up * speed * WebPortConstants.PackageSpinYawScale * yawSign * scale;
            package.AngularVelocity = ClampAngularVelocity(package.AngularVelocity + spin);
        }

        private static void AddSpinFromImpact(PackageState package, Vector3 normal, float impactSpeed)
        {
            if (impactSpeed <= 1f)
                return;

            Vector3 tangentAxis = Vector3.Cross(normal, Vector3.up);
            if (tangentAxis.sqrMagnitude <= 0.001f)
                tangentAxis = Vector3.forward;

            package.AngularVelocity = ClampAngularVelocity(package.AngularVelocity + tangentAxis.normalized * impactSpeed * WebPortConstants.PackageImpactSpinScale);
        }

        private static Vector3 ClampAngularVelocity(Vector3 angularVelocity)
        {
            float max = WebPortConstants.PackageMaxAngularSpeed;
            return angularVelocity.sqrMagnitude > max * max ? angularVelocity.normalized * max : angularVelocity;
        }

        private static Vector3 NormalizeEuler(Vector3 euler)
        {
            return new Vector3(
                Mathf.Repeat(euler.x + 180f, 360f) - 180f,
                Mathf.Repeat(euler.y + 180f, 360f) - 180f,
                Mathf.Repeat(euler.z + 180f, 360f) - 180f);
        }

        private void ApplyGravityPackages(PackageState package, float dt)
        {
            if (package.Kind == PackageKind.Gravity || package.Position.y > 5f)
                return;

            foreach (PackageState other in _packages.Values)
            {
                if (other == package || other.Kind != PackageKind.Gravity || other.HeldBy.HasValue || other.Delivered)
                    continue;

                Vector3 delta = other.Position - package.Position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance < WebPortConstants.GravityRadius && distance > 5f)
                    package.Position += delta / distance * WebPortConstants.GravityPull * dt;
            }
        }

        private void ResolvePackageObstacleCollision(PackageState package)
        {
            for (int i = 0; i < GetObstacleCount(); i++)
            {
                ObstacleData obstacle = GetObstacle(i);
                Vector3 delta = package.Position - obstacle.Position;
                delta.y = 0f;
                float distance = delta.magnitude;
                float minimum = obstacle.Radius + GetPackageRadius(package);
                if (distance < minimum && distance > 0.001f)
                {
                    Vector3 normal = delta / distance;
                    package.Position = obstacle.Position + normal * minimum + Vector3.up * package.Position.y;
                    float vn = Vector3.Dot(package.Velocity, normal);
                    float impactSpeed = Mathf.Abs(vn);
                    package.Velocity = (package.Velocity - 2f * vn * normal) * WebPortConstants.PackageCollisionBounceRetain;
                    AddSpinFromImpact(package, normal, impactSpeed);
                }
            }
        }

        private int GetObstacleCount()
        {
            return _layout != null && _layout.ObstacleCount > 0 ? _layout.ObstacleCount : WebPortConstants.Obstacles.Length;
        }

        private ObstacleData GetObstacle(int index)
        {
            return _layout != null && _layout.ObstacleCount > 0 ? _layout.GetObstacle(index) : WebPortConstants.Obstacles[index];
        }

        private void ResolvePackagePackageCollision(PackageState package)
        {
            if (package.Position.y > 5f)
                return;

            foreach (PackageState other in _packages.Values)
            {
                if (other == package || other.HeldBy.HasValue || other.Delivered)
                    continue;

                Vector3 delta = package.Position - other.Position;
                delta.y = 0f;
                float distance = delta.magnitude;
                float minimum = GetPackageRadius(package) + GetPackageRadius(other);
                if (distance > 0.001f && distance < minimum)
                {
                    Vector3 normal = delta / distance;
                    package.Position += normal * (minimum - distance);
                    float closingSpeed = Mathf.Max(-Vector3.Dot(package.Velocity - other.Velocity, normal), 0f);
                    AddSpinFromImpact(package, normal, closingSpeed);
                }
                else if (distance <= 0.001f)
                {
                    float angle = Mathf.Repeat(package.Id * 137.5f + other.Id * 53.3f, 360f) * Mathf.Deg2Rad;
                    package.Position += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * minimum;
                }
            }
        }

        private static Vector3 GetPackageHalfExtents(PackageState package)
        {
            return WebPortVisuals.Config.GetPackageCollisionHalfExtents(package.Kind);
        }

        private static float GetPackageRadius(PackageState package)
        {
            return WebPortVisuals.Config.GetPackageCollisionRadius(package.Kind);
        }

        private void CheckDelivery(PlayerState self, PackageState package, float now)
        {
            if (package.Delivered || package.Position.y > 0f)
                return;

            Vector3 delta = package.Position - _goal;
            delta.y = 0f;
            if (delta.magnitude >= WebPortConstants.GoalRadius)
                return;

            package.Delivered = true;
            package.Velocity = Vector3.zero;
            package.AngularVelocity = Vector3.zero;
            self.Deliveries += package.Kind == PackageKind.High ? 5 : 1;
            _deliveredSinceTruck++;
            AddEffect(EffectKind.Deliver, package.Position, now, 0.9f);

            if (_deliveredSinceTruck >= WebPortConstants.TruckThreshold)
            {
                _deliveredSinceTruck = 0;
                foreach (int id in _packages.Where(p => p.Value.Delivered).Select(p => p.Key).ToList())
                    _packages.Remove(id);
                _renderSystem.PlayTruck();
                _truckBannerUntil = now + 2.2f;
            }
        }

        private void CheckPackageHitPlayers(PackageState package)
        {
            Vector3 planarVelocity = new(package.Velocity.x, 0f, package.Velocity.z);
            if (package.Delivered || planarVelocity.magnitude <= WebPortConstants.HitSpeed)
                return;

            foreach (PlayerState player in _players.Values)
            {
                if (player.Id == _selfId || player.Stunned)
                    continue;

                Vector3 delta = package.Position - player.RenderPosition;
                delta.y = 0f;
                if (delta.magnitude >= WebPortConstants.HitRadius)
                    continue;

                package.Velocity = Vector3.zero;
                package.AngularVelocity = Vector3.zero;
                package.Position = new Vector3(package.Position.x, 0f, package.Position.z);
                // Notify the target instead of stunning them directly - they apply it to
                // themselves on receipt (OnHitReceived), same reasoning as push above.
                _transport.Send(new HitCommand { TargetId = player.Id, X = package.Position.x, Z = package.Position.z });
                break;
            }
        }

        private void TickBomb(PackageState package, float now)
        {
            if (package.Kind != PackageKind.Bomb)
                return;

            package.Timer -= Time.deltaTime;
            if (package.Timer > 0f)
                return;

            ApplyBlast(package.Position, now);
            AddEffect(EffectKind.Explosion, package.Position, now, 0.5f);
            _packages.Remove(package.Id);
        }

        // Remote players/packages are dead-reckoned from their last known position + estimated
        // velocity, then smoothed toward that prediction (mirrors gameState.js tickGame's
        // "다른 플레이어: 추정 속도로 외삽(dead-reckoning) 후 LERP" block).
        private void SmoothRenderPositions(float dt)
        {
            float now = Time.time;
            float lerpFactor = 1f - Mathf.Pow(0.65f, dt * 60f);

            foreach (PlayerState player in _players.Values)
            {
                if (player.Id == _selfId)
                {
                    player.RenderPosition = player.Position;
                    continue;
                }

                float extrapSeconds = Mathf.Min(now - player.TargetTime, WebPortConstants.ExtrapolateCapSeconds);
                Vector3 predicted = player.TargetPosition + player.Velocity * extrapSeconds;
                player.RenderPosition = Vector3.Lerp(player.RenderPosition, predicted, lerpFactor);
            }

            foreach (PackageState package in _packages.Values)
            {
                if (package.OwnerId == _selfId)
                {
                    package.RenderPosition = package.Position;
                    package.RenderRotation = package.Rotation;
                    continue;
                }

                float extrapSeconds = Mathf.Min(now - package.TargetTime, WebPortConstants.ExtrapolateCapSeconds);
                float predictedX = package.TargetPosition.x + package.Velocity.x * extrapSeconds;
                float predictedZ = package.TargetPosition.z + package.Velocity.z * extrapSeconds;
                Vector3 predictedRotation = package.TargetRotation + package.AngularVelocity * extrapSeconds;
                float renderX = Mathf.Lerp(package.RenderPosition.x, predictedX, lerpFactor);
                float renderZ = Mathf.Lerp(package.RenderPosition.z, predictedZ, lerpFactor);
                package.RenderPosition = new Vector3(renderX, package.TargetPosition.y, renderZ);
                package.RenderRotation = LerpEuler(package.RenderRotation, predictedRotation, lerpFactor);
            }
        }

        private static Vector3 LerpEuler(Vector3 current, Vector3 target, float t)
        {
            Quaternion rotation = Quaternion.Slerp(Quaternion.Euler(current), Quaternion.Euler(target), t);
            return NormalizeEuler(rotation.eulerAngles);
        }

        private void TryPickup()
        {
            PlayerState self = Self;
            if (self == null || self.Stunned || self.GrabbedBy.HasValue || CountHeld(self.Id) >= WebPortConstants.MaxHold)
                return;

            PackageState nearest = null;
            float nearestDistance = WebPortConstants.PickupRange;
            foreach (PackageState package in _packages.Values)
            {
                if (package.HeldBy.HasValue || package.Delivered)
                    continue;

                Vector3 delta = package.Position - self.Position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = package;
                }
            }

            if (nearest == null)
                return;

            // Optimistic local claim; the server may still reject this via 'pickupRejected'
            // if another client's pickup for the same box arrived first (see OnPickupRejected).
            nearest.HeldBy = self.Id;
            nearest.OwnerId = self.Id;
            nearest.Velocity = Vector3.zero;
            ResetPackageCarryRotation(nearest);
            nearest.Position = new Vector3(self.Position.x, nearest.Position.y, self.Position.z);
            nearest.RenderPosition = nearest.Position;
            nearest.TargetPosition = nearest.Position;
            ClearSlotForPackage(nearest.Id);
            _transport.Send(new PickupCommand { Id = nearest.Id });
        }

        private void DoGrab(float now)
        {
            PlayerState self = Self;
            if (self == null || self.Stunned || self.GrabbedBy.HasValue || self.DraggingId.HasValue || self.GrabCooldown > 0f)
                return;

            self.GrabCooldown = WebPortConstants.GrabCooldown;
            AddEffect(EffectKind.Swing, self.Position, now, 0.25f, self.Angle);
            AddEffect(EffectKind.Sweep, self.Position, now, 0.22f, self.Angle, length: WebPortConstants.GrabRange);

            PlayerState target = FindFrontalTarget(WebPortConstants.GrabRange, WebPortConstants.GrabCone);
            if (target == null)
                return;

            // Only track the drag locally; GrabbedBy/GrabTimer on the target belong to their
            // own client and are set there when they receive this 'grab' message (OnGrabReceived).
            self.DraggingId = target.Id;
            self.DragTimer = WebPortConstants.GrabDuration;
            AddEffect(EffectKind.GrabFlash, target.Position, now, 0.3f);
            _transport.Send(new GrabCommand { TargetId = target.Id });
        }

        private void StartPushCharge(float now)
        {
            PlayerState self = Self;
            if (self == null || self.Stunned || self.GrabbedBy.HasValue || self.PushCooldown > 0f || self.PushCharging)
                return;

            self.PushCharging = true;
            self.PushChargeStartedAt = now;
        }

        private void ReleasePushCharge(float now)
        {
            PlayerState self = Self;
            if (self == null || !self.PushCharging)
                return;

            self.PushCharging = false;
            if (self.Stunned || self.GrabbedBy.HasValue)
                return;

            float held = Mathf.Min(now - self.PushChargeStartedAt, WebPortConstants.PushMaxChargeSeconds);
            float ratio = held / WebPortConstants.PushMaxChargeSeconds;
            float scale = WebPortConstants.PushForceMinScale + ratio * (WebPortConstants.PushForceMaxScale - WebPortConstants.PushForceMinScale);
            float radius = WebPortConstants.PushRange * (1f + ratio * (WebPortConstants.PushRangeMaxScale - 1f));
            self.PushCooldown = WebPortConstants.PushCooldown;
            AddEffect(EffectKind.Shockwave, self.Position, now, 0.32f, self.Angle, radius);

            foreach (PlayerState target in _players.Values)
            {
                if (target.Id == self.Id)
                    continue;

                Vector3 delta = target.RenderPosition - self.Position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance >= radius || distance <= 0.01f)
                    continue;

                float diff = Mathf.Abs(Mathf.DeltaAngle(self.Angle * Mathf.Rad2Deg, Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (diff > WebPortConstants.PushArcHalfAngle)
                    continue;

                // Only send the push; the actual knockback/instability/drop is applied on the
                // target's own client when they receive it (OnPushReceived) - mirrors gameState.js,
                // where a client never mutates another player's authoritative state directly.
                Vector3 direction = delta / distance;
                _transport.Send(new PushCommand { TargetId = target.Id, DirX = direction.x, DirZ = direction.z, Scale = scale });
                AddEffect(EffectKind.Impact, target.Position, now, 0.35f);
            }
        }

        private void ThrowHeldBoxes(float now)
        {
            PlayerState self = Self;
            if (self == null)
                return;

            float held = Mathf.Min(now - self.ThrowChargeStartedAt, WebPortConstants.MaxChargeSeconds);
            float power = (held / WebPortConstants.MaxChargeSeconds) * WebPortConstants.MaxPower;
            float angle = Mathf.Atan2(_mouseWorld.z - self.Position.z, _mouseWorld.x - self.Position.x);
            List<PackageState> heldPackages = _packages.Values.Where(p => p.HeldBy == self.Id).OrderBy(p => p.Id).ToList();
            int count = heldPackages.Count;

            for (int i = 0; i < count; i++)
            {
                PackageState package = heldPackages[i];
                package.HeldBy = null;
                package.OwnerId = self.Id;
                float packagePower = package.Kind == PackageKind.Gravity ? power * 0.5f : power;
                float spread = (i - (count - 1) * 0.5f) * 0.15f;
                package.Velocity = new Vector3(
                    Mathf.Cos(angle + spread) * packagePower,
                    packagePower * WebPortConstants.ThrowVyFactor,
                    Mathf.Sin(angle + spread) * packagePower);
                AddSpinFromPlanarVelocity(package, package.Velocity, 1.25f + i * 0.12f);
                SendBoxUpdate(package);
            }
        }

        // Every client that owns a bomb detonates it locally and broadcasts 'explode'; every
        // client that receives 'explode' (including this one, for its own bomb) calls this
        // again. So each call must only affect this client's own player + owned packages -
        // never reach into other players' state directly (mirrors gameState.js applyBlast,
        // which only ever touches state.me and packages it owns).
        private void ApplyBlast(Vector3 position, float now)
        {
            PlayerState self = Self;
            if (self != null)
            {
                Vector3 delta = self.Position - position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance < WebPortConstants.BlastRadius)
                {
                    Vector3 direction = distance > 0.001f ? delta / distance : Vector3.forward;
                    float force = (WebPortConstants.BlastRadius - distance) * WebPortConstants.BlastScale;
                    if (distance < WebPortConstants.DirectHitRadius)
                    {
                        force *= WebPortConstants.BombDirectScale;
                        Stun(self, WebPortConstants.BombStunDuration, WebPortConstants.BombStunDuration);
                    }

                    self.ExternalVelocity += direction * force;
                }
            }

            foreach (PackageState package in _packages.Values)
            {
                if (package.OwnerId != _selfId || package.HeldBy.HasValue || package.Delivered)
                    continue;

                Vector3 delta = package.Position - position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance < WebPortConstants.BlastRadius && distance > 0.001f)
                {
                    Vector3 direction = delta / distance;
                    float force = (WebPortConstants.BlastRadius - distance) * WebPortConstants.BlastScale;
                    Vector3 impulse = direction * force * WebPortConstants.PackageExternalImpulseScale;
                    package.Velocity += impulse;
                    AddSpinFromPlanarVelocity(package, impulse, 1.1f);
                }
            }
        }

        private PlayerState FindFrontalTarget(float range, float halfCone)
        {
            PlayerState self = Self;
            PlayerState best = null;
            float bestDistance = range;

            foreach (PlayerState player in _players.Values)
            {
                if (player.Id == self.Id)
                    continue;

                Vector3 delta = player.RenderPosition - self.Position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance > range)
                    continue;

                float diff = Mathf.Abs(Mathf.DeltaAngle(self.Angle * Mathf.Rad2Deg, Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (diff > halfCone)
                    continue;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = player;
                }
            }

            return best;
        }

        private void DropHeldBoxes(int playerId)
        {
            foreach (PackageState package in _packages.Values)
            {
                if (package.HeldBy != playerId)
                    continue;

                package.HeldBy = null;
                package.OwnerId = playerId;
                package.Position = new Vector3(package.Position.x, Mathf.Max(package.Position.y, 0f), package.Position.z);
                package.RenderPosition = package.Position;
                package.RenderRotation = package.Rotation;
                package.Velocity = new Vector3(0f, package.Velocity.y, 0f);
                package.AngularVelocity = Vector3.zero;
                // Send immediately rather than waiting for the throttled tick, so the box
                // doesn't sit in a "still held" state on other clients for up to 33ms longer.
                SendBoxUpdate(package);
            }
        }

        private void Stun(PlayerState player, float duration, float rollDuration)
        {
            player.Stunned = true;
            player.StunTimer = Mathf.Max(player.StunTimer, duration);
            player.RollTimer = Mathf.Max(player.RollTimer, rollDuration);
        }

        private int CountHeld(int playerId)
        {
            int count = 0;
            foreach (PackageState package in _packages.Values)
            {
                if (package.HeldBy == playerId)
                    count++;
            }

            return count;
        }

        private void SetupSlots()
        {
            _slots.Clear();
            int layoutSlotCount = _layout != null ? _layout.PackageSpawnPointCount : 0;
            if (layoutSlotCount > 0)
            {
                for (int i = 0; i < layoutSlotCount; i++)
                {
                    Vector3 position = _layout.GetPackageSpawnPosition(i);
                    PackageSlot slot = new()
                    {
                        Position = position,
                    };
                    slot.PackageId = SpawnPackageAt(position);
                    _slots.Add(slot);
                }

                return;
            }

            for (int row = 0; row < WebPortConstants.SlotRows; row++)
            {
                for (int col = 0; col < WebPortConstants.SlotColumns; col++)
                {
                    Vector3 position = _start + new Vector3(
                        (col - (WebPortConstants.SlotColumns - 1) * 0.5f) * WebPortConstants.SlotSpacing,
                        0f,
                        (row - (WebPortConstants.SlotRows - 1) * 0.5f) * WebPortConstants.SlotSpacing);

                    PackageSlot slot = new()
                    {
                        Position = position,
                    };
                    slot.PackageId = SpawnPackageAt(position);
                    _slots.Add(slot);
                }
            }
        }

        private int SpawnPackageAt(Vector3 position)
        {
            int id = _nextPackageId++;
            float r = UnityEngine.Random.value;
            PackageKind kind = r switch
            {
                < 0.65f => PackageKind.Normal,
                < 0.69f => PackageKind.High,
                < 0.89f => PackageKind.Bomb,
                _ => PackageKind.Gravity,
            };

            _packages[id] = new PackageState(id, kind, position);
            return id;
        }

        private void ClearSlotForPackage(int packageId)
        {
            foreach (PackageSlot slot in _slots)
            {
                if (slot.PackageId != packageId)
                    continue;

                slot.PackageId = null;
                slot.RefillAt = Time.time + WebPortConstants.RefillDelaySeconds;
                return;
            }
        }

        private void AddEffect(EffectKind kind, Vector3 position, float now, float duration, float angle = 0f, float radius = 0f, float length = 0f)
        {
            _effects.Add(new EffectEvent
            {
                Kind = kind,
                Position = position,
                StartedAt = now,
                Duration = duration,
                Angle = angle,
                Radius = radius,
                Length = length,
            });
        }

        private void CleanupEffects(float now)
        {
            _effects.RemoveAll(e => now - e.StartedAt >= e.Duration);
        }

        private void Render(float now)
        {
            _renderSystem.Update(_players, _packages, _effects, _start, _goal, _mouseWorld, _selfId, _camera, now);
            TryBindCameraTarget();
        }

        private void TryBindCameraTarget()
        {
            if (_cameraTargetBound)
                return;

            Transform target = _renderSystem.GetPlayerTransform(_selfId);
            if (target == null)
                return;

            _cameraRig.SetTarget(target);
            _cameraTargetBound = true;
        }

        private void UpdateHud()
        {
            PlayerState self = Self;
            Vector3 goalDelta = _goal - self.Position;
            float bearing = Mathf.Atan2(goalDelta.x, -goalDelta.z) * Mathf.Rad2Deg;
            int goalDistance = Mathf.RoundToInt(new Vector2(goalDelta.x, goalDelta.z).magnitude);
            float remain = _sessionRemainMs / 1000f;
            List<ScoreEntry> scores = _players.Values
                .Select(p => new ScoreEntry(p.Id, p.Deliveries))
                .OrderByDescending(s => s.Deliveries)
                .ToList();

            _ui.UpdateHud(
                scores,
                _selfId,
                bearing,
                goalDistance,
                CountHeld(self.Id),
                WebPortConstants.MaxHold,
                WebPortConstants.StableHoldCount,
                self.Instability,
                remain,
                Time.time < _truckBannerUntil);
        }

        // Networked sessions end when the server broadcasts 'gameEnded' (see OnGameEnded via
        // IGameTransport.GameEnded) - the client never decides this for itself. Only the local
        // transport, which has no real server behind it, ends its own session here.
        private void EndGame()
        {
            if (_phase != GamePhase.Playing || _localTransport == null)
                return;

            List<ScoreEntry> results = _players.Values
                .Select(p => new ScoreEntry(p.Id, p.Deliveries))
                .OrderByDescending(s => s.Deliveries)
                .ToList();

            _localTransport.EndGame(results);
        }

        private static PlayerState CopyPlayer(PlayerState source)
        {
            PlayerState copy = new(source.Id, source.Position)
            {
                RenderPosition = source.RenderPosition,
                TargetPosition = source.TargetPosition,
                Velocity = source.Velocity,
                Angle = source.Angle,
                Stunned = source.Stunned,
                Rolling = source.Rolling,
                Deliveries = source.Deliveries,
            };
            return copy;
        }

        private static void ResetPlayerPosition(PlayerState player, Vector3 position)
        {
            player.Position = position;
            player.RenderPosition = position;
            player.TargetPosition = position;
            player.Velocity = Vector3.zero;
            player.ExternalVelocity = Vector3.zero;
        }

        private static PackageState CopyPackage(PackageState source)
        {
            return new PackageState(source.Id, source.Kind, source.Position)
            {
                RenderPosition = source.RenderPosition,
                TargetPosition = source.TargetPosition,
                TargetTime = source.TargetTime,
                Velocity = source.Velocity,
                Rotation = source.Rotation,
                RenderRotation = source.RenderRotation,
                TargetRotation = source.TargetRotation,
                AngularVelocity = source.AngularVelocity,
                HeldBy = source.HeldBy,
                OwnerId = source.OwnerId,
                Timer = source.Timer,
                Delivered = source.Delivered,
            };
        }
    }
}
