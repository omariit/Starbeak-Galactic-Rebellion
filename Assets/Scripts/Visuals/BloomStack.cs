using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Lightweight additive bloom for the built-in pipeline: soft-knee bright pass,
    /// separable gaussian blur, camera composite. Fully disabled on the lowest
    /// Android quality tiers and whenever the custom shaders are stripped.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BloomStack : MonoBehaviour
    {
        [Range(0f, 3f)] public float intensity = 1.15f;
        [Range(0f, 2f)] public float threshold = 0.6f;
        [Range(0.01f, 1f)] public float knee = 0.4f;
        [Range(0.2f, 3f)] public float blurSpread = 1.3f;

        private Shader brightShader;
        private Shader blurShader;
        private Shader compositeShader;
        private Material brightMat;
        private Material blurMat;
        private Material compositeMat;
        private bool unavailable;

        private static readonly int BlurDir = Shader.PropertyToID("_BlurDir");
        private static readonly int Threshold = Shader.PropertyToID("_Threshold");
        private static readonly int Knee = Shader.PropertyToID("_Knee");
        private static readonly int Intensity = Shader.PropertyToID("_Intensity");
        private static readonly int BloomTex = Shader.PropertyToID("_BloomTex");

        private void OnEnable()
        {
            int level = QualitySettings.GetQualityLevel();
            if (level < 2 || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (compositeMat != null || unavailable) return;
            PrepareMaterials();
        }

        private void PrepareMaterials()
        {
            brightShader = Shader.Find("Hidden/StarbeakBright");
            blurShader = Shader.Find("Hidden/StarbeakBlur");
            compositeShader = Shader.Find("Hidden/StarbeakComposite");

            if (brightShader == null || blurShader == null || compositeShader == null)
            {
                unavailable = true;
                return;
            }

            brightMat = new Material(brightShader) { hideFlags = HideFlags.HideAndDontSave };
            blurMat = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
            compositeMat = new Material(compositeShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnDisable()
        {
            if (brightMat != null) DestroyMaterial(brightMat);
            if (blurMat != null) DestroyMaterial(blurMat);
            if (compositeMat != null) DestroyMaterial(compositeMat);
            brightMat = blurMat = compositeMat = null;
        }

        private static void DestroyMaterial(Material m)
        {
            if (m != null) DestroyImmediate(m);
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            int level = QualitySettings.GetQualityLevel();
            if (unavailable || level < 2 || compositeMat == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            int dw = level >= 4 ? 2 : 3;
            int bw = level >= 4 ? 1 : 2; // further downsample for the blur surfaces

            int bwH = Mathf.Max(8, source.height >> (dw + bw));
            int bwW = Mathf.Max(8, source.width >> (dw + bw));
            int hw = Mathf.Max(8, source.height >> dw);
            int ww = Mathf.Max(8, source.width >> dw);

            RenderTexture bright = RenderTexture.GetTemporary(ww, hw, 0, source.graphicsFormat);
            RenderTexture blurA = RenderTexture.GetTemporary(bwW, bwH, 0, source.graphicsFormat);
            RenderTexture blurB = RenderTexture.GetTemporary(bwW, bwH, 0, source.graphicsFormat);

            brightMat.SetFloat(Threshold, threshold);
            brightMat.SetFloat(Knee, knee);
            Graphics.Blit(source, bright, brightMat);

            float step = 1.3f / bwH * blurSpread * 200f;
            blurMat.SetVector(BlurDir, new Vector4(step / 512f, 0f, 0f, 0f));
            Graphics.Blit(bright, blurA, blurMat);

            blurMat.SetVector(BlurDir, new Vector4(0f, step / 512f, 0f, 0f));
            Graphics.Blit(blurA, blurB, blurMat);

            if (level >= 4)
            {
                float step2 = step * 2.1f;
                blurMat.SetVector(BlurDir, new Vector4(step2 / 512f, 0f, 0f, 0f));
                Graphics.Blit(blurB, blurA, blurMat);
                blurMat.SetVector(BlurDir, new Vector4(0f, step2 / 512f, 0f, 0f));
                Graphics.Blit(blurA, blurB, blurMat);
            }

            compositeMat.SetFloat(Intensity, intensity);
            compositeMat.SetTexture(BloomTex, blurB);
            Graphics.Blit(source, destination, compositeMat);

            RenderTexture.ReleaseTemporary(bright);
            RenderTexture.ReleaseTemporary(blurA);
            RenderTexture.ReleaseTemporary(blurB);
        }
    }
}
