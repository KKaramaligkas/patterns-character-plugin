using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Patterns.Character.Editor.Tests
{
    /// <summary>
    /// Verifies that the editor wizard really produces a wired character. A wizard bug is invisible until
    /// someone presses Play, which is exactly the kind of thing worth testing.
    /// </summary>
    public class CharacterRigWizardTests
    {
        GameObject _created;

        [TearDown]
        public void TearDown()
        {
            if (_created != null) Object.DestroyImmediate(_created);
        }

        [Test]
        public void Wizard_ThirdPerson_CreatesWiredCharacter()
        {
            _created = CharacterRigWizard.Create(CharacterRigWizard.CharacterPresetId.ThirdPerson);
            Assert.IsNotNull(_created, "The wizard should return the created player");

            Assert.IsNotNull(_created.GetComponent<CharacterController>(), "Collision body");
            Assert.IsNotNull(_created.GetComponent<CharacterMotor>(), "Motor");
            Assert.IsNotNull(_created.GetComponent<CharacterResources>(), "Resources");
            Assert.IsNotNull(_created.GetComponent<CharacterLook>(), "Look");
            Assert.IsNotNull(_created.GetComponent<CharacterContext>(), "Context");
            Assert.IsNotNull(_created.GetComponent<CharacterInputSourceBehaviour>(), "Input source");

            Assert.IsNotNull(_created.GetComponent<LocomotionAbility>(), "Locomotion");
            Assert.IsNotNull(_created.GetComponent<OrientationAbility>(), "Orientation");
            Assert.IsNotNull(_created.GetComponent<SprintAbility>(), "Sprint");
            Assert.IsNotNull(_created.GetComponent<CrouchAbility>(), "Crouch");
            Assert.IsNotNull(_created.GetComponent<JumpAbility>(), "Jump");
            Assert.IsNotNull(_created.GetComponent<DashAbility>(), "Dash");
            Assert.IsNotNull(_created.GetComponent<InteractionAbility>(), "Interaction");

            Transform cameraRoot = _created.transform.Find("CharacterCamera");
            Assert.IsNotNull(cameraRoot, "Camera root");

            CharacterCameraRigSwitcher switcher = cameraRoot.GetComponent<CharacterCameraRigSwitcher>();
            Assert.IsNotNull(switcher, "Rig switcher");
            Assert.GreaterOrEqual(switcher.Rigs.Count, 2, "Third person preset should ship more than one camera mode");

            bool hasCamera = false;
            for (int i = 0; i < switcher.Rigs.Count; i++)
            {
                if (switcher.Rigs[i] != null && switcher.Rigs[i].cameraComponent != null) hasCamera = true;
            }
            Assert.IsTrue(hasCamera, "At least one rig should own a camera");
        }

        [Test]
        public void Wizard_WiresContextReferences()
        {
            _created = CharacterRigWizard.Create(CharacterRigWizard.CharacterPresetId.FirstPerson);

            CharacterContext context = _created.GetComponent<CharacterContext>();

            SerializedObject so = new SerializedObject(context);
            Assert.IsNotNull(so.FindProperty("motor").objectReferenceValue, "Motor reference should be wired");
            Assert.IsNotNull(so.FindProperty("look").objectReferenceValue, "Look reference should be wired");
            Assert.IsNotNull(so.FindProperty("resources").objectReferenceValue, "Resources reference should be wired");
            Assert.IsNotNull(so.FindProperty("cameraRig").objectReferenceValue, "Camera rig reference should be wired");
            Assert.IsNotNull(so.FindProperty("cameraTarget").objectReferenceValue, "Camera target should be wired");

            LocomotionAbility locomotion = _created.GetComponent<LocomotionAbility>();
            Assert.AreEqual(MovementSpace.CharacterYaw, locomotion.movementSpace, "First person should use character relative movement");

            OrientationAbility orientation = _created.GetComponent<OrientationAbility>();
            Assert.AreEqual(OrientationMode.FaceCamera, orientation.mode, "First person should face the camera");
        }

        [Test]
        public void Wizard_TopDown_UsesCameraBasisMovement()
        {
            _created = CharacterRigWizard.Create(CharacterRigWizard.CharacterPresetId.TopDown);

            LocomotionAbility locomotion = _created.GetComponent<LocomotionAbility>();
            Assert.AreEqual(MovementSpace.CameraBasis, locomotion.movementSpace);

            Transform cameraRoot = _created.transform.Find("CharacterCamera");
            CharacterCameraRigSwitcher switcher = cameraRoot.GetComponent<CharacterCameraRigSwitcher>();

            bool hasTopDown = false;
            for (int i = 0; i < switcher.Rigs.Count; i++)
            {
                if (switcher.Rigs[i] is TopDownCameraRig) hasTopDown = true;
            }
            Assert.IsTrue(hasTopDown, "Top down preset should include a top down rig");
        }

        [Test]
        public void Wizard_UpgradeExisting_KeepsModelComponents()
        {
            _created = new GameObject("Existing Model");
            MeshRenderer marker = _created.AddComponent<MeshRenderer>();

            CharacterRigWizard.UpgradeExisting(_created, CharacterRigWizard.CharacterPresetId.ThirdPerson);

            Assert.IsNotNull(_created.GetComponent<CharacterContext>(), "Character components should be added");
            Assert.IsNotNull(_created.GetComponent<MeshRenderer>(), "Existing components should be kept");
            Assert.AreEqual(marker, _created.GetComponent<MeshRenderer>());
        }

        [Test]
        public void Wizard_EveryPreset_ProducesAUsableCameraRig()
        {
            CharacterRigWizard.CharacterPresetId[] presets =
            {
                CharacterRigWizard.CharacterPresetId.FirstPerson,
                CharacterRigWizard.CharacterPresetId.ThirdPerson,
                CharacterRigWizard.CharacterPresetId.TopDown,
                CharacterRigWizard.CharacterPresetId.SideScroller,
                CharacterRigWizard.CharacterPresetId.Everything
            };

            for (int i = 0; i < presets.Length; i++)
            {
                _created = CharacterRigWizard.Create(presets[i]);

                Transform cameraRoot = _created.transform.Find("CharacterCamera");
                Assert.IsNotNull(cameraRoot, "Camera root missing for preset " + presets[i]);

                CharacterCameraRigSwitcher switcher = cameraRoot.GetComponent<CharacterCameraRigSwitcher>();
                Assert.IsNotNull(switcher, "Switcher missing for preset " + presets[i]);

                int withCamera = 0;
                for (int r = 0; r < switcher.Rigs.Count; r++)
                {
                    if (switcher.Rigs[r] != null && switcher.Rigs[r].cameraComponent != null) withCamera++;
                }

                Assert.AreEqual(switcher.Rigs.Count, withCamera, "Every rig of preset " + presets[i] + " needs a camera");

                Object.DestroyImmediate(_created);
                _created = null;
            }
        }
    }
}