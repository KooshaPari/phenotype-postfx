using UnityEngine;

namespace Phenotype.PostFx
{
    public sealed class PostStack : MonoBehaviour
    {
        static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        [Header("Pass Toggles")]
        public bool EnableSSAO = true;
        public bool EnableSSGI;
        public bool EnableBloom;
        public bool EnableACES = true;
        public bool EnableLUT = true;

        [Header("SSAO")]
        public int SSAOSamples = 12;
        public float SSAORadius = 2.0f;
        public float SSAOBias = 0.0012f;
        public float SSAOIntensity = 1.0f;

        [Header("SSGI")]
        public int SSGISamples = 12;
        public float SSGIRadius = 1.8f;
        public float SSGIIntensity = 0.45f;

        [Header("ACES")]
        public float Exposure = 1.0f;

        [Header("LUT")]
        public Texture2D LutTexture;

        Material _ssaoMat;
        Material _ssgiMat;
        Material _bloomMat;
        Material _acesMat;
        Material _lutMat;
        RenderTexture _ping;
        bool _initialized;
        static readonly Vector4[] _ssaoKernel = new Vector4[16];
        static readonly Vector4[] _ssgiKernel = new Vector4[12];
        static bool _kernelsBuilt;

        void Awake()
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null) cam.depthTextureMode |= DepthTextureMode.Depth;
            BuildKernels();
            InitMaterials();
        }

        void OnDestroy()
        {
            ReleaseMaterials();
            ReleasePingPong();
        }

        static void BuildKernels()
        {
            if (_kernelsBuilt) return;
            var rng = new System.Random(1337);
            for (int i = 0; i < _ssaoKernel.Length; i++)
            {
                Vector2 s;
                s.x = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble());
                s.y = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble());
                if (s.sqrMagnitude < 0.0001f) s = Vector2.right;
                s.Normalize();
                _ssaoKernel[i] = new Vector4(s.x, s.y, 0f, (i + 1f) / _ssaoKernel.Length);
            }
            rng = new System.Random(4242);
            for (int i = 0; i < _ssgiKernel.Length; i++)
            {
                Vector2 s;
                s.x = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble());
                s.y = Mathf.Lerp(-1f, 1f, (float)rng.NextDouble());
                if (s.sqrMagnitude < 0.0001f) s = Vector2.one;
                s.Normalize();
                _ssgiKernel[i] = new Vector4(s.x, s.y, 0f, 0f);
            }
            _kernelsBuilt = true;
        }

        void InitMaterials()
        {
            ReleaseMaterials();
            _ssaoMat = TryLoad("Shaders/ScreenSpaceAO", "Hidden/ScreenSpaceAO");
            _ssgiMat = TryLoad("Shaders/ScreenSpaceGI", "Hidden/ScreenSpaceGI");
            _bloomMat = TryLoad("Shaders/BrpBloom", "Hidden/Phenotype/BrpBloom");
            _acesMat = TryLoad("Shaders/BrpACES", "Hidden/Phenotype/BrpACES");

            Shader lutShader = Resources.Load<Shader>("Shaders/ColorGradingLUT");
            lutShader ??= Shader.Find("Hidden/ColorGradingLUT");
            if (lutShader != null)
            {
                _lutMat = new Material(lutShader);
                if (LutTexture != null)
                {
                    if (_lutMat.HasProperty("_LutTex")) _lutMat.SetTexture("_LutTex", LutTexture);
                    else if (_lutMat.HasProperty("_LookupTex")) _lutMat.SetTexture("_LookupTex", LutTexture);
                }
                if (_lutMat.HasProperty("_LutParams"))
                    _lutMat.SetVector("_LutParams", new Vector4(16f / 256f, 1f / 16f, 1f, 0f));
            }

            if (_ssaoMat != null) ApplySSAOParams();
            if (_ssgiMat != null) ApplySSGIParams();

            _initialized = true;
        }

        void ReleaseMaterials()
        {
            if (_ssaoMat != null) { Destroy(_ssaoMat); _ssaoMat = null; }
            if (_ssgiMat != null) { Destroy(_ssgiMat); _ssgiMat = null; }
            if (_bloomMat != null) { Destroy(_bloomMat); _bloomMat = null; }
            if (_acesMat != null) { Destroy(_acesMat); _acesMat = null; }
            if (_lutMat != null) { Destroy(_lutMat); _lutMat = null; }
        }

        static Material TryLoad(string resourcePath, string fallbackName)
        {
            Shader shader = Resources.Load<Shader>(resourcePath);
            shader ??= Shader.Find(fallbackName);
            return shader != null ? new Material(shader) : null;
        }

        void ApplySSAOParams()
        {
            _ssaoMat.SetInt("_SampleCount", SSAOSamples);
            _ssaoMat.SetVectorArray("_Samples", _ssaoKernel);
            _ssaoMat.SetFloat("_Radius", SSAORadius);
            _ssaoMat.SetFloat("_Bias", SSAOBias);
            _ssaoMat.SetFloat("_Intensity", SSAOIntensity);
        }

        void ApplySSGIParams()
        {
            _ssgiMat.SetInt("_SampleCount", SSGISamples);
            _ssgiMat.SetVectorArray("_Samples", _ssgiKernel);
            _ssgiMat.SetFloat("_Radius", SSGIRadius);
            _ssgiMat.SetFloat("_Intensity", SSGIIntensity);
        }

        void EnsurePingPong(RenderTexture src)
        {
            if (_ping != null && _ping.width == src.width && _ping.height == src.height) return;
            ReleasePingPong();
            _ping = RenderTexture.GetTemporary(src.descriptor);
        }

        void ReleasePingPong()
        {
            if (_ping != null) { RenderTexture.ReleaseTemporary(_ping); _ping = null; }
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (src == null || dst == null) return;
            if (!_initialized)
            {
                Graphics.Blit(src, dst);
                return;
            }

            bool anyPass = (EnableSSAO && _ssaoMat) || (EnableSSGI && _ssgiMat) ||
                           (EnableBloom && _bloomMat) || (EnableACES && _acesMat) ||
                           (EnableLUT && _lutMat);
            if (!anyPass)
            {
                Graphics.Blit(src, dst);
                return;
            }

            EnsurePingPong(src);
            try
            {
                RenderTexture cur = src, next = _ping;

                if (EnableSSAO && _ssaoMat)
                {
                    ApplySSAOParams();
                    Graphics.Blit(cur, next, _ssaoMat);
                    Swap(ref cur, ref next);
                }

                if (EnableSSGI && _ssgiMat)
                {
                    ApplySSGIParams();
                    Graphics.Blit(cur, next, _ssgiMat);
                    Swap(ref cur, ref next);
                }

                if (EnableBloom && _bloomMat)
                {
                    int w = Mathf.Max(1, src.width / 4);
                    int h = Mathf.Max(1, src.height / 4);
                    RenderTexture bA = RenderTexture.GetTemporary(w, h, 0, src.format);
                    RenderTexture bB = RenderTexture.GetTemporary(w, h, 0, src.format);
                    try
                    {
                        Graphics.Blit(cur, bA, _bloomMat, 0);
                        Graphics.Blit(bA, bB, _bloomMat, 1);
                        Graphics.Blit(bB, bA, _bloomMat, 2);
                        _bloomMat.SetTexture(BloomTexId, bA);
                        Graphics.Blit(cur, next, _bloomMat, 3);
                        Swap(ref cur, ref next);
                    }
                    finally
                    {
                        _bloomMat.SetTexture(BloomTexId, null);
                        RenderTexture.ReleaseTemporary(bA);
                        RenderTexture.ReleaseTemporary(bB);
                    }
                }

                if (EnableACES && _acesMat)
                {
                    _acesMat.SetFloat(ExposureId, Exposure);
                    Graphics.Blit(cur, next, _acesMat);
                    Swap(ref cur, ref next);
                }

                if (EnableLUT && _lutMat && LutTexture)
                    Graphics.Blit(cur, dst, _lutMat);
                else
                    Graphics.Blit(cur, dst);
            }
            finally
            {
                ReleasePingPong();
            }
        }

        static void Swap(ref RenderTexture a, ref RenderTexture b) => (a, b) = (b, a);
    }
}
