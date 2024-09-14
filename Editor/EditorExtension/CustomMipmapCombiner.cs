using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;
using UnityEditor.PackageManager;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class CustomMipmapCombiner : EditorWindow
    {
        [SerializeField]
        private string AssetNameStem = "CustomMipmap";
        [SerializeField]
        private Texture2D Mip0Texture;
        [SerializeField]
        private MipEntry[] Entries;

        private ObjectField _objectFieldMip0;
        private ListView _listViewSources;
        private TextField _textFieldAssetName;
        private Button _buttonGenerate;

        [MenuItem("Window/kb10uy/Custom Mipmap Combiner")]
        internal static void OpenWindow()
        {
            GetWindow<CustomMipmapCombiner>();
        }

        internal void CreateGUI()
        {
            var visualTree = Resources.LoadVisualTreeByGuid("0504f6ce497ece94c9c422bab44edd97");
            var visualTreeItem = Resources.LoadVisualTreeByGuid("ea294add14a5e8540aa84981c1a1787a");
            visualTree.CloneTree(rootVisualElement);
            rootVisualElement.Bind(new SerializedObject(this));
            ZatoolLocalization.UILocalizer.ApplyLocalizationFor(rootVisualElement);

            _objectFieldMip0 = rootVisualElement.Q<ObjectField>("FieldMip0Texture");
            _listViewSources = rootVisualElement.Q<ListView>("FieldSources");
            _textFieldAssetName = rootVisualElement.Q<TextField>("FieldAssetNameStem");
            _buttonGenerate = rootVisualElement.Q<Button>("ButtonGenerate");

            _objectFieldMip0.RegisterValueChangedCallback((e) => UpdateCanGenerate());
            _listViewSources.makeItem = visualTreeItem.CloneTree;
            _textFieldAssetName.RegisterValueChangedCallback((e) => UpdateCanGenerate());
            _buttonGenerate.clicked += Generate;

            UpdateCanGenerate();
        }

        private void UpdateCanGenerate()
        {
            _buttonGenerate.SetEnabled(!string.IsNullOrWhiteSpace(AssetNameStem) && Mip0Texture != null);
        }

        private void Generate()
        {
            // Source が指定されているもののうち先頭から順に各 LevelGeq ごとに 1 エントリだけになるようにする
            var orderedMipEntries = Entries
                .Where((e) => e.Source != null)
                .GroupBy((e) => e.LevelGeq)
                .ToDictionary((e) => e.Key, (e) => e.First());

            // Compute Shader でテクスチャから受け取る値は sRGB to Linear などが適用されている
            // ここでは Linear で作らなければならない
            var textureSize = 1024;
            var textureAsset = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, true, true, true);
            var allPixelsCount = textureSize * textureSize;

            var computeShader = Resources.LoadComputeShaderByGuid("bad178b10407a8e4d9302af0b90c8578");
            var computeKernelId = computeShader.FindKernel("CopyToMipmapRgba32");
            var mipmapBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allPixelsCount, 4);
            var pixelBuffer = new byte[allPixelsCount * 4];

            var sourceTexture = Mip0Texture;
            var mipBias = 0;
            computeShader.SetBuffer(computeKernelId, "MipmapPixels", mipmapBuffer);
            for (int mipLevel = 0; mipLevel < textureAsset.mipmapCount; ++mipLevel)
            {
                // 今の mipLevel に対して LevelGeq がヒットしたら更新する                
                if (orderedMipEntries.TryGetValue(mipLevel, out var entry))
                {
                    sourceTexture = entry.Source;
                    mipBias = entry.Bias;
                }
                var termN = textureAsset.mipmapCount - mipLevel;
                var mipWidth = 1 << (termN - 1);

                computeShader.SetTexture(computeKernelId, "SourceTexture", sourceTexture);
                computeShader.SetInt("MipBias", mipBias);
                computeShader.SetInt("MipWidth", mipWidth);
                computeShader.SetInt("MipLevel", mipLevel);
                computeShader.Dispatch(computeKernelId, mipWidth, mipWidth, 1);

                mipmapBuffer.GetData(pixelBuffer);
                textureAsset.SetPixelData(pixelBuffer, mipLevel, 0);
            }

            textureAsset.Apply(false);

            var assetPath = $"Assets/{AssetNameStem}.asset";
            AssetDatabase.CreateAsset(textureAsset, assetPath);
        }

        [System.Serializable]
        internal sealed class MipEntry
        {
            public Texture2D Source;
            public int Bias = 0;
            public int LevelGeq = 1;
        }
    }
}
