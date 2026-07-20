using System;
using UnityEngine;

namespace GridSquad
{
    public enum Team
    {
        Ally,
        Enemy
    }

    [Serializable]
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public readonly int X;
        public readonly int Z;

        public GridCoordinate(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int ManhattanDistance(GridCoordinate other)
            => Mathf.Abs(X - other.X) + Mathf.Abs(Z - other.Z);

        public bool Equals(GridCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GridCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z);
        public override string ToString() => $"({X}, {Z})";

        public static bool operator ==(GridCoordinate left, GridCoordinate right) => left.Equals(right);
        public static bool operator !=(GridCoordinate left, GridCoordinate right) => !left.Equals(right);
        public static GridCoordinate operator +(GridCoordinate left, GridCoordinate right)
            => new(left.X + right.X, left.Z + right.Z);
    }

    public enum ShotFailureReason
    {
        None,
        NoTarget,
        TargetDead,
        OutOfRange,
        FullyBlocked,
        NoPeekPosition
    }

    public enum FireCycleState
    {
        WaitingForAim,
        Aiming,
        Cooldown
    }

    [Serializable]
    public readonly struct CoverEvaluation
    {
        public readonly float AngleDegrees;
        public readonly float EvasionPercent;

        public bool HasCover => EvasionPercent > 0.01f;

        public CoverEvaluation(float angleDegrees, float evasionPercent)
        {
            AngleDegrees = angleDegrees;
            EvasionPercent = evasionPercent;
        }

        public static CoverEvaluation None => new(-1f, 0f);
    }

    [Serializable]
    public struct ShotEvaluation
    {
        public bool CanShoot;
        public bool UsesPeekPosition;
        public GridCoordinate ShotOriginCell;
        public GridCoordinate TargetExposureCell;
        public float CoverAngleDegrees;
        public float CoverEvasionPercent;
        public float HitChancePercent;
        public Vector3 ShotOrigin;
        public Vector3 TargetCenter;
        public ShotFailureReason FailureReason;

        public static ShotEvaluation CannotShoot(
            ShotFailureReason reason,
            GridCoordinate originCell,
            GridCoordinate targetExposureCell,
            Vector3 origin,
            Vector3 targetCenter)
        {
            return new ShotEvaluation
            {
                CanShoot = false,
                UsesPeekPosition = false,
                ShotOriginCell = originCell,
                TargetExposureCell = targetExposureCell,
                CoverAngleDegrees = -1f,
                CoverEvasionPercent = 0f,
                HitChancePercent = 0f,
                ShotOrigin = origin,
                TargetCenter = targetCenter,
                FailureReason = reason
            };
        }
    }
}
