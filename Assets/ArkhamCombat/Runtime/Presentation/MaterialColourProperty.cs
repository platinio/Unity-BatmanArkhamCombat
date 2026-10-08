using UnityEngine;

namespace ArkhamCombat.Presentation
{
    /// <summary>The shader property that holds a material's main colour, in URP and in built-in shaders alike.</summary>
    public static class MaterialColourProperty
    {
        private static readonly int UrpBaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int BuiltInColour = Shader.PropertyToID("_Color");

        public static int Of(Material material) =>
            material != null && material.HasProperty(UrpBaseColour) ? UrpBaseColour : BuiltInColour;
    }
}
