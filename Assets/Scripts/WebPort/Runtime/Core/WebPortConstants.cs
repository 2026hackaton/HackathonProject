using UnityEngine;

namespace Hackathon.WebPort
{
    public static class WebPortConstants
    {
        public const float ArmLength = 560f;
        public const float ArmHalfWidth = 130f;

        public static readonly Vector3 Start = new(-ArmLength, 0f, 0f);
        public static readonly Vector3[] GoalPositions =
        {
            new(ArmLength, 0f, 0f),
            new(0f, 0f, -ArmLength),
            new(0f, 0f, ArmLength),
        };

        public const float GoalRadius = 55f;
        public const float GroundY = 0f;

        public const float PlayerRadius = 28f;
        public const float PlayerSpeed = 220f;
        public const float MaxChargeSeconds = 1.6f;
        public const float MaxPower = 520f;
        public const int MaxHold = 8;
        public const int StableHoldCount = 1;
        public const float InstabilityFillPerSecond = 16f;
        public const float InstabilityDrainPerSecond = 45f;
        public const float StumbleStunDuration = 0.6f;
        public const float BoxStackHeight = 26f;
        public const float CarryHeight = 34f;
        public const float HoldSpeedPenaltyPerBox = 0.08f;
        public const float HoldSpeedFloor = 0.4f;
        public const float CarefulWalkMultiplier = 0.45f;
        public const float CarefulInstabilityMultiplier = 0.15f;

        public const float PickupRange = 95f;
        public const float HitSpeed = 80f;
        public const float HitRadius = 45f;
        public const float StunDuration = 1.2f;
        public const float GrabRange = 95f;
        public const float GrabCone = 0.6f;
        public const float GrabCooldown = 1.2f;
        public const float GrabDuration = 1.5f;
        public const float GrabPullBase = 0.05f;
        public const float PushRange = 80f;
        public const float PushRangeMaxScale = 1.3f;
        public const float PushArcHalfAngle = 1.15f;
        public const float PushCooldown = 0.6f;
        public const float PushForce = 330f;
        public const float PushMaxChargeSeconds = 0.9f;
        public const float PushForceMinScale = 0.6f;
        public const float PushForceMaxScale = 1.4f;
        public const float PushChargeSpeedPenaltyMax = 0.75f;
        public const float PushChargeSpeedFloor = 0.2f;

        public const float BlastScale = 8f;
        public const float BlastRadius = 150f;
        public const float DirectHitRadius = 55f;
        public const float BombDirectScale = 1.9f;
        public const float BombStunDuration = 1f;
        public const float GravityRadius = 150f;
        public const float GravityPull = 40f;
        public const float ExtrapolateCapSeconds = 0.15f;
        public const float BoxHalf = 12f;
        public const float BoxPushSpeed = 85f;
        public const float FrictionRetain = 0.05f;
        public const float ThrowGravity = 1750f;
        public const float ThrowVyFactor = 0.18f;
        public const float PackageExternalImpulseScale = 0.65f;
        public const float PackageCollisionBounceRetain = 0.25f;
        public const float PackageSpinVelocityScale = 0.35f;
        public const float PackageSpinYawScale = 0.08f;
        public const float PackageImpactSpinScale = 0.2f;
        public const float PackageAirAngularRetain = 0.5f;
        public const float PackageGroundAngularRetain = 0.01f;
        public const float PackageGroundUprightLerp = 18f;
        public const float PackageGroundSnapAngle = 0.2f;
        public const float PackageGroundStopSpeed = 10f;
        public const float PackageMaxAngularSpeed = 220f;
        public const float PackageCarryLiftSpeed = 1400f;
        public const float PackageCarryLiftAcceleration = 16000f;

        public const int SlotColumns = 3;
        public const int SlotRows = 3;
        public const float SlotSpacing = 30f;
        public const float RefillDelaySeconds = 4f;
        public const int TruckThreshold = 20;
        public const float GoalRotateSeconds = 25f;
        public const float SessionDurationSeconds = 180f;

        public static readonly ObstacleData[] Obstacles =
        {
            new(new Vector3(250f, 0f, 0f), 48f, ObstacleKind.Pillar),
            new(new Vector3(0f, 0f, -250f), 55f, ObstacleKind.Wall),
            new(new Vector3(0f, 0f, 250f), 50f, ObstacleKind.Rock),
        };

        public static Vector3 ClampToCross(Vector3 position, float margin = 20f)
        {
            float length = ArmLength - margin;
            float halfWidth = ArmHalfWidth - margin;
            float x = Mathf.Clamp(position.x, -length, length);
            float z = Mathf.Clamp(position.z, -length, length);

            if (Mathf.Abs(z) <= halfWidth || Mathf.Abs(x) <= halfWidth)
                return new Vector3(x, position.y, z);

            if (Mathf.Abs(x) > Mathf.Abs(z))
                z = Mathf.Clamp(z, -halfWidth, halfWidth);
            else
                x = Mathf.Clamp(x, -halfWidth, halfWidth);

            return new Vector3(x, position.y, z);
        }
    }
}
