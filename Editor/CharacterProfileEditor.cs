using Patterns.Character;
using UnityEditor;
using UnityEngine;

namespace Patterns.Character.Editor
{
    /// <summary>Tuning helper for the character profile: sanity checks and quick actions.</summary>
    [CustomEditor(typeof(CharacterProfile))]
    public class CharacterProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CharacterProfile profile = (CharacterProfile)target;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);

            if (profile.standHeight < profile.crouchHeight)
            {
                EditorGUILayout.HelpBox("Stand height is smaller than crouch height. Standing up will look wrong.", MessageType.Warning);
            }

            if (profile.jumpHeight > 3f)
            {
                EditorGUILayout.HelpBox("Jump height above 3 m feels floaty for most games with this gravity. " +
                                        "Reduce the height or lower the gravity value.", MessageType.Info);
            }

            if (profile.airControl < 0.2f)
            {
                EditorGUILayout.HelpBox("Air control below 0.2 makes mid-air corrections very hard.", MessageType.Info);
            }

            if (Mathf.Abs(profile.gravity) < 9f)
            {
                EditorGUILayout.HelpBox("Gravity weaker than -9 m/s makes normal jumps feel like moon jumps.", MessageType.Info);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Preview Jump Velocity"))
            {
                float velocity = profile.JumpVelocityFor(profile.jumpHeight);
                float airTime = velocity / Mathf.Abs(profile.gravity) * 2f;
                Debug.LogFormat("[Patterns.Character] Jump height {0:0.00} m needs {1:0.00} m/s launch, total air time {2:0.00} s.",
                    profile.jumpHeight, velocity, airTime);
            }

            if (GUILayout.Button("Reset To Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset profile",
                        "Reset every value on this profile to the package defaults?", "Reset", "Cancel"))
                {
                    Undo.RecordObject(profile, "Reset Character Profile");
                    CharacterProfile defaults = ScriptableObject.CreateInstance<CharacterProfile>();
                    EditorUtility.CopySerialized(defaults, profile);
                    Object.DestroyImmediate(defaults);
                    AssetDatabase.SaveAssets();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>Motor inspector: shows the ground probe, slope state and runtime velocity.</summary>
    [CustomEditor(typeof(CharacterMotor))]
    public class CharacterMotorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CharacterMotor motor = (CharacterMotor)target;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime values appear here while playing.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField("Grounded", motor.IsGrounded.ToString());
            EditorGUILayout.LabelField("Velocity", motor.Velocity.ToString("F2"));
            EditorGUILayout.LabelField("Planar Speed", motor.PlanarSpeed.ToString("F2"));
            EditorGUILayout.LabelField("Air Time", motor.AirTime.ToString("F2"));
            EditorGUILayout.LabelField("Slope", motor.Ground.SlopeAngle.ToString("F1") + " deg");
            EditorGUILayout.LabelField("Steep Slope", motor.IsOnSteepSlope.ToString());
            EditorGUILayout.LabelField("Movement Blocked", motor.BlockReason.ToString());

            Repaint();
        }
    }
}