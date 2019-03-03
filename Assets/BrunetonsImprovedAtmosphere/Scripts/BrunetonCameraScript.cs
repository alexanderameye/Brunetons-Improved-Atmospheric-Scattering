using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering.LightweightPipeline;
using BrunetonsImprovedAtmosphere;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class BrunetonPrecomputeLookupsComponent : MonoBehaviour, IBeforeCameraRender
    {
        struct BrunetonParameters
        {
            public float m_mieScattering;
            public float m_raleightScattering;
            public float m_ozoneDesnity;
            public float m_phase;
            public float m_fogAmount;
            public float m_sunSize;
            public float m_sunEdge;

            static bool Equals(ref BrunetonParameters a, ref BrunetonParameters b)
            {
                return
                (
                    a.m_mieScattering == b.m_mieScattering &&
                    a.m_raleightScattering == b.m_raleightScattering &&
                    a.m_ozoneDesnity == b.m_ozoneDesnity &&
                    a.m_phase == b.m_phase
                );
            }
        };

        static BrunetonParameters BrunetonParams;

        static int kNumScatteringOrders = 4;
        static int kLambdaMin = 360;
        static int kLambdaMax = 830;
        static bool kUseConstantSolarSpectrum = false;
        static bool kUseOzone = true;
        static bool kUseCombinedTextures = true;
        static bool kUseHalfPrecision = false;
        static bool kDoWhiteBalance = false;
        static float kExposure = 10;
        static float kSunAngularRadius = 0.00935f / 2.0f * 7.5f;
        static float kBottomRadius = 6360000.0f;
        static float kLengthUnitInMeters = 1000.0f;
        static string kBrenetonComputeLookupsTag = "Breneton Compute Lookups";
        static LUMINANCE kUseLuminance = LUMINANCE.NONE;

        Model m_model;

        public bool BrunetonLookupsDirty()
        {
            Material skybox = RenderSettings.skybox;
            if (!skybox || !skybox.shader || skybox.shader.name != "Skybox/BunetonSkybox")
            {
                return false;
            }

            BrunetonParameters brunetonParamsToCompare;
            brunetonParamsToCompare.m_mieScattering = skybox.GetFloat("_MieScatteringScalar");
            brunetonParamsToCompare.m_raleightScattering = skybox.GetFloat("_RayleighScatteringScalar");
            brunetonParamsToCompare.m_ozoneDesnity = skybox.GetFloat("_OzoneDensity");
            brunetonParamsToCompare.m_phase = skybox.GetFloat("_Phase");

            brunetonParamsToCompare.m_fogAmount = skybox.GetFloat("_FogAmount");
            brunetonParamsToCompare.m_sunSize = skybox.GetFloat("_SunSize");
            brunetonParamsToCompare.m_sunEdge = skybox.GetFloat("_SunEdge");

            BrunetonParams.m_fogAmount = brunetonParamsToCompare.m_fogAmount;
            BrunetonParams.m_sunSize = brunetonParamsToCompare.m_sunSize;
            BrunetonParams.m_sunEdge = brunetonParamsToCompare.m_sunEdge;

            if (!BrunetonCameraScript.Equals(BrunetonParams, brunetonParamsToCompare))
            {
                BrunetonParams = brunetonParamsToCompare;
                return true;
            }

            return false;

        }
        void CreateBrunetonModel()
        {
            double[] kSolarIrradiance = new double[]
                {
                    1.11776, 1.14259, 1.01249, 1.14716, 1.72765, 1.73054, 1.6887, 1.61253,
                    1.91198, 2.03474, 2.02042, 2.02212, 1.93377, 1.95809, 1.91686, 1.8298,
                    1.8685, 1.8931, 1.85149, 1.8504, 1.8341, 1.8345, 1.8147, 1.78158, 1.7533,
                    1.6965, 1.68194, 1.64654, 1.6048, 1.52143, 1.55622, 1.5113, 1.474, 1.4482,
                    1.41018, 1.36775, 1.34188, 1.31429, 1.28303, 1.26758, 1.2367, 1.2082,
                    1.18737, 1.14683, 1.12362, 1.1058, 1.07124, 1.04992
                };

            double[] kOzoneCrossSection = new double[]
            {
                    1.18e-27, 2.182e-28, 2.818e-28, 6.636e-28, 1.527e-27, 2.763e-27, 5.52e-27,
                    8.451e-27, 1.582e-26, 2.316e-26, 3.669e-26, 4.924e-26, 7.752e-26, 9.016e-26,
                    1.48e-25, 1.602e-25, 2.139e-25, 2.755e-25, 3.091e-25, 3.5e-25, 4.266e-25,
                    4.672e-25, 4.398e-25, 4.701e-25, 5.019e-25, 4.305e-25, 3.74e-25, 3.215e-25,
                    2.662e-25, 2.238e-25, 1.852e-25, 1.473e-25, 1.209e-25, 9.423e-26, 7.455e-26,
                    6.566e-26, 5.105e-26, 4.15e-26, 4.228e-26, 3.237e-26, 2.451e-26, 2.801e-26,
                    2.534e-26, 1.624e-26, 1.465e-26, 2.078e-26, 1.383e-26, 7.105e-27
            };

            double kDobsonUnit = 2.687e20;
            double kMaxOzoneNumberDensity = 300.0 * kDobsonUnit / 15000.0;
            double kConstantSolarIrradiance = 1.5;
            double kTopRadius = 6420000.0;
            double kRayleigh = 1.24062e-6;
            double kRayleighScaleHeight = 8000.0;
            double kMieScaleHeight = 1200.0;
            double kMieAngstromAlpha = 0.0;
            double kMieAngstromBeta = 5.328e-3;
            double kMieSingleScatteringAlbedo = 0.9;
            double kGroundAlbedo = 0.1;
            double max_sun_zenith_angle = (kUseHalfPrecision ? 102.0 : 120.0) / 180.0 * Mathf.PI;

            DensityProfileLayer rayleigh_layer = new DensityProfileLayer("rayleigh", 0.0, 1.0, -1.0 / kRayleighScaleHeight, 0.0, 0.0);
            DensityProfileLayer mie_layer = new DensityProfileLayer("mie", 0.0, 1.0, -1.0 / kMieScaleHeight, 0.0, 0.0);

            List<DensityProfileLayer> ozone_density = new List<DensityProfileLayer>();
            ozone_density.Add(new DensityProfileLayer("absorption0", 25000.0, 0.0, 0.0, 1.0 / 15000.0, -2.0 / 3.0));
            ozone_density.Add(new DensityProfileLayer("absorption1", 0.0, 0.0, 0.0, -1.0 / 15000.0, 8.0 / 3.0));

            List<double> wavelengths = new List<double>();
            List<double> solar_irradiance = new List<double>();
            List<double> rayleigh_scattering = new List<double>();
            List<double> mie_scattering = new List<double>();
            List<double> mie_extinction = new List<double>();
            List<double> absorption_extinction = new List<double>();
            List<double> ground_albedo = new List<double>();

            for (int l = kLambdaMin; l <= kLambdaMax; l += 10)
            {
                double lambda = l * 1e-3;  // micro-meters
                double mie = kMieAngstromBeta / kMieScaleHeight * System.Math.Pow(lambda, -kMieAngstromAlpha);

                wavelengths.Add(l);

                if (kUseConstantSolarSpectrum)
                    solar_irradiance.Add(kConstantSolarIrradiance);
                else
                    solar_irradiance.Add(kSolarIrradiance[(l - kLambdaMin) / 10]);

                rayleigh_scattering.Add(kRayleigh * System.Math.Pow(lambda, -4) * BrunetonParams.m_raleightScattering);
                mie_scattering.Add(mie * kMieSingleScatteringAlbedo * BrunetonParams.m_mieScattering);
                mie_extinction.Add(mie);
                absorption_extinction.Add(kUseOzone ? BrunetonParams.m_ozoneDesnity * kMaxOzoneNumberDensity * kOzoneCrossSection[(l - kLambdaMin) / 10] : 0.0);
                ground_albedo.Add(kGroundAlbedo);
            }

            m_model = new Model();
            m_model.HalfPrecision = kUseHalfPrecision;
            m_model.CombineScatteringTextures = kUseCombinedTextures;
            m_model.UseLuminance = kUseLuminance;
            m_model.Wavelengths = wavelengths;
            m_model.SolarIrradiance = solar_irradiance;
            m_model.SunAngularRadius = kSunAngularRadius;
            m_model.BottomRadius = kBottomRadius;
            m_model.Exposure = kExposure;
            m_model.DoWhiteBalance = kDoWhiteBalance;
            m_model.TopRadius = kTopRadius;
            m_model.RayleighDensity = rayleigh_layer;
            m_model.RayleighScattering = rayleigh_scattering;
            m_model.MieDensity = mie_layer;
            m_model.MieScattering = mie_scattering;
            m_model.MieExtinction = mie_extinction;
            m_model.MiePhaseFunctionG = BrunetonParams.m_phase;
            m_model.AbsorptionDensity = ozone_density;
            m_model.AbsorptionExtinction = absorption_extinction;
            m_model.GroundAlbedo = ground_albedo;
            m_model.MaxSunZenithAngle = max_sun_zenith_angle;
            m_model.LengthUnitInMeters = kLengthUnitInMeters;
        }

        public void ExecuteBeforeCameraRender(LightweightRenderPipeline pipelineInstance, ScriptableRenderContext context, Camera camera)
        {
            CommandBuffer cmd = CommandBufferPool.Get(kBrenetonComputeLookupsTag);

            if ((m_model == null) || BrunetonLookupsDirty())
            {
                CreateBrunetonModel();
                m_model.Init(BrunetonCameraScript.BrunetonComputeShader, kNumScatteringOrders);
            }
            else
            {
                m_model.FogAmount = BrunetonParams.m_fogAmount;
                m_model.SunSize = BrunetonParams.m_sunSize;
                m_model.SunEdge = BrunetonParams.m_sunEdge;
                m_model.BindToPipeline(cmd, null, null);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}

[DisallowMultipleComponent]
[ExecuteInEditMode]
public class BrunetonCameraScript : MonoBehaviour
{
    // Hack to get the compute shader for the Editor's Camera
    static public ComputeShader BrunetonComputeShader;

    public ComputeShader m_compute;

    void Start()
    {
        BrunetonComputeShader = m_compute;
        
        BrunetonPrecomputeLookupsComponent toAdd = new BrunetonPrecomputeLookupsComponent();
        if (gameObject.GetComponent<BrunetonPrecomputeLookupsComponent>() == null)
            gameObject.AddComponent<BrunetonPrecomputeLookupsComponent>();

        foreach (SceneView sv in SceneView.sceneViews)
        {
            Camera cc = sv.camera;
            if (cc.GetComponent<BrunetonPrecomputeLookupsComponent>() == null)
                cc.gameObject.AddComponent<BrunetonPrecomputeLookupsComponent>();
        }
    }
}
