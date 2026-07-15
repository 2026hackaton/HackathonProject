using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
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
        public float TargetTime;
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
        public float TargetTime;
        public Vector3 Velocity;
        public Vector3 Rotation;
        public Vector3 RenderRotation;
        public Vector3 TargetRotation;
        public Vector3 AngularVelocity;
        public int? HeldBy;
        public int? OwnerId;
        public float Timer;
        public bool Delivered;
        public bool PickupLocked;
        public bool Armed; // 폭탄: 누군가 실제로 던져야 true가 되고, 그 뒤 착지하는 순간 터진다.

        public PackageState(int id, PackageKind kind, Vector3 position)
        {
            Id = id;
            Kind = kind;
            Position = position;
            RenderPosition = position;
            TargetPosition = position;
            Rotation = Vector3.zero;
            RenderRotation = Vector3.zero;
            TargetRotation = Vector3.zero;
            AngularVelocity = Vector3.zero;
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

        public abstract JObject ToJson();
    }

    public sealed class MoveCommand : GameClientCommand
    {
        public float X;
        public float Z;
        public float Angle;
        public bool Stunned;
        public bool Rolling;
        public int Deliveries;

        public override JObject ToJson() => new()
        {
            ["type"] = "move",
            ["x"] = X,
            ["z"] = Z,
            ["angle"] = Angle,
            ["stunned"] = Stunned,
            ["rolling"] = Rolling,
            ["deliveries"] = Deliveries,
        };
    }

    public sealed class PickupCommand : GameClientCommand
    {
        public int Id;

        public override JObject ToJson() => new() { ["type"] = "pickup", ["id"] = Id };
    }

    public sealed class BoxUpdateCommand : GameClientCommand
    {
        public int Id;
        public string BoxType;
        public float X;
        public float Y;
        public float Z;
        public float Vx;
        public float Vy;
        public float Vz;
        public float Rx;
        public float Ry;
        public float Rz;
        public float Avx;
        public float Avy;
        public float Avz;
        public int? HeldBy;
        public float Timer;
        public bool Delivered;

        public override JObject ToJson() => new()
        {
            ["type"] = "boxUpdate",
            ["id"] = Id,
            ["boxType"] = BoxType,
            ["x"] = X,
            ["y"] = Y,
            ["z"] = Z,
            ["vx"] = Vx,
            ["vy"] = Vy,
            ["vz"] = Vz,
            ["rx"] = Rx,
            ["ry"] = Ry,
            ["rz"] = Rz,
            ["avx"] = Avx,
            ["avy"] = Avy,
            ["avz"] = Avz,
            ["heldBy"] = HeldBy.HasValue ? (JToken)HeldBy.Value : JValue.CreateNull(),
            ["timer"] = Timer,
            ["delivered"] = Delivered,
        };
    }

    public sealed class GrabCommand : GameClientCommand
    {
        public int TargetId;

        public override JObject ToJson() => new() { ["type"] = "grab", ["targetId"] = TargetId };
    }

    public sealed class PushCommand : GameClientCommand
    {
        public int TargetId;
        public float DirX;
        public float DirZ;
        public float Scale;

        public override JObject ToJson() => new()
        {
            ["type"] = "push",
            ["targetId"] = TargetId,
            ["dirX"] = DirX,
            ["dirZ"] = DirZ,
            ["scale"] = Scale,
        };
    }

    public sealed class HitCommand : GameClientCommand
    {
        public int TargetId;
        public float X;
        public float Z;

        public override JObject ToJson() => new() { ["type"] = "hit", ["targetId"] = TargetId, ["x"] = X, ["z"] = Z };
    }

    public sealed class ExplodeCommand : GameClientCommand
    {
        public int Id;
        public float X;
        public float Z;

        public override JObject ToJson() => new() { ["type"] = "explode", ["id"] = Id, ["x"] = X, ["z"] = Z };
    }

    public static class PackageKindWire
    {
        public static string ToWireString(PackageKind kind) => kind switch
        {
            PackageKind.High => "high",
            PackageKind.Bomb => "bomb",
            PackageKind.Gravity => "gravity",
            _ => "normal",
        };

        public static PackageKind FromWireString(string value) => value switch
        {
            "high" => PackageKind.High,
            "bomb" => PackageKind.Bomb,
            "gravity" => PackageKind.Gravity,
            _ => PackageKind.Normal,
        };
    }
}
