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
        UV0 = 0,
        UV1 = 1,
        UV2 = 2,
        UV3 = 3,
        UV4 = 4,
        UV5 = 5,
        UV6 = 6,
        UV7 = 7,
    }

    public enum TileDistribution
    {
        RedGreen,
        Ansi16,
    }
}
