using System.Globalization;
using System.IO;
using Lumon.GuardianReview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumon.HubWorld.Editor
{
    public static class HubWorldMotionPreview
    {
        private const string ScenePath = "Assets/Lumon/HubWorld/HubWorld.unity";
        private const string Output = "Logs/HubAmbientFrames";

        [MenuItem("Lumon/Open HubWorld")]
        public static void Open()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Capture()
        {
            Open();
            Camera camera = Camera.main;
            var stage = Object.FindAnyObjectByType<HubStage>();
            var motions = stage.GetComponentsInChildren<HubLayerMotion>();
            Directory.CreateDirectory(Output);
            for (int frame = 0; frame < 48; frame++)
            {
                float seconds = frame * 0.1f;
                foreach (HubLayerMotion motion in motions)
                    motion.Sample(seconds);

                string number = frame.ToString("00", CultureInfo.InvariantCulture);
                GuardianSpriteReview.CaptureFrame(camera, new Vector2Int(1280, 720), $"{Output}/frame-{number}.png");
            }
            Debug.Log($"Lumon HubWorld: rendered 48 motion preview frames for {motions.Length} layers.");
        }
    }
}
