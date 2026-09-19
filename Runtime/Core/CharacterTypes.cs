using UnityEngine;

namespace Patterns.Character
{
    /// <summary>Actions a character can be asked to perform, independent of the device that produced them.</summary>
    public enum CharacterAction
    {
        None = 0,
        Jump = 1,
        Sprint = 2,
        Crouch = 3,
        Dash = 4,
        Interact = 5,
        Fly = 6,
        PrimaryAction = 7,
        SecondaryAction = 8,
        ToggleCamera = 9,
        Glide = 10
    }

    /// <summary>How the desired movement direction is converted into world space.</summary>
    public enum MovementSpace
    {
        /// <summary>Movement input is already in world space (+Z = forward, +X = right).</summary>
        World = 0,
        /// <summary>Direction is relative to the yaw of the camera. Classic third person.</summary>
        CameraYaw = 1,
        /// <summary>Direction is relative to the full camera basis (pitch included). Used by top-down / isometric rigs.</summary>
        CameraBasis = 2,
        /// <summary>Direction is relative to the character's own yaw. Tank controls / strafing first person.</summary>
        CharacterYaw = 3,
        /// <summary>Movement is flattened onto the world XY plane (2D / 2.5D side scrollers).</summary>
        WorldXY = 4
    }

    /// <summary>How the character body is rotated while moving.</summary>
    public enum OrientationMode
    {
        /// <summary>Body follows the movement direction.</summary>
        FaceMovement = 0,
        /// <summary>Body follows the camera yaw (strafe / shooter style).</summary>
        FaceCamera = 1,
        /// <summary>Body follows the raw input direction, even before acceleration ramps up.</summary>
        FaceInput = 2,
        /// <summary>Body is never rotated by the plugin.</summary>
        Manual = 3
    }

    /// <summary>Resource buckets every character carries by default. Custom resources can be added by name.</summary>
    public enum ResourceType
    {
        Health = 0,
        Stamina = 1,
        Energy = 2
    }

    /// <summary>Why the motor stopped moving the character this frame.</summary>
    public enum MovementBlockReason
    {
        None = 0,
        Locked = 1,
        NoRequest = 2,
        Dead = 3,
        Custom = 4
    }

    /// <summary>Snapshot of the surface the character is standing on.</summary>
    public struct GroundInfo
    {
        public bool IsGrounded;
        public Vector3 Normal;
        public Vector3 Point;
        public float Distance;
        public float SlopeAngle;
        public Collider Collider;
        public MovingPlatform Platform;

        public static readonly GroundInfo None = new GroundInfo
        {
            IsGrounded = false,
            Normal = Vector3.up,
            Point = Vector3.zero,
            Distance = float.PositiveInfinity,
            SlopeAngle = 0f,
            Collider = null,
            Platform = null
        };
    }

    /// <summary>Payload raised when the character touches the ground after being airborne.</summary>
    public struct LandingInfo
    {
        public Vector3 Position;
        public float ImpactSpeed;
        public float AirTime;
        public GroundInfo Ground;
        public bool HardLanding;
    }

    /// <summary>Type of virtual volume (water, ladder, wind, ...) a character can enter.</summary>
    public enum VolumeType
    {
        Water = 0,
        Ladder = 1,
        Wind = 2,
        Lava = 3,
        Custom = 4
    }
}