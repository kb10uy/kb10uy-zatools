using UnityEngine;

namespace KusakaFactory.Zatools.Runtime
{
    [AddComponentMenu("KusakaFactory/Zatools Set UV Tile by Texture")]
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
        [InspectorName("UV 0")] UV0 = 0,
        [InspectorName("UV 1")] UV1 = 1,
        [InspectorName("UV 2")] UV2 = 2,
        [InspectorName("UV 3")] UV3 = 3,
        [InspectorName("UV 4")] UV4 = 4,
        [InspectorName("UV 5")] UV5 = 5,
        [InspectorName("UV 6")] UV6 = 6,
        [InspectorName("UV 7")] UV7 = 7,
    }

    public enum TileDistribution
    {
        [InspectorName("Red / Green")] RedGreen,
        [InspectorName("ANSI 16")] Ansi16,
    }
}
