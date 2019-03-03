# Brunetons-Improved-Atmospheric-Scattering

This is a fork from the [Scrawk](https://github.com/Scrawk/Brunetons-Improved-Atmospheric-Scattering) repo. It uses the exposed interface "IBeforeCameraRender" (available from Unity's Scriptable LightweightPipeline) to generate and bind the Bruneton resources required to light a scene. This is a work in progress and is fairly clunky to set up.

How to use:
- Import the Assets from this repository into a project
- Assign the BrunetonSkyboxMaterial as the Skybox Material
- Add a script compopnent to the main camera of your project and assign the BrunetonCameraScript to it
- Assign the Precomputation shader as the compute shader for the BrunetonCameraScript

To use the lookups for lighting the scene: 
- Merge "Lighting.hlsl.txt" with "Lighweight RP\ShaderLibrary\Lighting.hlsl"
- Merge "LitForwardPass.hlsl.txt" with "Lighweight RP\Shaders\LitForwardPass.hlsl"

![AtmosphericScatter0](https://i.imgur.com/iLEAWBH.jpg)


![AtmosphericScatter1](https://i.imgur.com/F5l7uMs.jpg)
