using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Set UV Tile by Texture")]
    [Icon("Packages/org.kb10uy.zatools/Resources/Icon.png")]
    [HelpURL("https://zatools.kb10uy.dev/ndmf-plugin/uv-tile-map-distribution/")]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class UvTileMapDistribution : ZatoolsMeshEditingComponent
    {
        public UvChannel Source = UvChannel.UV0;
        public UvChannel Target = UvChannel.UV2;
        public Texture2D TileMap;
        public TileDistribution Distribution = TileDistribution.RedGreen;
    }

    public enum UvChannel : int
    {
        [InspectorLabel("UV 0")] UV0 = 0,
        [InspectorLabel("UV 1")] UV1 = 1,
        [InspectorLabel("UV 2")] UV2 = 2,
        [InspectorLabel("UV 3")] UV3 = 3,
        [InspectorLabel("UV 4")] UV4 = 4,
        [InspectorLabel("UV 5")] UV5 = 5,
        [InspectorLabel("UV 6")] UV6 = 6,
        [InspectorLabel("UV 7")] UV7 = 7,
    }

    public enum TileDistribution
    {
        [InspectorLabel("Red / Green")] RedGreen,
        [InspectorLabel("ANSI 16")] Ansi16,
    }
}
