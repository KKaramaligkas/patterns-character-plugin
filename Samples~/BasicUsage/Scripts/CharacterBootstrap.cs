using Patterns.Character;
using UnityEngine;

namespace Patterns.Character.Samples
{
    /// <summary>
    /// Builds a complete character from code. Drop this on an empty GameObject in an empty scene and press
    /// Play: a player with locomotion, sprint, crouch, jump and dash is created together with a camera rig.
    /// The same setup is available from the editor through GameObject ▸ Patterns Character.
    /// </summary>
    [AddComponentMenu("Patterns/Character/Samples/Character Bootstrap")]
    public class CharacterBootstrap : MonoBehaviour
    {
        [Header("Setup")]
        public bool buildOnStart = true;
        public Vector3 spawnPosition = new Vector3(0f, 0.2f, 0f);
        public bool thirdPerson = true;

        CharacterContext _context;

        void Start()
        {
            if (buildOnStart) Build();
        }

        /// <summary>Creates the player and returns its context.</summary>
        public CharacterContext Build()
        {
            GameObject player = new GameObject("Player (script built)");
            player.transform.position = spawnPosition;

            // ---- collision
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;

            // ---- core
            CharacterMotor motor = player.AddComponent<CharacterMotor>();
            CharacterResources resources = player.AddComponent<CharacterResources>();
            CharacterLook look = player.AddComponent<CharacterLook>();
            CharacterContext context = player.AddComponent<CharacterContext>();

            // ---- abilities, order matters for the inspector but the context sorts them by executionOrder
            LocomotionAbility locomotion = player.AddComponent<LocomotionAbility>();
            locomotion.movementSpace = thirdPerson ? MovementSpace.CameraYaw : MovementSpace.CharacterYaw;

            OrientationAbility orientation = player.AddComponent<OrientationAbility>();
            orientation.mode = thirdPerson ? OrientationMode.FaceMovement : OrientationMode.FaceCamera;
            orientation.executionOrder = 50;

            player.AddComponent<SprintAbility>().executionOrder = 100;

            CrouchAbility crouch = player.AddComponent<CrouchAbility>();
            crouch.executionOrder = 110;

            JumpAbility jump = player.AddComponent<JumpAbility>();
            jump.executionOrder = 200;

            DashAbility dash = player.AddComponent<DashAbility>();
            dash.executionOrder = 300;
            dash.motorPriority = 900;

            player.AddComponent<InteractionAbility>().executionOrder = 350;

            // ---- input, legacy on purpose so the sample runs anywhere
            player.AddComponent<LegacyInputSource>();

            // ---- camera
            GameObject cameraRoot = new GameObject("CharacterCamera");
            cameraRoot.transform.SetParent(player.transform, false);

            CharacterCameraRigSwitcher switcher = cameraRoot.AddComponent<CharacterCameraRigSwitcher>();

            GameObject rigObject = new GameObject(thirdPerson ? "ThirdPersonRig" : "FirstPersonRig");
            rigObject.transform.SetParent(cameraRoot.transform, false);

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(rigObject.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            CharacterCameraRig rig;
            if (thirdPerson)
            {
                ThirdPersonCameraRig third = rigObject.AddComponent<ThirdPersonCameraRig>();
                third.rigId = "Third Person";
                third.distance = 4.5f;
                third.targetOffset = new Vector3(0f, 1.4f, 0f);
                rig = third;
            }
            else
            {
                FirstPersonCameraRig first = rigObject.AddComponent<FirstPersonCameraRig>();
                first.rigId = "First Person";
                rig = first;
            }

            rig.cameraComponent = camera;
            switcher.AddRig(rig);

            // ---- wiring: the context finds the components it needs by itself, this is just explicit
            _context = context;

            // Add the camera target the rigs orbit around.
            GameObject target = new GameObject("CameraTarget");
            target.transform.SetParent(player.transform, false);
            target.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            AnimationDemo(look);
            return context;
        }

        void AnimationDemo(CharacterLook look)
        {
            look.SetLook(0f, 10f);
        }

        void Update()
        {
            // Example: press T to toggle third person / first person if both rigs exist.
            if (_context == null || _context.CameraRig == null) return;
            if (!Input.GetKeyDown(KeyCode.T)) return;

            CharacterCameraRigSwitcher switcher = _context.CameraRig;
            if (switcher.ActiveRig != null && switcher.ActiveRig.rigId == "Third Person")
            {
                switcher.SetRigById("First Person");
            }
            else
            {
                switcher.SetRigById("Third Person");
            }
        }
    }
}