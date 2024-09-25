using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal sealed class MissingBlendShapeInserterProcess
    {
        private ImmutableList<AnimationClip> _clips;
        private string _targetPath;
        private SkinnedMeshRenderer _valueSource;

        private ImmutableSortedSet<string> _targetBlendShapeNames;
        private ImmutableDictionary<string, float> _copyingBlendShapeValues;

        internal MissingBlendShapeInserterProcess(IEnumerable<AnimationClip> clips, string path, SkinnedMeshRenderer valueSource)
        {
            _clips = clips.ToImmutableList();
            _targetPath = path;
            _valueSource = valueSource;

            _targetBlendShapeNames = CalculateTargetBlendShapeNamesUnion();
            _copyingBlendShapeValues = FetchBlendShapeValues();
        }

        internal ImmutableSortedSet<string> CalculateMissingBlendShapeNamesFor(AnimationClip clip)
        {
            if (!_clips.Contains(clip)) throw new ArgumentException($"unintended clip specified");

            var existingBlendShapeNames = AnimationUtility.GetCurveBindings(clip)
                .Where(IsTargetCurveBinding)
                .Select((cb) => cb.propertyName.Substring(11));
            return _targetBlendShapeNames.Except(existingBlendShapeNames);
        }

        internal float GetCopyingBlendShapeValue(string blendShapeName) =>
            _copyingBlendShapeValues.TryGetValue(blendShapeName, out var value) ? value : 0.0f;

        private ImmutableSortedSet<string> CalculateTargetBlendShapeNamesUnion()
        {
            var curveBindings = _clips
                .SelectMany(AnimationUtility.GetCurveBindings)
                .Where(IsTargetCurveBinding);
            return curveBindings
                .Select((cb) => cb.propertyName.Substring(11))
                .ToImmutableSortedSet();
        }

        private bool IsTargetCurveBinding(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer)
                && binding.path == _targetPath
                && binding.propertyName.StartsWith("blendShape.");
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
    }
}
