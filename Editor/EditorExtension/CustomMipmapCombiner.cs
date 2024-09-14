using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using KusakaFactory.Zatools.Localization;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class CustomMipmapCombiner : EditorWindow
    {
        [Serializable]
        internal sealed class MipEntry
        {
            public Texture2D Source;
            public int Bias = 0;
            public int LevelGeq = 1;
        }

        [SerializeField]
        private string AssetNameStem = "CustomMipmap";
        [SerializeField]
        private Texture2D Mip0Texture;
        [SerializeField]
        private MipEntry[] Entries;
        [SerializeField]
        private string TextureSizeString = "1024";
        [SerializeField]
        private string TextureFormatString = "RGBA32";

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
            _listViewSources.itemsAdded += (e) =>
            {
                // 最初の 1 個がゼロで初期化されるので明示的に 1 にしないといけない
                foreach (var i in e) Entries[i].LevelGeq = 1;
            };
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
            var textureSize = Convert.ToInt32(TextureSizeString);
            var (textureFormat, bufferStride, kernalName, bufferBindName) = TextureFormatString switch
            {
                "RGBA32" => (TextureFormat.RGBA32, 4, "CopyToMipmapRgba32", "Rgba32Pixels"),
                "RGBAFloat" => (TextureFormat.RGBAFloat, 16, "CopyToMipmapRgbaFloat", "RgbaFloatPixels"),
                "RGBAHalf" => (TextureFormat.RGBAHalf, 8, "CopyToMipmapRgbaHalf", "RgbaHalfPixels"),
                _ => throw new ArgumentException($"Unexpected Format String: {TextureFormatString}"),
            };
            var textureAsset = new Texture2D(textureSize, textureSize, textureFormat, true, true, true);
            var allPixelsCount = textureSize * textureSize;

            var computeShader = Resources.LoadComputeShaderByGuid("bad178b10407a8e4d9302af0b90c8578");
            var computeKernelId = computeShader.FindKernel(kernalName);
            var mipmapBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, allPixelsCount, bufferStride);
            var pixelBuffer = new byte[allPixelsCount * bufferStride];

            var sourceTexture = Mip0Texture;
            var mipBias = 0;
            computeShader.SetBuffer(computeKernelId, bufferBindName, mipmapBuffer);
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
    }
}
