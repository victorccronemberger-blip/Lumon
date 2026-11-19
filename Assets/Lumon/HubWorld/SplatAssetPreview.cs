using GaussianSplatting.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lumon.HubWorld
{
    public sealed class SplatAssetPreview : MonoBehaviour
    {
        private Transform pivot;
        private Camera previewCamera;
        private RenderTexture target;
        private GaussianSplatRenderer model;
        private float spinSpeed;

        public Image Image { get; private set; }

        public void ConfigureEgg(int tier, Color[] palette, bool reduceMotion, bool transparentBackground)
        {
            bool rare = tier == 1;
            Vector3 center = new(50f + tier * 4f, 0f, 50f);
            Color background = transparentBackground
                ? Color.clear
                : palette.Length > 6 ? palette[6] : new Color(0.96f, 0.9f, 0.74f);
            Configure(center, new Vector3(3f, 2.1f, 4f), 0.52f, transparentBackground ? 0.76f : 0.71f, background, 256);
            Image.scaleMode = ScaleMode.ScaleAndCrop;
            pivot.localRotation = Quaternion.Euler(rare ? -10f : 10f, rare ? -20f : 20f, 0f);
            model.name = rare ? "Ovo Raro — Gaussian splat" : "Ovo Comum — Gaussian splat";
            model.m_SourcePath = rare ? "Lumon/Splats/egg-rare.ply" : "Lumon/Splats/egg-common.ply";
            spinSpeed = reduceMotion ? 0f : 36f;
        }

        public void ConfigureCreature()
        {
            Configure(new Vector3(-50f, 0f, 50f), new Vector3(2.8f, 1.75f, 4.8f),
                0.71f, 0.98f, Color.clear, 252);
        }

        public void ShowCreature(string species, bool reduceMotion)
        {
            string path = $"Lumon/Splats/lumon-{species}.ply";
            if (model.m_SourcePath == path) return;
            model.m_SourcePath = path;
            model.name = $"Lumon {species} — Gaussian splat";
            pivot.localRotation = Quaternion.identity;
            spinSpeed = reduceMotion ? 0f : 23f;
        }

        public void Turn()
        {
            spinSpeed = 0f;
            pivot.Rotate(0f, 90f, 0f, Space.World);
        }

        private void Configure(Vector3 center, Vector3 cameraOffset, float modelCenterY, float size, Color background, int textureSize)
        {
            transform.position = center;

            GameObject pivotObject = new("Eixo da prévia");
            pivotObject.transform.SetParent(transform, false);
            pivot = pivotObject.transform;

            GameObject modelObject = new("Modelo da prévia");
            modelObject.transform.SetParent(pivot, false);
            modelObject.transform.localPosition = new Vector3(0f, -modelCenterY, 0f);
            model = modelObject.AddComponent<GaussianSplatRenderer>();
            model.m_SHOrder = 0;

            target = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "Lumon — prévia",
                filterMode = FilterMode.Point,
                antiAliasing = 1
            };
            target.Create();
            GameObject cameraObject = new("Câmera da prévia");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = cameraOffset;
            cameraObject.transform.LookAt(center);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = background;
            previewCamera.orthographic = true;
            previewCamera.orthographicSize = size;
            previewCamera.nearClipPlane = 0.01f;
            previewCamera.farClipPlane = 20f;
            previewCamera.allowMSAA = false;
            previewCamera.allowHDR = false;
            previewCamera.targetTexture = target;
            SplatPixelRender pixel = cameraObject.AddComponent<SplatPixelRender>();
            pixel.Configure(2);

            Image = new Image
            {
                image = target,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
        }

        private void Update()
        {
            if (spinSpeed != 0f && pivot != null) pivot.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnDestroy()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (Image != null) Image.image = null;
            if (target != null)
            {
                target.Release();
                Destroy(target);
            }
        }
    }
}
