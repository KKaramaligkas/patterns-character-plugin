using System;
using System.Collections.Generic;
using Patterns.Character;
using UnityEditor;
using UnityEngine;

namespace Patterns.Character.Editor
{
    /// <summary>
    /// One click character setup. Builds a fully wired player (motor, abilities, camera modes, input)
    /// for first person, third person, top-down or 2.5D games, or upgrades an existing model.
    /// </summary>
    public static class CharacterRigWizard
    {
        const float DefaultHeight = 1.8f;
        const float DefaultRadius = 0.35f;

        [MenuItem("GameObject/Patterns Character/Create Player - First Person", false, 10)]
        public static void CreateFirstPerson()
        {
            Create(CharacterPresetId.FirstPerson);
        }

        [MenuItem("GameObject/Patterns Character/Create Player - Third Person", false, 11)]
        public static void CreateThirdPerson()
        {
            Create(CharacterPresetId.ThirdPerson);
        }

        [MenuItem("GameObject/Patterns Character/Create Player - Top Down", false, 12)]
        public static void CreateTopDown()
        {
            Create(CharacterPresetId.TopDown);
        }

        [MenuItem("GameObject/Patterns Character/Create Player - 2.5D Side Scroller", false, 13)]
        public static void CreateSideScroller()
        {
            Create(CharacterPresetId.SideScroller);
        }

        [MenuItem("GameObject/Patterns Character/Create Player - Everything", false, 14)]
        public static void CreateEverything()
        {
            Create(CharacterPresetId.Everything);
        }

        [MenuItem("GameObject/Patterns Character/Add Character Components To Selection", false, 30)]
        public static void AddToSelection()
        {
            GameObject target = Selection.activeGameObject;
            if (target == null)
            {
                EditorUtility.DisplayDialog("Patterns Character",
                    "Select a GameObject (usually a rigged model) in the hierarchy first.", "OK");
                return;
            }

            UpgradeExisting(target, CharacterPresetId.ThirdPerson);
        }

        [MenuItem("GameObject/Patterns Character/Setup Scene (Ground, Light, Volumes)", false, 40)]
        public static void SetupScene()
        {
            SceneSetupTools.SetupScene();
        }

        // ------------------------------------------------------------------ presets

        public enum CharacterPresetId
        {
            FirstPerson,
            ThirdPerson,
            TopDown,
            SideScroller,
            Everything
        }

        public static GameObject Create(CharacterPresetId preset)
        {
            GameObject root = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Patterns Character");

            Vector3 position = Vector3.zero;

            // Spawn in front of the scene view camera so the player is visible immediately.
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                position = view.pivot + view.camera.transform.forward * 6f;
                position.y = 0f;
            }

            root.transform.position = position;

            BuildCharacter(root, preset);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            Debug.Log("[Patterns.Character] Player created. Press Play: WASD to move, Space to jump, " +
                      "Shift to sprint, C to crouch, Ctrl to dash, E to interact, Tab to switch camera, Escape to free the cursor.", root);

            return root;
        }

        public static void UpgradeExisting(GameObject target, CharacterPresetId preset)
        {
            Undo.RegisterFullObjectHierarchyUndo(target, "Add Patterns Character");
            BuildCharacter(target, preset);
            Selection.activeGameObject = target;

            Debug.Log("[Patterns.Character] Character components added to " + target.name + ".", target);
        }

        // ------------------------------------------------------------------ construction

        static void BuildCharacter(GameObject root, CharacterPresetId preset)
        {
            bool firstPerson = preset == CharacterPresetId.FirstPerson;
            bool topDown = preset == CharacterPresetId.TopDown;
            bool sideScroller = preset == CharacterPresetId.SideScroller;
            bool everything = preset == CharacterPresetId.Everything;

            // ---- collision body
            CharacterController controller = GetOrAdd<CharacterController>(root);
            controller.height = DefaultHeight;
            controller.radius = DefaultRadius;
            controller.center = new Vector3(0f, DefaultHeight * 0.5f, 0f);
            controller.slopeLimit = sideScroller ? 55f : 50f;
            controller.stepOffset = sideScroller ? 0.3f : 0.4f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;

            // ---- core components
            CharacterResources resources = GetOrAdd<CharacterResources>(root);
            CharacterMotor motor = GetOrAdd<CharacterMotor>(root);
            CharacterLook look = GetOrAdd<CharacterLook>(root);
            CharacterContext context = GetOrAdd<CharacterContext>(root);

            // ---- abilities
            LocomotionAbility locomotion = GetOrAdd<LocomotionAbility>(root);
            OrientationAbility orientation = GetOrAdd<OrientationAbility>(root);
            SprintAbility sprint = GetOrAdd<SprintAbility>(root);
            CrouchAbility crouch = GetOrAdd<CrouchAbility>(root);
            JumpAbility jump = GetOrAdd<JumpAbility>(root);
            DashAbility dash = GetOrAdd<DashAbility>(root);
            GlideAbility glide = GetOrAdd<GlideAbility>(root);
            AimAbility aim = GetOrAdd<AimAbility>(root);
            InteractionAbility interaction = GetOrAdd<InteractionAbility>(root);
            FlyAbility fly = GetOrAdd<FlyAbility>(root);

            ConfigureAbilities(locomotion, orientation, sprint, crouch, jump, dash, glide, aim, interaction, fly,
                firstPerson, topDown, sideScroller, everything);

            // ---- presentation
            CharacterAnimatorDriver animatorDriver = GetOrAdd<CharacterAnimatorDriver>(root);
            CharacterAudioDriver audioDriver = GetOrAdd<CharacterAudioDriver>(root);

            // ---- input
            CharacterInputSourceBehaviour inputSource = EnsureInputSource(root);

            // ---- camera target
            Transform cameraTarget = EnsureChild(root, "CameraTarget", new Vector3(0f, DefaultHeight * 0.72f, 0f));

            // ---- camera rigs
            GameObject cameraRoot = EnsureChildObject(root, "CharacterCamera", Vector3.zero);
            CharacterCameraRigSwitcher switcher = GetOrAdd<CharacterCameraRigSwitcher>(cameraRoot);
            CameraShaker shaker = GetOrAdd<CameraShaker>(cameraRoot);

            List<CharacterCameraRig> rigs = new List<CharacterCameraRig>();
            bool listenerPlaced = false;

            rigs.Add(BuildRig(cameraRoot.transform, RigKind.FirstPerson, DefaultHeight, shaker, ref listenerPlaced));

            if (preset == CharacterPresetId.ThirdPerson || everything)
            {
                rigs.Add(BuildRig(cameraRoot.transform, RigKind.ThirdPerson, DefaultHeight, shaker, ref listenerPlaced));
            }

            if (topDown || everything)
            {
                rigs.Add(BuildRig(cameraRoot.transform, RigKind.TopDown, DefaultHeight, shaker, ref listenerPlaced));
            }

            if (sideScroller || everything)
            {
                rigs.Add(BuildRig(cameraRoot.transform, RigKind.SideScroller, DefaultHeight, shaker, ref listenerPlaced));
            }

            if (rigs.Count == 0)
            {
                rigs.Add(BuildRig(cameraRoot.transform, RigKind.ThirdPerson, DefaultHeight, shaker, ref listenerPlaced));
            }

            // The default rig for each preset, and the one that defines the initial look direction.
            string startRigId = "Third Person";
            if (firstPerson) startRigId = "First Person";
            else if (topDown) startRigId = "Top Down";
            else if (sideScroller) startRigId = "Follow (2.5D)";

            SetRigList(switcher, rigs, startRigId);

            // ---- wiring through SerializedObject, the fields are private on purpose
            WireContext(context, motor, look, resources, switcher, inputSource, cameraTarget);
            WireRig(rigs, cameraTarget);

            // The camera target is optional for the follow rig; keep it out of the way.
            if (sideScroller && cameraTarget != null)
            {
                cameraTarget.localPosition = new Vector3(0f, DefaultHeight * 0.5f, 0f);
            }

            look.SetLook(0f, 10f);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(cameraRoot);

            if (animatorDriver != null) animatorDriver.enabled = true;
            if (audioDriver != null) audioDriver.enabled = false; // Stays off until clips are assigned.
        }

        static void ConfigureAbilities(LocomotionAbility locomotion, OrientationAbility orientation, SprintAbility sprint,
            CrouchAbility crouch, JumpAbility jump, DashAbility dash, GlideAbility glide, AimAbility aim,
            InteractionAbility interaction, FlyAbility fly,
            bool firstPerson, bool topDown, bool sideScroller, bool everything)
        {
            locomotion.movementSpace = firstPerson
                ? MovementSpace.CharacterYaw
                : (topDown || sideScroller ? MovementSpace.CameraBasis : MovementSpace.CameraYaw);

            orientation.mode = firstPerson
                ? OrientationMode.FaceCamera
                : (sideScroller ? OrientationMode.FaceMovement : OrientationMode.FaceMovement);

            orientation.rotateOnlyWhenMoving = !firstPerson;
            orientation.executionOrder = CharacterAbility.DefaultExecutionOrderFor(typeof(OrientationAbility));

            locomotion.executionOrder = 0;
            locomotion.motorPriority = 0;

            sprint.executionOrder = CharacterAbility.DefaultExecutionOrderFor(typeof(SprintAbility));
            crouch.executionOrder = CharacterAbility.DefaultExecutionOrderFor(typeof(CrouchAbility));
            jump.executionOrder = CharacterAbility.DefaultExecutionOrderFor(typeof(JumpAbility));
            dash.executionOrder = CharacterAbility.DefaultExecutionOrderFor(typeof(DashAbility));
            glide.executionOrder = CharacterAbility.DefaultExecutionOrderFor(typeof(GlideAbility));
            aim.executionOrder = 150;
            interaction.executionOrder = CharacterAbility.DefaultExecutionOrderFor(typeof(InteractionAbility));
            fly.executionOrder = CharacterAbility.DefaultExecutionOrderFor(typeof(FlyAbility));

            dash.motorPriority = CharacterAbility.DefaultMotorPriorityFor(typeof(DashAbility));

            // Flight and ladders are opt in for the simple presets, always on for "Everything".
            fly.enabled = everything;
            glide.enabled = everything || !sideScroller;

            if (topDown)
            {
                // Top-down games usually want a fixed facing and no crouch.
                crouch.enabled = false;
                aim.enabled = false;
            }

            if (sideScroller)
            {
                crouch.enabled = false;
                aim.enabled = false;
                glide.enabled = false;
            }
        }

        enum RigKind
        {
            FirstPerson,
            ThirdPerson,
            TopDown,
            SideScroller
        }

        static CharacterCameraRig BuildRig(Transform parent, RigKind kind, float height, CameraShaker shaker, ref bool listenerPlaced)
        {
            string name;
            switch (kind)
            {
                case RigKind.FirstPerson: name = "FirstPersonRig"; break;
                case RigKind.TopDown: name = "TopDownRig"; break;
                case RigKind.SideScroller: name = "FollowRig"; break;
                default: name = "ThirdPersonRig"; break;
            }

            GameObject rigRoot = EnsureChildObject(parent.gameObject, name, Vector3.zero);
            GameObject cameraObject = EnsureChildObject(rigRoot, "Camera", Vector3.zero);

            Camera camera = GetOrAdd<Camera>(cameraObject);
            camera.tag = "MainCamera";

            if (!listenerPlaced)
            {
                if (cameraObject.GetComponent<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
                listenerPlaced = true;
            }
            else
            {
                AudioListener duplicate = cameraObject.GetComponent<AudioListener>();
                if (duplicate != null) UnityEngine.Object.DestroyImmediate(duplicate);
            }

            CharacterCameraRig rig;

            switch (kind)
            {
                case RigKind.FirstPerson:
                {
                    FirstPersonCameraRig fp = GetOrAdd<FirstPersonCameraRig>(rigRoot);
                    fp.rigId = "First Person";
                    fp.priority = 30;
                    fp.eyeOffset = new Vector3(0f, 0.02f, 0.1f);
                    fp.cameraComponent = camera;
                    rig = fp;
                    break;
                }
                case RigKind.TopDown:
                {
                    TopDownCameraRig td = GetOrAdd<TopDownCameraRig>(rigRoot);
                    td.rigId = "Top Down";
                    td.priority = 10;
                    td.pitch = 55f;
                    td.distance = 14f;
                    td.fixedYaw = 45f;
                    td.cameraComponent = camera;
                    rig = td;
                    break;
                }
                case RigKind.SideScroller:
                {
                    FollowCameraRig follow = GetOrAdd<FollowCameraRig>(rigRoot);
                    follow.rigId = "Follow (2.5D)";
                    follow.priority = 10;
                    follow.offset = new Vector3(0f, height * 0.6f, -9f);
                    follow.followAxisMask = new Vector3(1f, 1f, 0f);
                    follow.deadZone = 0.4f;
                    follow.cameraComponent = camera;
                    rig = follow;
                    break;
                }
                default:
                {
                    ThirdPersonCameraRig tp = GetOrAdd<ThirdPersonCameraRig>(rigRoot);
                    tp.rigId = "Third Person";
                    tp.priority = 20;
                    tp.distance = 4.5f;
                    tp.targetOffset = new Vector3(0f, 1.4f, 0f);
                    tp.cameraComponent = camera;
                    rig = tp;
                    break;
                }
            }

            rig.name = rigName(kind);

            if (shaker != null && shaker.target == null && kind == RigKind.ThirdPerson)
            {
                shaker.target = camera.transform;
            }

            return rig;
        }

        static string rigName(RigKind kind)
        {
            return kind.ToString();
        }

        static void SetRigList(CharacterCameraRigSwitcher switcher, List<CharacterCameraRig> rigs, string startRigId)
        {
            SerializedObject so = new SerializedObject(switcher);
            SerializedProperty list = so.FindProperty("rigs");
            if (list != null)
            {
                list.ClearArray();
                for (int i = 0; i < rigs.Count; i++)
                {
                    list.InsertArrayElementAtIndex(i);
                    list.GetArrayElementAtIndex(i).objectReferenceValue = rigs[i];
                }
            }

            SerializedProperty start = so.FindProperty("startRigId");
            if (start != null) start.stringValue = startRigId;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireContext(CharacterContext context, MotorBase motor, CharacterLook look,
            CharacterResources resources, CharacterCameraRigSwitcher switcher,
            CharacterInputSourceBehaviour inputSource, Transform cameraTarget)
        {
            SerializedObject so = new SerializedObject(context);

            SetRef(so, "motor", motor);
            SetRef(so, "look", look);
            SetRef(so, "resources", resources);
            SetRef(so, "cameraRig", switcher);
            SetRef(so, "cameraTarget", cameraTarget);
            SetRef(so, "explicitInputSource", inputSource);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireRig(List<CharacterCameraRig> rigs, Transform cameraTarget)
        {
            for (int i = 0; i < rigs.Count; i++)
            {
                CharacterCameraRig rig = rigs[i];
                if (rig == null) continue;

                SerializedObject so = new SerializedObject(rig);
                SerializedProperty pivot = so.FindProperty("yawPivot");
                if (pivot != null) pivot.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetRef(SerializedObject so, string field, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property != null) property.objectReferenceValue = value;
        }

        // ------------------------------------------------------------------ input source

        public static CharacterInputSourceBehaviour EnsureInputSource(GameObject root)
        {
            CharacterInputSourceBehaviour existing = root.GetComponent<CharacterInputSourceBehaviour>();
            if (existing != null) return existing;

#if PATTERNS_INPUTSYSTEM
            // The Input System support lives in its own assembly, so it is created through reflection.
            Type inputSystemType = Type.GetType("Patterns.Character.InputSystem.InputSystemSource, Patterns.Character.InputSystem");
            if (inputSystemType != null && PreferInputSystem())
            {
                CharacterInputSourceBehaviour created = root.AddComponent(inputSystemType) as CharacterInputSourceBehaviour;
                if (created != null) return created;
            }
#endif

            return root.AddComponent<LegacyInputSource>();
        }

        static bool PreferInputSystem()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return true;
#else
            return false;
#endif
        }

        // ------------------------------------------------------------------ helpers

        public static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            if (existing != null) return existing;
            return target.AddComponent<T>();
        }

        public static GameObject EnsureChildObject(GameObject parent, string name, Vector3 localPosition)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;

            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        public static Transform EnsureChild(GameObject parent, string name, Vector3 localPosition)
        {
            return EnsureChildObject(parent, name, localPosition).transform;
        }
    }
}