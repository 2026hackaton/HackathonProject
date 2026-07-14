using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Hackathon.WebPort
{
    public interface IGameTransport : IDisposable
    {
        event Action<int> Connected;
        event Action<RoomStatePayload> RoomStateChanged;
        event Action<GameStartPayload> GameStarted;
        event Action<IReadOnlyList<ScoreEntry>> GameEnded;

        // Gameplay messages (move/pickup/boxUpdate/grab/push/hit/explode/spawn/goalChanged/
        // truckDeparted/tick/leave/pickupRejected) that the server relays during a session,
        // raised on the main thread only after Pump() is called.
        event Action<JObject> GameMessageReceived;

        int SelfId { get; }
        bool IsConnected { get; }

        void Connect();
        void CreateRoom();
        void JoinRoom(string code);
        void StartGame();
        void Send(GameClientCommand command);

        // Drains any messages received off the main thread and raises the corresponding
        // events. Must be called every frame regardless of game phase.
        void Pump();
    }
}
