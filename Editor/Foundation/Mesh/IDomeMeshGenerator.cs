using System.Collections.Immutable;
using UnityEngine;

namespace KusakaFactory.Zatools.Foundation.Mesh
{
    /// <summary>
    /// 外周頂点列と重心情報からドーム状メッシュ片を生成するアルゴリズムの抽象。
    /// 実装ごとに頂点配置・三角形構成・補間方法が異なる。
    /// </summary>
    internal interface IDomeMeshGenerator
    {
        DomeMeshResult Generate(in DomeMeshInput input);
    }

    /// <summary>
    /// ドーム生成への入力。
    /// </summary>
    internal readonly struct DomeMeshInput
    {
        /// <summary>外周頂点の位置(順序は巡回順)。</summary>
        public ImmutableArray<Vector3> OuterPositions { get; }
        /// <summary>外周頂点の法線。エルミート補間の接線方向や法線補間に用いる。</summary>
        public ImmutableArray<Vector3> OuterNormals { get; }
        /// <summary>外周頂点のボーンウェイト。空配列または default の場合は default(BoneWeight) を割り当てる。</summary>
        public ImmutableArray<BoneWeight> OuterBoneWeights { get; }
        /// <summary>重心位置。</summary>
        public Vector3 CentroidPosition { get; }
        /// <summary>重心の法線。エルミート補間の接線方向や法線補間に用いる。</summary>
        public Vector3 CentroidNormal { get; }
        /// <summary>重心のボーンウェイト。</summary>
        public BoneWeight CentroidBoneWeight { get; }
        /// <summary>中間リング数(ループカット数)。アルゴリズムによっては無視される。0 以上。</summary>
        public int Subdivisions { get; }
        /// <summary>エルミート接線のスケール係数。外周-重心間の距離に乗じて適用する。アルゴリズムによっては無視される。</summary>
        public float TangentScale { get; }

        public DomeMeshInput(
            ImmutableArray<Vector3> outerPositions,
            ImmutableArray<Vector3> outerNormals,
            ImmutableArray<BoneWeight> outerBoneWeights,
            Vector3 centroidPosition,
            Vector3 centroidNormal,
            BoneWeight centroidBoneWeight,
            int subdivisions,
            float tangentScale
        )
        {
            OuterPositions = outerPositions;
            OuterNormals = outerNormals;
            OuterBoneWeights = outerBoneWeights;
            CentroidPosition = centroidPosition;
            CentroidNormal = centroidNormal;
            CentroidBoneWeight = centroidBoneWeight;
            Subdivisions = subdivisions;
            TangentScale = tangentScale;
        }
    }

    /// <summary>
    /// ドーム生成の結果。
    /// </summary>
    internal readonly struct DomeMeshResult
    {
        /// <summary>生成された頂点位置。</summary>
        public ImmutableArray<Vector3> Positions { get; }
        /// <summary>生成された頂点法線。</summary>
        public ImmutableArray<Vector3> Normals { get; }
        /// <summary>生成された頂点のボーンウェイト。</summary>
        public ImmutableArray<BoneWeight> BoneWeights { get; }
        /// <summary>三角形インデックス(0始まりのローカルインデックス)。呼び出し側でオフセットを加算すること。</summary>
        public ImmutableArray<int> Triangles { get; }

        public DomeMeshResult(
            ImmutableArray<Vector3> positions,
            ImmutableArray<Vector3> normals,
            ImmutableArray<BoneWeight> boneWeights,
            ImmutableArray<int> triangles
        )
        {
            Positions = positions;
            Normals = normals;
            BoneWeights = boneWeights;
            Triangles = triangles;
        }
    }
}
