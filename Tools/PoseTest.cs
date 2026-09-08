using Biscotte.Rider;
using UnityEditor;
using UnityEngine;

namespace Biscotte.Tools
{
    /// <summary>
    /// Edit-mode preview of the procedural rider poses: a row of mannequins (each on a bodyboard) posed with RiderPose
    /// for several states, camera parked in front. Run Setup, then `capture_game_view`, then Cleanup.
    ///   unity command run_script --file Tools/PoseTest.cs --entry Biscotte.Tools.PoseTest.Setup --timeout_ms 120000 --timeout 200
    ///   unity command capture_game_view --save_path Screenshots~/poses.png
    ///   unity command run_script --file Tools/PoseTest.cs --entry Biscotte.Tools.PoseTest.SetupSide  (side view)
    ///   unity command run_script --file Tools/PoseTest.cs --entry Biscotte.Tools.PoseTest.Cleanup
    /// </summary>
    public static class PoseTest
    {
        const string kRoot = "PoseTest";
        const string kMannequin = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/Mannequin_F/Mannequin_F.fbx";

        public static string Setup() { return Build(0); }
        public static string SetupSide() { return Build(1); }
        public static string SetupTop() { return Build(2); }
        public static string SetupClosePaddle() { return Build(3, 1); }   // 3/4 close-up centred on figure 1 (paddle 90)
        public static string SetupCloseRide() { return Build(3, 4); }     // ride / ride lean R
        public static string SetupCloseAir() { return Build(3, 6); }      // air grab / duck dive / wipeout
        public static string SetupCloseDK() { return Build(3, 8); }       // drop-knee

        static string Build(int view, int focus = 0)
        {
            Cleanup();
            var mannequin = AssetDatabase.LoadAssetAtPath<GameObject>(kMannequin);
            if (mannequin == null) return "mannequin missing";
            var root = new GameObject(kRoot);
            root.transform.position = new Vector3(0f, 20f, -80f);   // away from the water, above the ocean plane

            var cases = new (string name, PoseInput input)[]
            {
                ("paddle 0", new PoseInput { state = RiderState.Paddle, paddle = 1f, paddlePhase = 0f, time = 1f }),
                ("paddle 90", new PoseInput { state = RiderState.Paddle, paddle = 1f, paddlePhase = Mathf.PI * 0.5f, time = 1f }),
                ("paddle rest", new PoseInput { state = RiderState.Paddle, paddle = 0f, time = 1f }),
                ("ride", new PoseInput { state = RiderState.Ride, speed = 8f, time = 1f, lookYaw = 40f }),
                ("ride lean R", new PoseInput { state = RiderState.Ride, speed = 8f, lean = 0.8f, time = 1f }),
                ("air grab", new PoseInput { state = RiderState.Air, airTuck = 1f, grab = true, time = 1f }),
                ("duck dive", new PoseInput { state = RiderState.DuckDive, stateTime = 0.6f, time = 1f }),
                ("wipeout", new PoseInput { state = RiderState.Wipeout, stateTime = 1f, time = 1f }),
                ("drop-knee", new PoseInput { state = RiderState.Ride, dropKnee = true, speed = 8f, time = 1f }),
            };

            var boardMesh = BodyboardMeshBuilder.Build();
            var boardMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.95f, 0.85f, 0.2f) };
            var suitMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.08f, 0.09f, 0.12f) };
            suitMat.SetFloat("_Smoothness", 0.55f);
            var sb = new System.Text.StringBuilder();
            float spacing = 1.7f;
            for (int i = 0; i < cases.Length; i++)
            {
                var slot = new GameObject(cases[i].name);
                slot.transform.SetParent(root.transform, false);
                slot.transform.localPosition = new Vector3(i * spacing, 0f, 0f);
                var board = new GameObject("Board");
                board.transform.SetParent(slot.transform, false);
                board.transform.localPosition = new Vector3(0f, 0.03f, 0.12f);
                board.AddComponent<MeshFilter>().sharedMesh = boardMesh;
                board.AddComponent<MeshRenderer>().sharedMaterial = boardMat;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(mannequin);
                inst.name = "Mannequin";
                inst.transform.SetParent(slot.transform, false);
                inst.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one * 0.93f;
                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>()) { smr.sharedMaterial = suitMat; smr.updateWhenOffscreen = true; }
                var anim = inst.GetComponent<Animator>();
                if (anim == null || anim.avatar == null || !anim.avatar.isHuman) { sb.Append("no humanoid avatar; "); continue; }
                anim.enabled = false;
                var handler = new HumanPoseHandler(anim.avatar, inst.transform);
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                var m = new float[RiderPose.MuscleCount];
                RiderPose.Solve(in cases[i].input, m, out Vector3 bp, out Quaternion br);
                pose.muscles = m; pose.bodyPosition = bp; pose.bodyRotation = br;
                handler.SetHumanPose(ref pose);
                handler.Dispose();
                sb.Append(cases[i].name).Append(" ok; ");
            }

            var cam = Camera.main;
            if (cam == null) return "no main camera";
            var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            Vector3 center = root.transform.position + new Vector3((cases.Length - 1) * spacing * 0.5f, 0.3f, 0f);
            Vector3 camPos;
            switch (view)
            {
                case 1: camPos = center + new Vector3(-9f, 1.2f, 0f); break;                 // from the left end, along the row
                case 2: camPos = center + new Vector3(0f, 9f, -0.1f); break;                 // top
                case 3:                                                                       // close 3/4 on one figure
                    center = root.transform.position + new Vector3(focus * spacing, 0.25f, 0.1f);
                    camPos = center + new Vector3(-2.2f, 1.5f, -2.6f); break;
                default: camPos = center + new Vector3(0f, 2.6f, -6.5f); break;              // front 3/4
            }
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(center - camPos, Vector3.up));
            cam.fieldOfView = view == 0 ? 75f : (view == 3 ? 50f : 60f);
            SceneView.RepaintAll();
            return sb.ToString();
        }

        public static string Cleanup()
        {
            var existing = GameObject.Find(kRoot);
            if (existing != null) Object.DestroyImmediate(existing);
            var cam = Camera.main;
            if (cam != null)
            {
                var brain = cam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
                if (brain != null) brain.enabled = true;
            }
            return "pose test removed";
        }
    }
}
