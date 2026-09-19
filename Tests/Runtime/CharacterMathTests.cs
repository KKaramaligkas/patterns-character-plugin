using NUnit.Framework;
using UnityEngine;

namespace Patterns.Character.Tests
{
    public class CharacterMathTests
    {
        const float Tolerance = 0.001f;

        [Test]
        public void MoveTowards_ReachesTargetExactly()
        {
            // Limited by the maximum delta.
            Assert.AreEqual(1f, CharacterMath.MoveTowards(0f, 5f, 1f), Tolerance);
            // Clamped to the target, never overshooting.
            Assert.AreEqual(5f, CharacterMath.MoveTowards(2f, 5f, 10f), Tolerance);
            // Moves backwards as well.
            Assert.AreEqual(4f, CharacterMath.MoveTowards(5f, 0f, 1f), Tolerance);
            Assert.AreEqual(0f, CharacterMath.MoveTowards(5f, 0f, 100f), Tolerance);
        }

        [Test]
        public void ApproachVelocity_AcceleratesThenStops()
        {
            Vector3 velocity = new Vector3(0f, 0f, 0f);
            Vector3 target = new Vector3(5f, 0f, 0f);

            velocity = CharacterMath.ApproachVelocity(velocity, target, 10f, 20f, 0.1f);
            Assert.AreEqual(1f, velocity.x, Tolerance, "Should accelerate at 10 units per second squared");

            velocity = CharacterMath.ApproachVelocity(velocity, Vector3.zero, 10f, 20f, 0.1f);
            Assert.AreEqual(0f, velocity.x, Tolerance, "Should decelerate at 20 units per second squared");
        }

        [Test]
        public void SmoothFactor_IsFrameRateIndependent()
        {
            float oneBigStep = CharacterMath.SmoothFactor(10f, 1f);
            float twoHalfSteps = 1f - (1f - CharacterMath.SmoothFactor(10f, 0.5f)) * (1f - CharacterMath.SmoothFactor(10f, 0.5f));

            Assert.AreEqual(oneBigStep, twoHalfSteps, 0.001f);
            Assert.Greater(oneBigStep, 0.99f, "Sharpness of 10 over a second should be almost complete");
            Assert.AreEqual(0f, CharacterMath.SmoothFactor(0f, 1f), Tolerance);
        }

        [Test]
        public void InputToWorld_CameraYawMapsForwardToCameraForward()
        {
            var character = new GameObject("Character").transform;
            var camera = new GameObject("Camera").transform;

            // Camera looking 90 degrees to the right means "up on the stick" is +X in world space.
            camera.rotation = Quaternion.Euler(0f, 90f, 0f);

            bool hasInput;
            Vector3 direction = CharacterMath.InputToWorld(new Vector2(0f, 1f), MovementSpace.CameraYaw, character, camera, out hasInput);

            Assert.IsTrue(hasInput);
            Assert.AreEqual(1f, direction.x, 0.01f);
            Assert.AreEqual(0f, direction.z, 0.01f);

            Object.DestroyImmediate(character.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }

        [Test]
        public void InputToWorld_WorldSpaceIgnoresTransforms()
        {
            var character = new GameObject("Character").transform;
            character.rotation = Quaternion.Euler(0f, 45f, 0f);

            bool hasInput;
            Vector3 direction = CharacterMath.InputToWorld(new Vector2(1f, 0f), MovementSpace.World, character, null, out hasInput);

            Assert.IsTrue(hasInput);
            Assert.AreEqual(1f, direction.x, 0.01f);
            Assert.AreEqual(0f, direction.z, 0.01f);

            Object.DestroyImmediate(character.gameObject);
        }

        [Test]
        public void InputToWorld_WorldXYKeepsInputPlane()
        {
            bool hasInput;
            Vector3 direction = CharacterMath.InputToWorld(new Vector2(0.5f, -0.5f), MovementSpace.WorldXY, null, null, out hasInput);

            Assert.IsTrue(hasInput);
            Assert.AreEqual(0.5f, direction.x, 0.01f);
            Assert.AreEqual(-0.5f, direction.y, 0.01f);
            Assert.AreEqual(0f, direction.z, 0.01f);

            // Input longer than one unit is normalised so diagonal movement is not faster.
            Vector3 clamped = CharacterMath.InputToWorld(new Vector2(1f, 1f), MovementSpace.WorldXY, null, null, out hasInput);
            Assert.AreEqual(1f, clamped.magnitude, 0.01f);
        }

        [Test]
        public void ClampPitch_WrapsAndClamps()
        {
            Assert.AreEqual(-45f, CharacterMath.ClampPitch(-45f), Tolerance);

            // Values above 180 are wrapped first (300 becomes -60).
            Assert.AreEqual(-60f, CharacterMath.ClampPitch(300f), Tolerance);

            // Everything outside the +-90 range is clamped.
            Assert.AreEqual(89.9f, CharacterMath.ClampPitch(120f), 0.2f);
            Assert.AreEqual(-89.9f, CharacterMath.ClampPitch(-120f), 0.2f);
        }

        [Test]
        public void ProjectOnPlaneSafe_HandlesZeroNormal()
        {
            Vector3 result = CharacterMath.ProjectOnPlaneSafe(new Vector3(1f, -1f, 0f), Vector3.zero);
            Assert.AreEqual(0f, result.y, 0.01f);
        }
    }

    public class CharacterProfileTests
    {
        [Test]
        public void JumpVelocityFor_MatchesPhysics()
        {
            CharacterProfile profile = ScriptableObject.CreateInstance<CharacterProfile>();
            profile.gravity = -20f;
            profile.jumpHeight = 2f;

            float velocity = profile.JumpVelocityFor(2f);
            Assert.AreEqual(Mathf.Sqrt(2f * 20f * 2f), velocity, 0.01f);

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void SpeedFor_ReturnsTierSpeeds()
        {
            CharacterProfile profile = ScriptableObject.CreateInstance<CharacterProfile>();
            profile.walkSpeed = 2f;
            profile.runSpeed = 5f;
            profile.sprintSpeed = 8f;
            profile.crouchSpeed = 1f;
            profile.swimSpeed = 3f;

            Assert.AreEqual(2f, profile.SpeedFor(LocomotionTier.Walk), 0.001f);
            Assert.AreEqual(5f, profile.SpeedFor(LocomotionTier.Run), 0.001f);
            Assert.AreEqual(8f, profile.SpeedFor(LocomotionTier.Sprint), 0.001f);
            Assert.AreEqual(1f, profile.SpeedFor(LocomotionTier.Crouch), 0.001f);
            Assert.AreEqual(3f, profile.SpeedFor(LocomotionTier.Swim), 0.001f);

            Object.DestroyImmediate(profile);
        }

        [Test]
        public void Default_IsSharedAndNotWrittenToDisk()
        {
            CharacterProfile first = CharacterProfile.Default;
            CharacterProfile second = CharacterProfile.Default;

            Assert.AreSame(first, second);
            Assert.IsTrue((first.hideFlags & HideFlags.HideAndDontSave) != 0);
        }
    }

    public class CharacterInputFrameTests
    {
        [Test]
        public void ConsumePressed_OnlyAllowsOneConsumer()
        {
            CharacterInputFrame frame = new CharacterInputFrame();
            frame.jumpPressed = true;

            Assert.IsTrue(frame.ConsumePressed(CharacterAction.Jump));
            Assert.IsFalse(frame.ConsumePressed(CharacterAction.Jump), "The second consumer must be refused");
            Assert.IsTrue(frame.IsConsumed(CharacterAction.Jump));
        }

        [Test]
        public void ReleaseClaim_LetsAnotherAbilityUseTheAction()
        {
            CharacterInputFrame frame = new CharacterInputFrame();
            frame.dashPressed = true;

            Assert.IsTrue(frame.ConsumePressed(CharacterAction.Dash));
            frame.ReleaseClaim(CharacterAction.Dash);
            Assert.IsTrue(frame.ConsumePressed(CharacterAction.Dash));
        }

        [Test]
        public void Clear_ResetsEverything()
        {
            CharacterInputFrame frame = new CharacterInputFrame();
            frame.move = new Vector2(1f, 1f);
            frame.jumpPressed = true;
            frame.cancelPressed = true;
            frame.lookFromStick = true;
            frame.ConsumePressed(CharacterAction.Jump);

            frame.Clear();

            Assert.AreEqual(Vector2.zero, frame.move);
            Assert.IsFalse(frame.jumpPressed);
            Assert.IsFalse(frame.cancelPressed);
            Assert.IsFalse(frame.lookFromStick);
            Assert.IsFalse(frame.IsConsumed(CharacterAction.Jump));
            Assert.IsFalse(frame.HasMovementInput);
        }

        [Test]
        public void HasMovementInput_UsesThreshold()
        {
            CharacterInputFrame frame = new CharacterInputFrame();
            frame.moveMagnitude = 0.005f;
            Assert.IsFalse(frame.HasMovementInput);

            frame.moveMagnitude = 0.5f;
            Assert.IsTrue(frame.HasMovementInput);
        }
    }

    public class CharacterResourceTests
    {
        [Test]
        public void Normalized_ClampsBetweenZeroAndOne()
        {
            CharacterResource resource = new CharacterResource { max = 100f, current = 250f };
            Assert.AreEqual(1f, resource.Normalized, 0.001f);

            resource.current = -5f;
            Assert.AreEqual(0f, resource.Normalized, 0.001f);

            resource.current = 50f;
            Assert.AreEqual(0.5f, resource.Normalized, 0.001f);
        }

        [Test]
        public void EmptyAndFull_DetectEdges()
        {
            CharacterResource resource = new CharacterResource { max = 100f, current = 0f };
            Assert.IsTrue(resource.IsEmpty);

            resource.Reset();
            Assert.IsTrue(resource.IsFull);
        }

        [Test]
        public void Clone_CopiesValuesWithoutSharingState()
        {
            CharacterResource resource = new CharacterResource { type = ResourceType.Energy, max = 50f, current = 25f, regenPerSecond = 5f };
            CharacterResource copy = resource.Clone();

            copy.current = 10f;
            Assert.AreEqual(25f, resource.current, 0.001f);
            Assert.AreEqual(ResourceType.Energy, copy.type);
            Assert.AreEqual(5f, copy.regenPerSecond, 0.001f);
        }
    }

    public class CharacterTypesTests
    {
        [Test]
        public void GroundInfo_NoneIsNotGrounded()
        {
            Assert.IsFalse(GroundInfo.None.IsGrounded);
            Assert.AreEqual(Vector3.up, GroundInfo.None.Normal);
        }

        [Test]
        public void MotorRequestDefaults_AreSafe()
        {
            MotorRequest request = default(MotorRequest);
            Assert.IsNull(request.Source);
            Assert.AreEqual(0, request.Priority);
            Assert.IsFalse(request.DrivesVertical);
            Assert.IsFalse(request.SkipGroundSnap);
        }

        [Test]
        public void AbilityDefaults_OrderMovementLayersCorrectly()
        {
            Assert.Less(CharacterAbility.DefaultExecutionOrderFor(typeof(LocomotionAbility)),
                CharacterAbility.DefaultExecutionOrderFor(typeof(JumpAbility)));

            Assert.Less(CharacterAbility.DefaultExecutionOrderFor(typeof(JumpAbility)),
                CharacterAbility.DefaultExecutionOrderFor(typeof(DashAbility)));

            Assert.Greater(CharacterAbility.DefaultMotorPriorityFor(typeof(DashAbility)),
                CharacterAbility.DefaultMotorPriorityFor(typeof(LocomotionAbility)));
        }
    }
}