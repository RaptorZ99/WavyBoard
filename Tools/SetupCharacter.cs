using UnityEditor;
using UnityEngine;

namespace WavyBoard.Tools
{
    /// <summary>One-off: switch the Quaternius mannequin + animation library to Humanoid rigs and report avatar validity.
    /// Run: unity command run_script --file Tools/SetupCharacter.cs --entry WavyBoard.Tools.SetupCharacter.Main --timeout_ms 300000 --timeout 400</summary>
    public static class SetupCharacter
    {
        public const string MannequinPath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/Mannequin_F/Mannequin_F.fbx";
        public const string LibraryPath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/Unity/AnimationLibrary_Unity_Standard.fbx";

        public static string Main()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var path in new[] { MannequinPath, LibraryPath })
            {
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) { sb.Append("missing importer ").Append(path).Append('\n'); continue; }
                bool changed = false;
                if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; changed = true; }
                if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; }
                if (path == LibraryPath)
                {
                    // loop every clip, keep root motion baked so the character stays in place
                    var clips = imp.defaultClipAnimations;
                    foreach (var c in clips)
                    {
                        c.loopTime = true; c.loopPose = true;
                        c.lockRootHeightY = true; c.lockRootRotation = true; c.lockRootPositionXZ = true;
                        c.keepOriginalPositionY = true; c.keepOriginalOrientation = true; c.keepOriginalPositionXZ = true;
                    }
                    imp.clipAnimations = clips; changed = true;
                    if (!imp.importAnimation) { imp.importAnimation = true; }
                }
                if (changed) { imp.SaveAndReimport(); }
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var anim = go != null ? go.GetComponent<Animator>() : null;
                var avatar = anim != null ? anim.avatar : null;
                sb.Append(System.IO.Path.GetFileName(path)).Append(": type=").Append(imp.animationType)
                  .Append(" avatar=").Append(avatar != null ? avatar.name : "null")
                  .Append(" valid=").Append(avatar != null && avatar.isValid).Append(" human=").Append(avatar != null && avatar.isHuman)
                  .Append(" clips=").Append(imp.clipAnimations.Length).Append('\n');
                if (avatar != null && avatar.isHuman)
                {
                    var desc = avatar.humanDescription;
                    sb.Append("  mapped bones=").Append(desc.human.Length).Append(" armStretch=").Append(desc.armStretch).Append('\n');
                    foreach (var h in desc.human) if (h.humanName.Contains("Hips") || h.humanName.Contains("Head") || h.humanName.Contains("UpperChest") || h.humanName.Contains("Chest")) sb.Append("  ").Append(h.humanName).Append("->").Append(h.boneName).Append('\n');
                }
            }
            sb.Append("MUSCLES:");
            for (int i = 0; i < HumanTrait.MuscleCount; i++) sb.Append(i).Append('=').Append(HumanTrait.MuscleName[i]).Append(';');
            return sb.ToString();
        }
    }
}
