using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace KusakaFactory.Zatools.EditorExtension.MissingBlendShapeInserter
{
    internal sealed class MissingBlendShapeInserterProcess
    {
        const string BLENDSHAPE_PROPERTY_PREFIX = "blendShape.";

        private ImmutableList<(AnimationClip Clip, bool ApplyFilling)> _clipStatuses;
        private ImmutableList<AnimationClip> _sourceClips;
        private string _targetPath;
        private SkinnedMeshRenderer _valueSource;
        private string _saveDirectory;

        private ImmutableSortedSet<string> _targetBlendShapeNames;
        private ImmutableDictionary<string, float> _copyingBlendShapeValues;

        internal MissingBlendShapeInserterProcess(
            IEnumerable<(AnimationClip, bool)> clips,
            string path,
            SkinnedMeshRenderer valueSource,
            string saveDirectory
        )
        {
            _clipStatuses = clips.ToImmutableList();
            _sourceClips = _clipStatuses.Select((p) => p.Clip).ToImmutableList();
            _targetPath = path;
            _valueSource = valueSource;
            _saveDirectory = saveDirectory;

            _targetBlendShapeNames = CalculateTargetBlendShapeNamesUnion();
            _copyingBlendShapeValues = FetchBlendShapeValues();
        }


        internal ImmutableSortedSet<string> CalculateMissingBlendShapeNamesFor(AnimationClip clip)
        {
            if (!_sourceClips.Contains(clip)) throw new ArgumentException($"unintended clip specified");

            var existingBlendShapeNames = AnimationUtility.GetCurveBindings(clip)
                .Where(IsTargetCurveBinding)
                .Select((cb) => cb.propertyName[BLENDSHAPE_PROPERTY_PREFIX.Length..]);
            return _targetBlendShapeNames.Except(existingBlendShapeNames);
        }

        internal float GetCopyingBlendShapeValue(string blendShapeName) =>
            _copyingBlendShapeValues.TryGetValue(blendShapeName, out var value) ? value : 0.0f;

        private ImmutableSortedSet<string> CalculateTargetBlendShapeNamesUnion()
        {
            var curveBindings = _sourceClips
                .SelectMany(AnimationUtility.GetCurveBindings)
                .Where(IsTargetCurveBinding);
            return curveBindings
                .Select((cb) => cb.propertyName.Substring(11))
                .ToImmutableSortedSet();
        }

        private ImmutableDictionary<string, float> FetchBlendShapeValues()
        {
            if (_valueSource == null || _valueSource.sharedMesh == null) return ImmutableDictionary<string, float>.Empty;

            return Enumerable.Range(0, _valueSource.sharedMesh.blendShapeCount)
                .ToImmutableDictionary(
                    (i) => _valueSource.sharedMesh.GetBlendShapeName(i),
                    (i) => _valueSource.GetBlendShapeWeight(i)
                );
        }

        private bool IsTargetCurveBinding(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer)
                && binding.path == _targetPath
                && binding.propertyName.StartsWith(BLENDSHAPE_PROPERTY_PREFIX);
        }

        internal void Apply()
        {
            if (_clipStatuses.Count == 0) return;

            foreach (var clipStatus in _clipStatuses)
            {
                if (!clipStatus.ApplyFilling) continue;
                ApplyForClip(clipStatus.Clip);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Missing BlendShape Inserter", "AnimationClip の変更が適用されました。", "OK");
        }

        private void ApplyForClip(AnimationClip originalClip)
        {
            var editingClip = PrepareAnimationClipAsset(originalClip);
            var missingBlendShapes = CalculateMissingBlendShapeNamesFor(originalClip);
            foreach (var bs in missingBlendShapes)
            {
                var value = GetCopyingBlendShapeValue(bs);
                AnimationUtility.SetEditorCurve(
                    editingClip,
                    new EditorCurveBinding
                    {
                        type = typeof(SkinnedMeshRenderer),
                        path = _targetPath,
                        propertyName = $"{BLENDSHAPE_PROPERTY_PREFIX}{bs}"
                    },
                    new AnimationCurve(new Keyframe(0.0f, value))
                );
            }
        }

        private AnimationClip PrepareAnimationClipAsset(AnimationClip clip)
        {
            if (string.IsNullOrEmpty(_saveDirectory)) return clip;

            var copiedClip = UnityObject.Instantiate(clip);
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath($"{_saveDirectory}/{clip.name}_filled.anim");
            AssetDatabase.CreateAsset(copiedClip, uniquePath);
            return copiedClip;
        }
    }
}
