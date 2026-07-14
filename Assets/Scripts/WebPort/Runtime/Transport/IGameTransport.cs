using System;
using System.Collections.Generic;

namespace Hackathon.WebPort
{
    public interface IGameTransport : IDisposable
    {
        event Action<int> Connected;
        event Action<RoomStatePayload> RoomStateChanged;
        event Action<GameStartPayload> GameStarted;
        event Action<IReadOnlyList<ScoreEntry>> GameEnded;

        int SelfId { get; }
        bool IsConnected { get; }

        void Connect();
        void CreateRoom();
        void JoinRoom(string code);
        void StartGame();
        void Send(GameClientCommand command);
    }
}
