using Patterns.Character;
using UnityEditor;
using UnityEngine;

namespace Patterns.Character.Editor
{
    /// <summary>Scene helpers: ground, light, a test obstacle course and character volumes.</summary>
    public static class SceneSetupTools
    {
        [MenuItem("Tools/Patterns Character/Setup Scene/Add Ground And Light", false, 20)]
        public static void SetupScene()
        {
            EnsureGround();
            EnsureLight();
            Debug.Log("[Patterns.Character] Ground and directional light are in place.");
        }

        [MenuItem("Tools/Patterns Character/Setup Scene/Add Test Obstacle Course", false, 21)]
        public static void SetupObstacleCourse()
        {
            var root = new GameObject("Patterns Test Course");
            Undo.RegisterCreatedObjectUndo(root, "Create Test Course");

            EnsureGround();

            // Slopes at different angles to test the slope limit and the slide behaviour.
            CreateRamp(root.transform, "Ramp 30", new Vector3(6f, 0f, 0f), 30f, 6f);
            CreateRamp(root.transform, "Ramp 60", new Vector3(12f, 0f, 0f), 60f, 6f);

            // Steps to test step offset.
            for (int i = 0; i < 5; i++)
            {
                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = "Step " + (i + 1);
                step.transform.SetParent(root.transform, true);
                step.transform.position = new Vector3(-6f - i * 0.8f, 0.15f + i * 0.3f, 0f);
                step.transform.localScale = new Vector3(0.8f, 0.3f + i * 0.3f, 1.6f);
            }

            // A wall to test sliding and wall collision.
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(root.transform, true);
            wall.transform.position = new Vector3(0f, 1.5f, 8f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.3f);

            // Platforms to test jumps and air control.
            CreatePlatform(root.transform, "Platform Low", new Vector3(0f, 0.8f, -5f), 3f);
            CreatePlatform(root.transform, "Platform High", new Vector3(3f, 1.8f, -8f), 3f);

            // Water volume and a ladder, so swim and ladder abilities can be tried immediately.
            CreateWaterVolume(root.transform, new Vector3(-12f, -0.6f, 6f));
            CreateLadder(root.transform, new Vector3(12f, 2.5f, 6f));

            Selection.activeGameObject = root;
            Debug.Log("[Patterns.Character] Test course created. Volumes use the CharacterVolume component.");
        }

        [MenuItem("Tools/Patterns Character/Setup Scene/Add Water Volume", false, 30)]
        public static void AddWaterVolume()
        {
            CreateWaterVolume(null, Vector3.zero + new Vector3(0f, -0.6f, 0f));
        }

        [MenuItem("Tools/Patterns Character/Setup Scene/Add Ladder", false, 31)]
        public static void AddLadder()
        {
            CreateLadder(null, new Vector3(0f, 2.5f, 0f));
        }

        [MenuItem("Tools/Patterns Character/Setup Scene/Add Moving Platform", false, 32)]
        public static void AddMovingPlatform()
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Moving Platform";
            platform.transform.position = new Vector3(0f, 1f, 0f);
            platform.transform.localScale = new Vector3(2f, 0.3f, 2f);
            Undo.RegisterCreatedObjectUndo(platform, "Create Moving Platform");

            MovingPlatform mover = platform.AddComponent<MovingPlatform>();
            Undo.RegisterCreatedObjectUndo(mover, "Create Moving Platform");

            Selection.activeGameObject = platform;
        }

        // ------------------------------------------------------------------ builders

        static void EnsureGround()
        {
            GameObject existing = GameObject.Find("Ground");
            if (existing != null) return;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
        }

        static void EnsureLight()
        {
#if UNITY_2023_1_OR_NEWER
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            Light[] lights = Object.FindObjectsOfType<Light>();
#endif
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) return;
            }

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Undo.RegisterCreatedObjectUndo(lightObject, "Create Directional Light");
        }

        static void CreateRamp(Transform parent, string name, Vector3 position, float angle, float length)
        {
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.SetParent(parent, true);
            ramp.transform.position = position + new Vector3(0f, Mathf.Sin(angle * Mathf.Deg2Rad) * length * 0.5f, 0f);
            ramp.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.transform.localScale = new Vector3(3f, 0.3f, length);
            Undo.RegisterCreatedObjectUndo(ramp, "Create Ramp");
        }

        static void CreatePlatform(Transform parent, string name, Vector3 position, float size)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = name;
            platform.transform.SetParent(parent, true);
            platform.transform.position = position;
            platform.transform.localScale = new Vector3(size, 0.3f, size);
            Undo.RegisterCreatedObjectUndo(platform, "Create Platform");
        }

        static void CreateWaterVolume(Transform parent, Vector3 position)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Water";
            if (parent != null) water.transform.SetParent(parent, true);
            water.transform.position = position + new Vector3(0f, 1f, 0f);
            water.transform.localScale = new Vector3(12f, 2f, 12f);
            Undo.RegisterCreatedObjectUndo(water, "Create Water");

            Collider collider = water.GetComponent<Collider>();
            collider.isTrigger = true;

            CharacterVolume volume = water.AddComponent<CharacterVolume>();
            volume.type = VolumeType.Water;
            volume.drag = 0.4f;
            volume.buoyancyMultiplier = 1f;

            Selection.activeGameObject = water;
        }

        static void CreateLadder(Transform parent, Vector3 position)
        {
            GameObject ladder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ladder.name = "Ladder";
            if (parent != null) ladder.transform.SetParent(parent, true);
            ladder.transform.position = position;
            ladder.transform.localScale = new Vector3(1f, 6f, 0.25f);
            Undo.RegisterCreatedObjectUndo(ladder, "Create Ladder");
            ladder.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            BoxCollider collider = ladder.GetComponent<BoxCollider>();
            collider.isTrigger = true;

            CharacterVolume volume = ladder.AddComponent<CharacterVolume>();
            volume.type = VolumeType.Ladder;

            Selection.activeGameObject = ladder;
        }
    }

    /// <summary>Menu entries for profiles and documentation.</summary>
    public static class PatternsCharacterMenu
    {
        const string DocsUrl = "https://github.com/KKaramaligkas/patterns-character-plugin#readme";

        [MenuItem("Tools/Patterns Character/Create Profile Asset", false, 1)]
        public static void CreateProfile()
        {
            CharacterProfile profile = ScriptableObject.CreateInstance<CharacterProfile>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CharacterProfile.asset");
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(profile);
            Selection.activeObject = profile;
        }

        [MenuItem("Tools/Patterns Character/Profiles/Realistic", false, 40)]
        public static void CreateRealistic()
        {
            ApplyPreset("Realistic", p =>
            {
                p.walkSpeed = 1.6f;
                p.runSpeed = 4.2f;
                p.sprintSpeed = 6.2f;
                p.crouchSpeed = 1f;
                p.acceleration = 12f;
                p.deceleration = 16f;
                p.jumpHeight = 0.9f;
                p.gravity = -22f;
                p.rotationSpeed = 640f;
                p.snappyRotation = false;
            });
        }

        [MenuItem("Tools/Patterns Character/Profiles/Arcade", false, 41)]
        public static void CreateArcade()
        {
            ApplyPreset("Arcade", p =>
            {
                p.walkSpeed = 3f;
                p.runSpeed = 6.5f;
                p.sprintSpeed = 10f;
                p.crouchSpeed = 1.8f;
                p.acceleration = 30f;
                p.deceleration = 34f;
                p.jumpHeight = 1.6f;
                p.gravity = -30f;
                p.airJumps = 2;
                p.airControl = 0.8f;
                p.dashSpeed = 20f;
                p.dashDuration = 0.16f;
            });
        }

        [MenuItem("Tools/Patterns Character/Profiles/Floaty", false, 42)]
        public static void CreateFloaty()
        {
            ApplyPreset("Floaty", p =>
            {
                p.gravity = -12f;
                p.maxFallSpeed = 16f;
                p.jumpHeight = 2.2f;
                p.airJumps = 3;
                p.airControl = 0.9f;
                p.glideFallSpeedMultiplier = 0.12f;
                p.waterDrag = 0.2f;
                p.buoyancy = 6f;
            });
        }

        [MenuItem("Tools/Patterns Character/Open Online Documentation", false, 60)]
        public static void OpenDocs()
        {
            Application.OpenURL(DocsUrl);
        }

        static void ApplyPreset(string label, System.Action<CharacterProfile> configure)
        {
            CharacterProfile profile = ScriptableObject.CreateInstance<CharacterProfile>();
            configure(profile);

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/CharacterProfile - " + label + ".asset");
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(profile);
            Selection.activeObject = profile;
        }
    }
}