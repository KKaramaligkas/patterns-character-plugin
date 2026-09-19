using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Patterns.Character.Tests
{
    /// <summary>Input source used by the play mode tests so abilities can be driven deterministically.</summary>
    public class ScriptedTestInput : CharacterInputSourceBehaviour
    {
        public Vector2 move;
        public bool jumpPressed;
        public bool sprintHeld;
        public bool crouchHeld;

        public override int Priority { get { return 1000; } }

        public override string DeviceName { get { return "Test Input"; } }

        public override void Poll(CharacterInputFrame frame, float deltaTime)
        {
            frame.move = move;
            frame.moveMagnitude = Mathf.Min(1f, move.magnitude);
            frame.jumpPressed |= jumpPressed;
            frame.jumpHeld |= jumpPressed;
            frame.sprintHeld |= sprintHeld;
            frame.crouchHeld |= crouchHeld;
            frame.deviceName = DeviceName;

            // Pulses last a single frame, exactly like a real press.
            jumpPressed = false;
            MarkActive();
        }
    }

    public class CharacterMotorPlayModeTests
    {
        GameObject _ground;
        GameObject _character;

        [TearDown]
        public void TearDown()
        {
            if (_character != null) Object.Destroy(_character);
            if (_ground != null) Object.Destroy(_ground);
        }

        GameObject CreateGround()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.position = Vector3.zero;
            _ground.transform.localScale = new Vector3(4f, 1f, 4f);
            return _ground;
        }

        CharacterContext CreateCharacter(Vector3 position, bool withAbilities = true)
        {
            _character = new GameObject("Test Character");
            _character.transform.position = position;

            CharacterController controller = _character.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;

            _character.AddComponent<CharacterMotor>();
            _character.AddComponent<CharacterResources>();
            _character.AddComponent<CharacterLook>();
            CharacterContext context = _character.AddComponent<CharacterContext>();
            _character.AddComponent<ScriptedTestInput>();

            if (withAbilities)
            {
                LocomotionAbility locomotion = _character.AddComponent<LocomotionAbility>();
                locomotion.movementSpace = MovementSpace.World;

                OrientationAbility orientation = _character.AddComponent<OrientationAbility>();
                orientation.executionOrder = 50;

                JumpAbility jump = _character.AddComponent<JumpAbility>();
                jump.executionOrder = 200;

                SprintAbility sprint = _character.AddComponent<SprintAbility>();
                sprint.executionOrder = 100;
                sprint.requiresForward = false;
            }

            return context;
        }

        [UnityTest]
        public IEnumerator Character_FallsAndLands()
        {
            CreateGround();
            CharacterContext context = CreateCharacter(new Vector3(0f, 3f, 0f));

            // Wait for the character to fall and settle.
            float timeout = 3f;
            while (timeout > 0f && !context.Motor.IsGrounded)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(context.Motor.IsGrounded, "The character should land on the ground plane");
            Assert.Less(context.transform.position.y, 0.2f, "The character should be standing on the plane");
            Assert.Greater(context.transform.position.y, -0.2f, "The character should not sink through the plane");
        }

        [UnityTest]
        public IEnumerator Character_WalksForward_WithInput()
        {
            CreateGround();
            CharacterContext context = CreateCharacter(new Vector3(0f, 0.1f, 0f));
            ScriptedTestInput input = _character.GetComponent<ScriptedTestInput>();

            input.move = new Vector2(0f, 1f);

            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(context.Motor.PlanarSpeed, 0.5f, "The character should be moving");
            Assert.Greater(context.transform.position.z, 0.5f, "The character should have travelled forward");

            LocomotionAbility locomotion = context.GetAbility<LocomotionAbility>();
            Assert.IsNotNull(locomotion);
            Assert.Greater(locomotion.CurrentSpeed, 0.5f);
        }

        [UnityTest]
        public IEnumerator Character_JumpsAndLands()
        {
            CreateGround();
            CharacterContext context = CreateCharacter(new Vector3(0f, 0.1f, 0f));
            ScriptedTestInput input = _character.GetComponent<ScriptedTestInput>();

            // Let the character settle on the ground first.
            float settle = 1f;
            while (settle > 0f)
            {
                settle -= Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(context.Motor.IsGrounded, "Should be grounded before jumping");

            float startHeight = context.transform.position.y;
            input.jumpPressed = true; // one frame pulse

            float peak = startHeight;
            float riseTime = 0.6f;
            while (riseTime > 0f)
            {
                riseTime -= Time.deltaTime;
                peak = Mathf.Max(peak, context.transform.position.y);
                yield return null;
            }

            Assert.Greater(peak, startHeight + 0.25f, "The character should rise after a jump");
            Assert.Greater(context.Motor.AirTime, 0f, "The character should have spent time airborne");

            float landing = 3f;
            while (landing > 0f && !context.Motor.IsGrounded)
            {
                landing -= Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(context.Motor.IsGrounded, "The character should land again");
        }

        [UnityTest]
        public IEnumerator Character_SprintRaisesSpeed()
        {
            CreateGround();
            CharacterContext context = CreateCharacter(new Vector3(0f, 0.1f, 0f));
            ScriptedTestInput input = _character.GetComponent<ScriptedTestInput>();

            input.move = new Vector2(0f, 1f);

            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float walkingSpeed = context.Motor.PlanarSpeed;

            input.sprintHeld = true;

            elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            float sprintSpeed = context.Motor.PlanarSpeed;

            SprintAbility sprint = context.GetAbility<SprintAbility>();
            Assert.IsNotNull(sprint);
            Assert.IsTrue(sprint.IsSprinting, "Sprint should be active while the key is held");
            Assert.Greater(sprintSpeed, walkingSpeed + 0.5f, "Sprinting should be faster than running");
        }

        [UnityTest]
        public IEnumerator Character_CrouchShrinksCapsule()
        {
            CreateGround();
            CharacterContext context = CreateCharacter(new Vector3(0f, 0.1f, 0f));
            ScriptedTestInput input = _character.GetComponent<ScriptedTestInput>();

            CrouchAbility crouch = _character.AddComponent<CrouchAbility>();
            crouch.executionOrder = 110;
            crouch.useProfileToggleSetting = false;
            crouch.toggle = false;

            float settle = 1f;
            while (settle > 0f)
            {
                settle -= Time.deltaTime;
                yield return null;
            }

            float standingHeight = context.Motor.Height;
            input.crouchHeld = true;

            float elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Less(context.Motor.Height, standingHeight - 0.2f, "Crouching should shrink the capsule");
            Assert.IsTrue(crouch.IsCrouching, "The crouch ability should report the crouched state");

            input.crouchHeld = false;

            elapsed = 0f;
            while (elapsed < 1.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(context.Motor.Height, standingHeight - 0.2f, "The character should stand up again");
        }

        [UnityTest]
        public IEnumerator Camera_Switcher_ChangesRig()
        {
            CreateGround();
            CharacterContext context = CreateCharacter(new Vector3(0f, 0.1f, 0f));

            GameObject cameraRoot = new GameObject("CameraRoot");
            cameraRoot.transform.SetParent(_character.transform, false);
            CharacterCameraRigSwitcher switcher = cameraRoot.AddComponent<CharacterCameraRigSwitcher>();

            GameObject rigObject = new GameObject("Rig");
            rigObject.transform.SetParent(cameraRoot.transform, false);
            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(rigObject.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();

            FirstPersonCameraRig rig = rigObject.AddComponent<FirstPersonCameraRig>();
            rig.rigId = "First Person";
            rig.cameraComponent = camera;
            switcher.AddRig(rig);

            float settle = 1f;
            while (settle > 0f)
            {
                settle -= Time.deltaTime;
                yield return null;
            }

            Assert.IsNotNull(switcher.ActiveRig, "The switcher should activate its rig");
            Assert.AreEqual("First Person", switcher.ActiveRig.rigId);
            Assert.IsNotNull(switcher.ActiveCameraTransform);

            // The camera should sit close to the character's camera target.
            float distance = Vector3.Distance(switcher.ActiveCameraTransform.position, context.CameraTargetPosition);
            Assert.Less(distance, 1.5f, "The first person camera should be at the character's head");

            Object.Destroy(cameraRoot);
        }
    }
}