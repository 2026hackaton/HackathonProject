using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    public sealed class LocalGameTransport : IGameTransport
    {
        private readonly List<int> _members = new();
        private readonly Dictionary<int, PlayerState> _players = new();
        private readonly Dictionary<int, PackageState> _packages = new();

        private string _roomCode = "LOCAL";
        private int _hostId;
        private GamePhase _phase = GamePhase.Menu;

        public event Action<int> Connected;
        public event Action<RoomStatePayload> RoomStateChanged;
        public event Action<GameStartPayload> GameStarted;
        public event Action<IReadOnlyList<ScoreEntry>> GameEnded;
#pragma warning disable CS0067
        public event Action<Newtonsoft.Json.Linq.JObject> GameMessageReceived;
#pragma warning restore CS0067

        public int SelfId { get; private set; }
        public bool IsConnected { get; private set; }

        public void Connect()
        {
            if (IsConnected)
                return;

            SelfId = 0;
            _hostId = SelfId;
            IsConnected = true;
            Connected?.Invoke(SelfId);
        }

        public void CreateRoom()
        {
            EnsureConnected();
            _roomCode = GenerateLocalCode();
            _phase = GamePhase.Lobby;
            _members.Clear();
            _members.Add(SelfId);
            _hostId = SelfId;
            RaiseRoomState();
        }

        public void JoinRoom(string code)
        {
            EnsureConnected();
            _roomCode = string.IsNullOrWhiteSpace(code) ? "LOCAL" : code.Trim().ToUpperInvariant();
            _phase = GamePhase.Lobby;
            _members.Clear();
            _members.Add(SelfId);
            _hostId = SelfId;
            RaiseRoomState();
        }

        public void StartGame()
        {
            EnsureConnected();
            if (_phase != GamePhase.Lobby)
                return;

            _phase = GamePhase.Playing;
            _players.Clear();
            _packages.Clear();

            Vector3 spawn = WebPortConstants.Start + new Vector3(UnityEngine.Random.Range(-30f, 30f), 0f, UnityEngine.Random.Range(-30f, 30f));
            _players[SelfId] = new PlayerState(SelfId, spawn);

            GameStarted?.Invoke(new GameStartPayload
            {
                Start = WebPortConstants.Start,
                Goal = WebPortConstants.GoalPositions[0],
                Obstacles = WebPortConstants.Obstacles,
                Players = _players,
                Packages = _packages,
                SessionEndTime = Time.time + WebPortConstants.SessionDurationSeconds,
            });
        }

        public void EndGame(IReadOnlyList<ScoreEntry> results)
        {
            _phase = GamePhase.Results;
            GameEnded?.Invoke(results);
        }

        public void Send(GameClientCommand command)
        {
            // No-op in the local adapter: there are no other clients to relay gameplay commands to.
        }

        public void Pump()
        {
            // No-op: the local adapter raises its events synchronously, nothing to drain.
        }

        public void Dispose()
        {
            _members.Clear();
            _players.Clear();
            _packages.Clear();
            IsConnected = false;
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
                Connect();
        }

        private void RaiseRoomState()
        {
            RoomStateChanged?.Invoke(new RoomStatePayload
            {
                Code = _roomCode,
                HostId = _hostId,
                MemberIds = _members.ToArray(),
                Phase = _phase,
            });
        }

        private static string GenerateLocalCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            Span<char> buffer = stackalloc char[4];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
            return new string(buffer);
        }
    }
}
