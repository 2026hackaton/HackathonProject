using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Hackathon.WebPort
{
    // Talks to the same relay server as the web client (server.js): the server only
    // arbitrates room lifecycle + pickup races, everything else is relayed as-is and
    // simulated client-side. See client/src/game/gameState.js for the reference protocol.
    public sealed class WebSocketGameTransport : IGameTransport
    {
        private readonly string _url;
        private readonly ConcurrentQueue<JObject> _incoming = new();
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;

        public event Action<int> Connected;
        public event Action<RoomStatePayload> RoomStateChanged;
        public event Action<GameStartPayload> GameStarted;
        public event Action<IReadOnlyList<ScoreEntry>> GameEnded;
        public event Action<JObject> GameMessageReceived;

        public int SelfId { get; private set; }
        public bool IsConnected { get; private set; }

        public WebSocketGameTransport(string url)
        {
            _url = url;
        }

        public async void Connect()
        {
            if (_socket != null)
                return;

            _socket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            try
            {
                await _socket.ConnectAsync(new Uri(_url), _cts.Token);
                IsConnected = true;
                _ = ReceiveLoop(_cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebSocketGameTransport] connect failed: {e.Message}");
            }
        }

        public void CreateRoom() => SendRaw(new JObject { ["type"] = "createRoom" });

        public void JoinRoom(string code) => SendRaw(new JObject { ["type"] = "joinRoom", ["code"] = code });

        public void StartGame() => SendRaw(new JObject { ["type"] = "startGame" });

        public void Send(GameClientCommand command) => SendRaw(command.ToJson());

        public void Pump()
        {
            while (_incoming.TryDequeue(out JObject msg))
                Dispatch(msg);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _socket?.Dispose();
            IsConnected = false;
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            StringBuilder textBuffer = new();
            try
            {
                while (_socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    WebSocketReceiveResult result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    textBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage)
                        continue;

                    string json = textBuffer.ToString();
                    textBuffer.Clear();
                    try
                    {
                        _incoming.Enqueue(JObject.Parse(json));
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[WebSocketGameTransport] bad json: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    Debug.LogError($"[WebSocketGameTransport] receive loop error: {e.Message}");
            }
            finally
            {
                IsConnected = false;
            }
        }

        private async void SendRaw(JObject payload)
        {
            if (_socket == null || _socket.State != WebSocketState.Open)
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString(Newtonsoft.Json.Formatting.None));
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebSocketGameTransport] send failed: {e.Message}");
            }
        }

        // Runs on the main thread (called from Pump), unlike ReceiveLoop which runs on a
        // background task - safe to raise events here.
        private void Dispatch(JObject msg)
        {
            string type = msg["type"]?.Value<string>();
            switch (type)
            {
                case "hello":
                    SelfId = msg["id"]!.Value<int>();
                    Connected?.Invoke(SelfId);
                    break;
                case "roomState":
                    RoomStateChanged?.Invoke(ParseRoomState(msg));
                    break;
                case "gameStarted":
                    GameStarted?.Invoke(ParseGameStarted(msg));
                    break;
                case "gameEnded":
                    GameEnded?.Invoke(ParseResults(msg));
                    break;
                case "joinFailed":
                    Debug.LogWarning($"[WebSocketGameTransport] join failed: {msg["reason"]}");
                    break;
                default:
                    GameMessageReceived?.Invoke(msg);
                    break;
            }
        }

        private static RoomStatePayload ParseRoomState(JObject msg) => new()
        {
            Code = msg["code"]!.Value<string>(),
            HostId = msg["hostId"]!.Value<int>(),
            MemberIds = msg["memberIds"]!.Values<int>().ToArray(),
            Phase = msg["phase"]!.Value<string>() == "playing" ? GamePhase.Playing : GamePhase.Lobby,
        };

        private static GameStartPayload ParseGameStarted(JObject msg)
        {
            Dictionary<int, PlayerState> players = new();
            foreach (JProperty prop in ((JObject)msg["players"]).Properties())
            {
                int id = int.Parse(prop.Name);
                JObject p = (JObject)prop.Value;
                players[id] = new PlayerState(id, new Vector3(p["x"]!.Value<float>(), 0f, p["z"]!.Value<float>()))
                {
                    Angle = p["angle"]?.Value<float>() ?? 0f,
                    Stunned = p["stunned"]?.Value<bool>() ?? false,
                    Deliveries = p["deliveries"]?.Value<int>() ?? 0,
                };
            }

            Dictionary<int, PackageState> packages = new();
            foreach (JProperty prop in ((JObject)msg["packages"]).Properties())
            {
                int id = int.Parse(prop.Name);
                JObject b = (JObject)prop.Value;
                PackageKind kind = PackageKindWire.FromWireString(b["boxType"]!.Value<string>());
                packages[id] = new PackageState(id, kind, new Vector3(b["x"]!.Value<float>(), b["y"]!.Value<float>(), b["z"]!.Value<float>()))
                {
                    Velocity = new Vector3(b["vx"]!.Value<float>(), b["vy"]!.Value<float>(), b["vz"]!.Value<float>()),
                    Rotation = new Vector3(b["rx"]?.Value<float>() ?? 0f, b["ry"]?.Value<float>() ?? 0f, b["rz"]?.Value<float>() ?? 0f),
                    RenderRotation = new Vector3(b["rx"]?.Value<float>() ?? 0f, b["ry"]?.Value<float>() ?? 0f, b["rz"]?.Value<float>() ?? 0f),
                    TargetRotation = new Vector3(b["rx"]?.Value<float>() ?? 0f, b["ry"]?.Value<float>() ?? 0f, b["rz"]?.Value<float>() ?? 0f),
                    AngularVelocity = new Vector3(b["avx"]?.Value<float>() ?? 0f, b["avy"]?.Value<float>() ?? 0f, b["avz"]?.Value<float>() ?? 0f),
                    HeldBy = b["heldBy"]!.Type == JTokenType.Null ? null : b["heldBy"]!.Value<int?>(),
                    Timer = b["timer"]?.Value<float>() ?? 0f,
                    Delivered = b["delivered"]?.Value<bool>() ?? false,
                };
            }

            JObject start = (JObject)msg["start"];
            JObject goal = (JObject)msg["goal"];
            double remainMs = msg["sessionEndAt"]!.Value<double>() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return new GameStartPayload
            {
                Start = new Vector3(start["x"]!.Value<float>(), 0f, start["z"]!.Value<float>()),
                Goal = new Vector3(goal["x"]!.Value<float>(), 0f, goal["z"]!.Value<float>()),
                Obstacles = ParseObstacles((JArray)msg["obstacles"]),
                Players = players,
                Packages = packages,
                SessionEndTime = Time.time + (float)(remainMs / 1000.0),
            };
        }

        private static ObstacleData[] ParseObstacles(JArray array)
        {
            ObstacleData[] result = new ObstacleData[array.Count];
            for (int i = 0; i < array.Count; i++)
            {
                JObject o = (JObject)array[i];
                ObstacleKind kind = o["type"]!.Value<string>() switch
                {
                    "wall" => ObstacleKind.Wall,
                    "rock" => ObstacleKind.Rock,
                    _ => ObstacleKind.Pillar,
                };
                result[i] = new ObstacleData(new Vector3(o["x"]!.Value<float>(), 0f, o["z"]!.Value<float>()), o["radius"]!.Value<float>(), kind);
            }

            return result;
        }

        private static List<ScoreEntry> ParseResults(JObject msg)
        {
            List<ScoreEntry> results = new();
            foreach (JToken token in (JArray)msg["results"])
            {
                JObject r = (JObject)token;
                results.Add(new ScoreEntry(r["id"]!.Value<int>(), r["deliveries"]!.Value<int>()));
            }

            return results;
        }
    }
}
