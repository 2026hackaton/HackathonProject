using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hackathon.WebPort
{
    public enum GamePhase
    {
        Menu,
        Lobby,
        Playing,
        Results,
    }

    public enum PackageKind
    {
        Normal,
        High,
        Bomb,
        Gravity,
    }

    public enum ObstacleKind
    {
        Pillar,
        Wall,
        Rock,
    }

    public enum EffectKind
    {
        Explosion,
        GrabFlash,
        Impact,
        Swing,
        Sweep,
        Shockwave,
        Deliver,
    }

    [Serializable]
    public readonly struct ObstacleData
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly ObstacleKind Kind;

        public ObstacleData(Vector3 position, float radius, ObstacleKind kind)
        {
            Position = position;
            Radius = radius;
            Kind = kind;
        }
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int Id;
        public Vector3 Position;
        public Vector3 RenderPosition;
        public Vector3 TargetPosition;
        public Vector3 Velocity;
        public float Angle;
        public bool Stunned;
        public bool Rolling;
        public int Deliveries;

        public float StunTimer;
        public float RollTimer;
        public Vector3 ExternalVelocity;
        public int? GrabbedBy;
        public float GrabTimer;
        public int? DraggingId;
        public float DragTimer;
        public float GrabCooldown;
        public float PushCooldown;
        public bool PushCharging;
        public float PushChargeStartedAt;
        public bool ChargingThrow;
        public float ThrowChargeStartedAt;
        public float Instability;
        public bool CarefulWalk;
        public float SpeedMultiplier;

        public PlayerState(int id, Vector3 position)
        {
            Id = id;
            Position = position;
            RenderPosition = position;
            TargetPosition = position;
            SpeedMultiplier = 1f;
        }
    }

    [Serializable]
    public sealed class PackageState
    {
        public int Id;
        public PackageKind Kind;
        public Vector3 Position;
        public Vector3 RenderPosition;
        public Vector3 TargetPosition;
        public Vector3 Velocity;
        public int? HeldBy;
        public int? OwnerId;
        public float Timer;
        public bool Delivered;

        public PackageState(int id, PackageKind kind, Vector3 position)
        {
            Id = id;
            Kind = kind;
            Position = position;
            RenderPosition = position;
            TargetPosition = position;
            Timer = kind == PackageKind.Bomb ? 4f : 0f;
        }
    }

    public sealed class PackageSlot
    {
        public Vector3 Position;
        public int? PackageId;
        public float RefillAt;
    }

    public sealed class EffectEvent
    {
        public EffectKind Kind;
        public Vector3 Position;
        public float Angle;
        public float Radius;
        public float Length;
        public float StartedAt;
        public float Duration;
    }

    public readonly struct ScoreEntry
    {
        public readonly int PlayerId;
        public readonly int Deliveries;

        public ScoreEntry(int playerId, int deliveries)
        {
            PlayerId = playerId;
            Deliveries = deliveries;
        }
    }

    public sealed class RoomStatePayload
    {
        public string Code;
        public int HostId;
        public IReadOnlyList<int> MemberIds;
        public GamePhase Phase;
    }

    public sealed class GameStartPayload
    {
        public Vector3 Start;
        public Vector3 Goal;
        public IReadOnlyList<ObstacleData> Obstacles;
        public IReadOnlyDictionary<int, PlayerState> Players;
        public IReadOnlyDictionary<int, PackageState> Packages;
        public float SessionEndTime;
    }

    public abstract class GameClientCommand
    {
        public int SenderId;
    }
}
