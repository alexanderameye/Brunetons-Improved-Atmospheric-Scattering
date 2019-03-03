# Brunetons-Improved-Atmospheric-Scattering

This is a fork from the [Scrawk](https://github.com/Scrawk/Brunetons-Improved-Atmospheric-Scattering) repo. It uses the exposed interface "IBeforeCameraRender" (available from Unity's Scriptable LightweightPipeline) to generate and bind the Bruneton resources required to light a scene. This is a work in progress and is fairly clunky to set up.

How to use:
1. Import the Assets from this repository into a project
2. Assign the BrunetonSkyboxMaterial as the Skybox Material
3. Add a script compopnent to the main camera of your project and assign the BrunetonCameraScript to it
4. Assign the Precomputation shader as the compute shader for the BrunetonCameraScript

To use the lookups to light the scene: 
1. Merge "Lighting.hlsl.txt" with "Lighweight RP\ShaderLibrary\Lighting.hlsl"
2. Merge "LitForwardPass.hlsl.txt" with "Lighweight RP\Shaders\LitForwardPass.hlsl"

To tweak the Sky settings edit the BrunetonSkyboxMaterial properties:
- Mie Scattering (Scattering caused by particles with size comparable to the wavelengths of visible light)
- Rayleigh Scattering (Scattering caused by particles with sizemuch smaller than the wavelengths of visible light)
- Ozone (Scattering caused by the particles in the ozone layer)
- Phase (The Henyey-Greenstein phase function term)
- Fog (Amount of fog)
- Sun Size (Size of the sun)
- Sun Edge (Size of the edge of the sun)

![AtmosphericScatter0](https://i.imgur.com/iLEAWBH.jpg)


![AtmosphericScatter1](https://i.imgur.com/F5l7uMs.jpg)
