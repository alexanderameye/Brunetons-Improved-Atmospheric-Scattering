Shader "Skybox/BunetonSkybox" {

Properties {
    _MieScatteringScalar ("Mie Scattering", Range(0,20)) = 1
    _RayleighScatteringScalar("Rayleigh Scattering", Range(0,5)) = 1
    _OzoneDensity("Ozone Density", Range(0,5)) = 1.0
    _Phase ("Phase", Range(0,0.999)) = 0.8
    _FogAmount ("Fog", Range(0,1)) = 1.0
    _SunSize ("Sun Size", Range(0.1,5)) = 1.0
    _SunEdge ("Sun Edge", Range(0.1,5)) = 2.0
}

SubShader {
    Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
    Cull Off ZWrite Off

    Pass {

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag

        #include "UnityCG.cginc"
        #include "Lighting.cginc"

        #include "Definitions.cginc"
        #include "UtilityFunctions.cginc"
        #include "TransmittanceFunctions.cginc"
        #include "ScatteringFunctions.cginc"
        #include "IrradianceFunctions.cginc"
        #include "RenderingFunctions.cginc"

        // Uniforms
        float exposure;
        float3 white_point;
        float3 earth_center;
        float3 sun_direction;
        float2 sun_size;
        float sun_edge;

        float4x4 frustumCorners;

        sampler2D transmittance_texture;
        sampler2D irradiance_texture;
        sampler3D scattering_texture;
        sampler3D single_mie_scattering_texture;

        // Defines
        #define SKY_GROUND_THRESHOLD 0.02
        #define OUTER_RADIUS 1.025

    #if defined(UNITY_COLORSPACE_GAMMA)
        #define GAMMA 2
        #define COLOR_2_GAMMA(color) color
        #define COLOR_2_LINEAR(color) color*color
        #define LINEAR_2_OUTPUT(color) sqrt(color)
    #else
        #define GAMMA 2.2
        // HACK: to get gfx-tests in Gamma mode to agree until UNITY_ACTIVE_COLORSPACE_IS_GAMMA is working properly
        #define COLOR_2_GAMMA(color) ((unity_ColorSpaceDouble.r>2.0) ? pow(color,1.0/GAMMA) : color)
        #define COLOR_2_LINEAR(color) color
        #define LINEAR_2_LINEAR(color) color
    #endif

        // Statics
        static const float kCameraHeight = 0.0001;
        static const float kSphereRadius = 1.0;
        static const float kOuterRadius = OUTER_RADIUS;
        static const float kOuterRadius2 = OUTER_RADIUS*OUTER_RADIUS;
        static const float kInnerRadius = 1.0;
        static const float kInnerRadius2 = 1.0;
        static const float3 kSphereCenter = float3(0.0, 1.0, 0.0);
        static const float3 kSphereAlbedo = float3(0.8, 0.8, 0.8);
        static const float3 kGroundAlbedo = float3(0.0, 0.0, 0.04);

        RadianceSpectrum GetSolarRadiance() 
        {
            return solar_irradiance / (B_PI * sun_angular_radius * sun_angular_radius);
        }

        RadianceSpectrum GetSkyRadiance(
            Position camera, Direction view_ray, Length shadow_length,
            Direction sun_direction, out DimensionlessSpectrum transmittance) 
        {
            return GetSkyRadiance(transmittance_texture,
                scattering_texture, single_mie_scattering_texture,
                camera, view_ray, shadow_length, sun_direction, transmittance);
        }

        RadianceSpectrum GetSkyRadianceToPoint(
            Position camera, Position _point, Length shadow_length,
            Direction sun_direction, out DimensionlessSpectrum transmittance) 
        {
            return GetSkyRadianceToPoint(transmittance_texture,
                scattering_texture, single_mie_scattering_texture,
                camera, _point, shadow_length, sun_direction, transmittance);
        }

        IrradianceSpectrum GetSunAndSkyIrradiance(
            Position p, Direction normal, Direction sun_direction,
            out IrradianceSpectrum sky_irradiance) 
        {
            return GetSunAndSkyIrradiance(transmittance_texture,
                irradiance_texture, p, normal, sun_direction, sky_irradiance);
        }

        struct appdata_t
        {
            float4 vertex : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f
        {
            float4  pos             : SV_POSITION;
            float3  vertex          : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };


        v2f vert (appdata_t v)
        {
            v2f OUT;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
            OUT.pos = UnityObjectToClipPos(v.vertex);
            OUT.vertex = -v.vertex;

            return OUT;
        }

        half4 frag (v2f IN) : SV_Target
        {
            // Todo: Calculate the real shadow length
            const float shadow_length = 0.f;

            half3 col = half3(0.0, 0.0, 0.0);

            half3 ray = normalize(mul((float3x3)unity_ObjectToWorld, IN.vertex));
            half y = ray.y / SKY_GROUND_THRESHOLD;

            float3 camera = float3(0,3,0);
            float3 view_direction = -ray;

            // Hack to fade out light shafts when the Sun is very close to the horizon.
            float lightshaft_fadein_hack = smoothstep(0.02, 0.04, dot(normalize(camera - earth_center), sun_direction));

            // Compute the distance between the view ray line and the sphere center,
            // and the distance between the camera and the intersection of the view
            // ray with the sphere (or NaN if there is no intersection).
            float3 p = camera - kSphereCenter;
            float p_dot_v = dot(p, view_direction);
            float p_dot_p = dot(p, p);
            float ray_sphere_center_squared_distance = p_dot_p - p_dot_v * p_dot_v;
            float distance_to_intersection = -p_dot_v - sqrt(kSphereRadius * kSphereRadius - ray_sphere_center_squared_distance);

            // Compute the radiance reflected by the sphere, if the ray intersects it.
            float sphere_alpha = 0.0;
            float3 sphere_radiance = float3(0,0,0);

            p = camera - earth_center;
            p_dot_v = dot(p, view_direction);
            p_dot_p = dot(p, p);
            float ray_earth_center_squared_distance = p_dot_p - p_dot_v * p_dot_v;
            distance_to_intersection = -p_dot_v - sqrt(earth_center.y * earth_center.y - ray_earth_center_squared_distance);

            // Compute the radiance reflected by the ground, if the ray intersects it.
            float ground_alpha = 0.0;
            float3 ground_radiance = float3(0,0,0);
            if (distance_to_intersection > 0.0) 
            {
                float3 _point = camera + view_direction * distance_to_intersection;
                float3 normal = normalize(_point - earth_center);

                // Compute the radiance reflected by the ground.
                float3 sky_irradiance;
                float3 sun_irradiance = GetSunAndSkyIrradiance(_point - earth_center, normal, sun_direction, sky_irradiance);

                ground_radiance = kGroundAlbedo * (1.0 / B_PI) * (sun_irradiance + sky_irradiance);

                float3 transmittance;
                float3 in_scatter = GetSkyRadianceToPoint(camera - earth_center, _point - earth_center, shadow_length, sun_direction, transmittance);

                ground_radiance = ground_radiance * transmittance + in_scatter;
                ground_alpha = 1.0;
            }

            float3 transmittance;
            float3 radiance = GetSkyRadiance(camera - earth_center, view_direction, shadow_length, sun_direction, transmittance);

            float renormalized_angle_to_sun = dot(view_direction, sun_direction) - sun_size.y;
            float sun_gradient = (renormalized_angle_to_sun * sun_edge) / sun_size.x;
            radiance += transmittance * GetSolarRadiance() * saturate(sun_gradient); 

            radiance = lerp(radiance, ground_radiance, ground_alpha);
            radiance = lerp(radiance, sphere_radiance, sphere_alpha);
            radiance = pow(1.f - exp(-radiance * exposure), 1.0 / 2.2);

            col.rgb = radiance;

            return float4(col,1);
        }
        ENDCG
    }
}

}
