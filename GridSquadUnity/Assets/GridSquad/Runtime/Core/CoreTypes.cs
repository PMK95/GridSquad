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
        OutsideViewAngle,
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
    public struct ShotEvaluation
    {
        public bool CanShoot;
        public bool UsesPeekPosition;
        public int VisibleSampleCount;
        public float CoverEvasionPercent;
        public float HitChancePercent;
        public Vector3 ShotOrigin;
        public Vector3 TargetCenter;
        public ShotFailureReason FailureReason;

        public static ShotEvaluation CannotShoot(ShotFailureReason reason, Vector3 origin, Vector3 targetCenter)
        {
            return new ShotEvaluation
            {
                CanShoot = false,
                UsesPeekPosition = false,
                VisibleSampleCount = 0,
                CoverEvasionPercent = 0f,
                HitChancePercent = 0f,
                ShotOrigin = origin,
                TargetCenter = targetCenter,
                FailureReason = reason
            };
        }
    }
}
