using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lumon.GuardianReview
{
    public static class GuardianDirectionalReview
    {
        [Serializable]
        private sealed class Pose
        {
            public string direction;
            public float pivotX;
            public float pivotY;
            public int opaqueHeight;
        }

        [Serializable]
        private sealed class Atlas
        {
            public Pose[] poses;
        }

        public static void InstallAndVerify()
        {
            const string art = "Art/Guardian2D/";
            Atlas atlas = JsonUtility.FromJson<Atlas>(File.ReadAllText(art + "idle-directions.json"));
            if (atlas.poses == null || atlas.poses.Length != 8)
                throw new InvalidOperationException("Eight approved directional poses are required.");

            var sprites = new Sprite[8];
            var fingerprints = new HashSet<string>();
            using var signature = SHA256.Create();
            foreach (Pose pose in atlas.poses)
            {
                if (!Enum.TryParse(pose.direction, out GuardianSpriteFacing.Direction direction)
                    || (uint)direction >= sprites.Length || sprites[(int)direction] != null
                    || pose.opaqueHeight < 280 || pose.pivotX <= 0 || pose.pivotX >= 1
                    || pose.pivotY <= 0 || pose.pivotY >= 0.12f)
                    throw new InvalidOperationException($"Invalid pose registration for {pose.direction}.");
                string name = "Directions/" + direction + ".png";
                string path = "Assets/Lumon/GuardianReview/Art2D/" + name;
                if (!fingerprints.Add(Convert.ToBase64String(signature.ComputeHash(File.ReadAllBytes(path)))))
                    throw new InvalidOperationException($"Duplicate texture for {direction}.");
                float ppu = pose.opaqueHeight / (8.2f * 165f / 720f);
                sprites[(int)direction] = GuardianSpriteReview.Import(name, ppu,
                    new Vector2(pose.pivotX, pose.pivotY), true);
                if (sprites[(int)direction] == null)
                    throw new InvalidOperationException($"Could not import the pose for {direction}.");
            }

            GuardianSpriteReview.Open();
            Camera camera = Camera.main;
            var actor = UnityEngine.Object.FindAnyObjectByType<GuardianMotion>();
            var renderer = GameObject.Find("Guardian illustration").GetComponent<SpriteRenderer>();
            GuardianSpriteFacing facing = renderer.GetComponent<GuardianSpriteFacing>();
            if (facing == null)
                facing = renderer.gameObject.AddComponent<GuardianSpriteFacing>();

            var visual = new SerializedObject(facing);
            SerializedProperty poses = visual.FindProperty("idleByDirection");
            poses.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                poses.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            visual.ApplyModifiedPropertiesWithoutUndo();
            var motion = new SerializedObject(actor);
            motion.FindProperty("directionalArt").objectReferenceValue = facing;
            motion.ApplyModifiedPropertiesWithoutUndo();

            string output = art + "review/idle-directions";
            Directory.CreateDirectory(output);
            VerifyIdles(actor, facing, renderer, camera, sprites, output);

            facing.Face(new Vector2(1f, -1f));
            if (!EditorSceneManager.SaveScene(actor.gameObject.scene))
                throw new IOException("Could not save the eight-direction Guardian scene.");
        }

        // One production tick per rumo, at whatever pace the scene stores. Captures are
        // skipped without an output folder, so the walk review can rerun these checks.
        internal static void VerifyIdles(GuardianMotion actor, GuardianSpriteFacing facing,
            SpriteRenderer renderer, Camera camera, Sprite[] sprites, string output)
        {
            Vector3 origin = actor.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            Vector2[] inputs = {
                Vector2.up, new Vector2(1, 1), Vector2.right, new Vector2(1, -1),
                Vector2.down, new Vector2(-1, -1), Vector2.left, new Vector2(-1, 1),
            };
            for (int i = 0; i < inputs.Length; i++)
            {
                actor.transform.position = origin;
                actor.Move(inputs[i], 1f / 30f);
                if (facing.Current != (GuardianSpriteFacing.Direction)i
                    || Mathf.Abs((actor.transform.position - origin).magnitude - actor.Speed / 30f) > 0.0001f
                    || Quaternion.Angle(cameraRotation, camera.transform.rotation) > 0.01f)
                    throw new InvalidOperationException($"Input did not select {(GuardianSpriteFacing.Direction)i}.");
                actor.Move(Vector2.zero, 1f / 30f);
                if (renderer.sprite != sprites[i])
                    throw new InvalidOperationException("Stopping must preserve the last direction.");
                actor.transform.position = origin;
                if (output != null)
                    GuardianSpriteReview.CaptureFrame(camera, new Vector2Int(1280, 720),
                        output + "/" + facing.Current + ".png");
                Debug.Log($"Lumon guardian {facing.Current}: input, speed, idle and camera passed.");
            }
        }
    }
}
