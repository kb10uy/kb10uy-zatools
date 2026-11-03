#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using UnityObject = UnityEngine.Object;
using FaceTrackingKey = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingKey;
using TargetMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorTargetMode;
using CombineMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorCombineMode;
using VisemeDampenMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.VisemeDampenMode;
using Viseme = VRC.SDKBase.VRC_AvatarDescriptor.Viseme;
using SdkBlendShapeSet = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingBlendshapesSet;
using SdkParameterBehavior = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingParameterBehavior;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class Ftdi
    {
        internal const int TrackingKeyCount = (int)FaceTrackingKey.Max;
        internal const int VisemeCount = (int)Viseme.Count;
        internal const int VoiceInputIndex = (int)FaceTrackingKey.Max;
        internal const string VoiceInputName = "Voice";
        internal const int MaxMeshes = 16;
        internal const int MaxBlendShapeSets = 128;
        internal const int MaxParameterBehaviors = 128;
        internal const int MaxInputs = 32;
        internal const int MaxExpressionLength = 1024;
        internal const int MaxSetNameLength = 100;
        internal const int MaxParameterNameLength = 64;

        internal static readonly ImmutableArray<string> TrackingKeyNames = Enumerable.Range(0, TrackingKeyCount)
            .Select((i) => ((FaceTrackingKey)i).ToString())
            .ToImmutableArray();
        internal static readonly ImmutableArray<string> VisemeNames = Enumerable.Range(0, VisemeCount)
            .Select((i) => ((Viseme)i).ToString())
            .ToImmutableArray();
        internal static readonly ImmutableDictionary<string, int> TrackingKeyIndices = TrackingKeyNames
            .Select((name, index) => (name, index))
            .ToImmutableDictionary((p) => p.name, (p) => p.index, StringComparer.Ordinal);
        internal static readonly ImmutableDictionary<string, int> VisemeIndices = VisemeNames
            .Select((name, index) => (name, index))
            .ToImmutableDictionary((p) => p.name, (p) => p.index, StringComparer.Ordinal);
        internal static readonly ImmutableDictionary<string, int> InputIndices = TrackingKeyIndices
            .Add(VoiceInputName, VoiceInputIndex)
            .AddRange(Enumerable.Range(0, VisemeCount).Select((i) => new KeyValuePair<string, int>(
                VRCAvatarDescriptor.GetFaceTrackingVisemeInputName((Viseme)i),
                VRCAvatarDescriptor.FaceTrackingVisemeInputStart + i)));

        internal static string InputName(int index)
        {
            if (index >= 0 && index < TrackingKeyCount) return TrackingKeyNames[index];
            if (index == VoiceInputIndex) return VoiceInputName;
            var visemeIndex = index - VRCAvatarDescriptor.FaceTrackingVisemeInputStart;
            if (visemeIndex >= 0 && visemeIndex < VisemeCount) return VRCAvatarDescriptor.GetFaceTrackingVisemeInputName((Viseme)visemeIndex);
            return null;
        }

        internal static bool IsBasicInput(int index)
        {
            return index >= TrackingKeyCount || VRCAvatarDescriptor.IsBasicFaceTrackingKey(index);
        }

        internal static bool HasExistingConfiguration(VRCAvatarDescriptor descriptor)
        {
            return (descriptor.faceTrackingMeshes?.Any((m) => m != null) ?? false)
                || (descriptor.faceTrackingBlendshapesSetList?.Count ?? 0) > 0
                || (descriptor.faceTrackingParameterBehaviors?.Count ?? 0) > 0;
        }

        internal static Configuration Merge(IEnumerable<Source> sources, bool fallbackUseAllTrackingKeys, List<FtdiDiagnostic> diagnostics)
        {
            var merger = new Merger(diagnostics);
            foreach (var source in sources) merger.Add(source);
            return merger.Finish(fallbackUseAllTrackingKeys);
        }

        internal static void Apply(VRCAvatarDescriptor descriptor, Configuration configuration)
        {
            descriptor.enableFaceTracking = true;
            if (configuration.UseAllTrackingKeys.HasValue) descriptor.useAllTrackingKeys = configuration.UseAllTrackingKeys.Value;
            if (configuration.VisemeDampen.HasValue)
            {
                descriptor.visemeDampenMode = configuration.VisemeDampen.Value.Mode;
                descriptor.visemeDampenAmount = configuration.VisemeDampen.Value.Amount;
            }
            descriptor.faceTrackingMeshes = configuration.Meshes.ToList();
            descriptor.faceTrackingBlendshapesSetList = configuration.BlendShapeSets.ToList();
            descriptor.faceTrackingParameterBehaviors = configuration.ParameterBehaviors.ToList();
        }

        internal static Definition Export(VRCAvatarDescriptor descriptor, List<FtdiDiagnostic> diagnostics)
        {
            return Export(
                descriptor.faceTrackingBlendshapesSetList ?? new List<SdkBlendShapeSet>(),
                descriptor.faceTrackingParameterBehaviors ?? new List<SdkParameterBehavior>(),
                descriptor.useAllTrackingKeys,
                new VisemeDampen(descriptor.visemeDampenMode, descriptor.visemeDampenAmount),
                diagnostics);
        }

        internal static Definition Export(
            IEnumerable<SdkBlendShapeSet> sets,
            IEnumerable<SdkParameterBehavior> behaviors,
            bool useAllTrackingKeys,
            VisemeDampen visemeDampen,
            List<FtdiDiagnostic> diagnostics)
        {
            var definition = new Definition
            {
                UseAllTrackingKeys = useAllTrackingKeys,
                VisemeDampen = visemeDampen,
            };

            foreach (var set in sets.Where((s) => s != null))
            {
                var exported = new BlendShapeSet { Name = set.setName ?? "", DefaultWeight = set.setWeight };
                exported.TrackingKeys.AddRange(ExportMappings(set.faceTrackingBlendshapeNames, set.faceTrackingBlendshapeMultipliers, TrackingKeyCount));
                exported.Visemes.AddRange(ExportMappings(set.visemeBlendshapeNames, set.visemeBlendshapeMultipliers, VisemeCount));
                definition.BlendShapeSets.Add(exported);
            }

            foreach (var behavior in behaviors.Where((b) => b != null))
            {
                var exported = new ParameterBehavior
                {
                    Operation = behavior.targetMode,
                    Target = TargetOf(behavior) ?? "",
                    Weight = string.IsNullOrEmpty(behavior.weightBlendSetName) ? null : behavior.weightBlendSetName,
                    Combine = behavior.combineMode,
                };
                if (behavior.combineMode == CombineMode.Custom)
                {
                    exported.CustomExpression = behavior.customExpression ?? "";
                }
                else
                {
                    exported.Inputs.AddRange(ExportInputs(behavior, diagnostics));
                }
                definition.ParameterBehaviors.Add(exported);
            }

            return definition;
        }

        internal static string TargetOf(TargetMode operation, string outputParameterName, string targetBlendSetName, string targetBlendshapeName)
        {
            switch (operation)
            {
                case TargetMode.ForwardToAnimator: return outputParameterName;
                case TargetMode.ApplyBlendSetWeight: return targetBlendSetName;
                default: return targetBlendshapeName;
            }
        }

        private static string TargetOf(SdkParameterBehavior behavior)
        {
            return TargetOf(behavior.targetMode, behavior.outputParameterName, behavior.targetBlendSetName, behavior.targetBlendshapeName);
        }

        internal static bool HasExclusiveOutput(TargetMode operation)
        {
            return operation == TargetMode.ForwardToAnimator
                || operation == TargetMode.ApplyBlendSetWeight
                || operation == TargetMode.ApplyToBlendshape;
        }

        internal static string Suggest(string value, IEnumerable<string> candidates)
        {
            string best = null;
            var bestDistance = Math.Max(2, value.Length / 3) + 1;
            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)) return candidate;
                var distance = EditDistance(value, candidate);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static int EditDistance(string lhs, string rhs)
        {
            var previous = new int[rhs.Length + 1];
            var current = new int[rhs.Length + 1];
            for (var j = 0; j <= rhs.Length; ++j) previous[j] = j;
            for (var i = 1; i <= lhs.Length; ++i)
            {
                current[0] = i;
                for (var j = 1; j <= rhs.Length; ++j)
                {
                    var substitution = previous[j - 1] + (lhs[i - 1] == rhs[j - 1] ? 0 : 1);
                    current[j] = Math.Min(substitution, Math.Min(previous[j], current[j - 1]) + 1);
                }
                (previous, current) = (current, previous);
            }
            return previous[rhs.Length];
        }

        private static IEnumerable<ShapeMapping> ExportMappings(string[] names, float[] multipliers, int count)
        {
            if (names == null) yield break;
            for (var i = 0; i < Math.Min(names.Length, count); ++i)
            {
                if (string.IsNullOrEmpty(names[i])) continue;
                var multiplier = multipliers != null && i < multipliers.Length ? multipliers[i] : 1.0f;
                yield return new ShapeMapping(i, names[i], multiplier, SourceLocation.None);
            }
        }

        private static IEnumerable<BehaviorInput> ExportInputs(SdkParameterBehavior behavior, List<FtdiDiagnostic> diagnostics)
        {
            var keys = behavior.inputTrackingKeys ?? Array.Empty<int>();
            var names = behavior.inputTrackingKeyNames ?? Array.Empty<string>();
            var inverted = behavior.inputInverted ?? Array.Empty<bool>();
            for (var i = 0; i < keys.Length; ++i)
            {
                var index = keys[i];
                if (InputName(index) == null)
                {
                    var name = i < names.Length ? names[i] : null;
                    if (string.IsNullOrEmpty(name) || !InputIndices.TryGetValue(name, out index))
                    {
                        var value = string.IsNullOrEmpty(name) ? keys[i].ToString() : name;
                        diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.UnknownInputKey, SourceLocation.None, value)
                            .WithSuggestion(string.IsNullOrEmpty(name) ? null : Suggest(name, InputIndices.Keys)));
                        continue;
                    }
                }
                yield return new BehaviorInput(index, i < inverted.Length && inverted[i], SourceLocation.None);
            }
        }

        internal readonly struct SourceLocation
        {
            internal static readonly SourceLocation None = new SourceLocation(0, 0);

            internal readonly int Line;
            internal readonly int Column;

            internal SourceLocation(int line, int column)
            {
                Line = line;
                Column = column;
            }

            internal bool IsKnown => Line > 0;
        }

        internal readonly struct VisemeDampen : IEquatable<VisemeDampen>
        {
            internal readonly VisemeDampenMode Mode;
            internal readonly float Amount;

            internal VisemeDampen(VisemeDampenMode mode, float amount)
            {
                Mode = mode;
                Amount = amount;
            }

            public bool Equals(VisemeDampen other) => Mode == other.Mode && Amount == other.Amount;
            public override bool Equals(object obj) => obj is VisemeDampen other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Mode, Amount);
        }

        internal readonly struct ShapeMapping
        {
            internal readonly int Index;
            internal readonly string Shape;
            internal readonly float Multiplier;
            internal readonly SourceLocation Location;

            internal ShapeMapping(int index, string shape, float multiplier, SourceLocation location)
            {
                Index = index;
                Shape = shape;
                Multiplier = multiplier;
                Location = location;
            }
        }

        internal readonly struct BehaviorInput
        {
            internal readonly int Index;
            internal readonly bool Invert;
            internal readonly SourceLocation Location;

            internal BehaviorInput(int index, bool invert, SourceLocation location)
            {
                Index = index;
                Invert = invert;
                Location = location;
            }
        }

        internal sealed class Definition
        {
            internal bool? UseAllTrackingKeys;
            internal SourceLocation UseAllTrackingKeysLocation;
            internal VisemeDampen? VisemeDampen;
            internal SourceLocation VisemeDampenLocation;
            internal readonly List<BlendShapeSet> BlendShapeSets = new List<BlendShapeSet>();
            internal readonly List<ParameterBehavior> ParameterBehaviors = new List<ParameterBehavior>();
        }

        internal sealed class BlendShapeSet
        {
            internal string Name;
            internal float? DefaultWeight;
            internal SourceLocation Location;
            internal readonly List<ShapeMapping> TrackingKeys = new List<ShapeMapping>();
            internal readonly List<ShapeMapping> Visemes = new List<ShapeMapping>();
        }

        internal sealed class ParameterBehavior
        {
            internal TargetMode Operation;
            internal string Target;
            internal string Weight;
            internal CombineMode Combine;
            internal string CustomExpression;
            internal SourceLocation Location;
            internal SourceLocation TargetLocation;
            internal SourceLocation WeightLocation;
            internal readonly List<BehaviorInput> Inputs = new List<BehaviorInput>();
        }

        internal sealed class Source
        {
            internal UnityObject Context;
            internal Definition Definition;
            internal IReadOnlyList<SkinnedMeshRenderer> Meshes;
        }

        internal sealed class Configuration
        {
            internal bool? UseAllTrackingKeys;
            internal VisemeDampen? VisemeDampen;
            internal ImmutableArray<SkinnedMeshRenderer> Meshes;
            internal ImmutableArray<SdkBlendShapeSet> BlendShapeSets;
            internal ImmutableArray<SdkParameterBehavior> ParameterBehaviors;
        }

        private sealed class Merger
        {
            private readonly List<FtdiDiagnostic> _diagnostics;
            private readonly List<SkinnedMeshRenderer> _meshes = new List<SkinnedMeshRenderer>();
            private readonly HashSet<SkinnedMeshRenderer> _meshSet = new HashSet<SkinnedMeshRenderer>();
            private readonly List<MergedSet> _sets = new List<MergedSet>();
            private readonly Dictionary<string, MergedSet> _setsByName = new Dictionary<string, MergedSet>(StringComparer.Ordinal);
            private readonly List<(UnityObject Context, ParameterBehavior Behavior)> _behaviors = new List<(UnityObject, ParameterBehavior)>();
            private readonly Dictionary<(TargetMode, string), UnityObject> _outputs = new Dictionary<(TargetMode, string), UnityObject>();
            private (bool Value, UnityObject Context)? _useAllTrackingKeys;
            private (VisemeDampen Value, UnityObject Context)? _visemeDampen;

            internal Merger(List<FtdiDiagnostic> diagnostics)
            {
                _diagnostics = diagnostics;
            }

            internal void Add(Source source)
            {
                var context = source.Context;
                var definition = source.Definition;

                if (definition.UseAllTrackingKeys.HasValue)
                {
                    var value = definition.UseAllTrackingKeys.Value;
                    if (!_useAllTrackingKeys.HasValue)
                    {
                        _useAllTrackingKeys = (value, context);
                    }
                    else if (_useAllTrackingKeys.Value.Value != value)
                    {
                        Report(FtdiDiagnosticCode.UseAllTrackingKeysConflict, definition.UseAllTrackingKeysLocation, context, ContextName(_useAllTrackingKeys.Value.Context));
                    }
                }

                if (definition.VisemeDampen.HasValue)
                {
                    var value = definition.VisemeDampen.Value;
                    if (!_visemeDampen.HasValue)
                    {
                        _visemeDampen = (value, context);
                    }
                    else if (!_visemeDampen.Value.Value.Equals(value))
                    {
                        Report(FtdiDiagnosticCode.VisemeDampenConflict, definition.VisemeDampenLocation, context, ContextName(_visemeDampen.Value.Context));
                    }
                }

                foreach (var mesh in source.Meshes ?? Array.Empty<SkinnedMeshRenderer>())
                {
                    if (mesh != null && _meshSet.Add(mesh)) _meshes.Add(mesh);
                }

                foreach (var set in definition.BlendShapeSets) AddSet(set, context);

                foreach (var behavior in definition.ParameterBehaviors)
                {
                    if (HasExclusiveOutput(behavior.Operation))
                    {
                        var key = (behavior.Operation, behavior.Target);
                        if (_outputs.TryGetValue(key, out var first))
                        {
                            Report(FtdiDiagnosticCode.OutputConflict, behavior.TargetLocation, context, behavior.Operation.ToString(), behavior.Target, ContextName(first));
                        }
                        else
                        {
                            _outputs.Add(key, context);
                        }
                    }
                    _behaviors.Add((context, behavior));
                }
            }

            internal Configuration Finish(bool fallbackUseAllTrackingKeys)
            {
                var useAllTrackingKeys = _useAllTrackingKeys?.Value ?? fallbackUseAllTrackingKeys;
                if (!useAllTrackingKeys) CheckNonBasicKeys();

                foreach (var (context, behavior) in _behaviors)
                {
                    if (behavior.Operation == TargetMode.ApplyBlendSetWeight && !_setsByName.ContainsKey(behavior.Target))
                    {
                        Report(FtdiDiagnosticCode.UnknownBlendShapeSet, behavior.TargetLocation, context, behavior.Target);
                    }
                    if (behavior.Weight != null && !_setsByName.ContainsKey(behavior.Weight))
                    {
                        Report(FtdiDiagnosticCode.UnknownBlendShapeSet, behavior.WeightLocation, context, behavior.Weight);
                    }
                }

                if (_meshes.Count > MaxMeshes) Report(FtdiDiagnosticCode.TooManyMeshes, SourceLocation.None, null, MaxMeshes.ToString());
                if (_sets.Count > MaxBlendShapeSets) Report(FtdiDiagnosticCode.TooManyBlendShapeSets, SourceLocation.None, null, MaxBlendShapeSets.ToString());
                if (_behaviors.Count > MaxParameterBehaviors) Report(FtdiDiagnosticCode.TooManyParameterBehaviors, SourceLocation.None, null, MaxParameterBehaviors.ToString());

                return new Configuration
                {
                    UseAllTrackingKeys = _useAllTrackingKeys?.Value,
                    VisemeDampen = _visemeDampen?.Value,
                    Meshes = _meshes.ToImmutableArray(),
                    BlendShapeSets = _sets.Select((s) => s.ToSdk()).ToImmutableArray(),
                    ParameterBehaviors = _behaviors.Select((b) => ToSdk(b.Behavior)).ToImmutableArray(),
                };
            }

            private void AddSet(BlendShapeSet set, UnityObject context)
            {
                if (!_setsByName.TryGetValue(set.Name, out var merged))
                {
                    merged = new MergedSet(set.Name);
                    _sets.Add(merged);
                    _setsByName.Add(set.Name, merged);
                }

                if (set.DefaultWeight.HasValue)
                {
                    var weight = set.DefaultWeight.Value;
                    if (!merged.Weight.HasValue)
                    {
                        merged.Weight = (weight, context);
                    }
                    else if (merged.Weight.Value.Value != weight)
                    {
                        Report(FtdiDiagnosticCode.DefaultWeightConflict, set.Location, context, set.Name, ContextName(merged.Weight.Value.Context));
                    }
                }

                foreach (var mapping in set.TrackingKeys)
                {
                    if (!merged.TrackingKeys[mapping.Index].TryAssign(mapping, context, out var other))
                    {
                        Report(FtdiDiagnosticCode.TrackingKeyConflict, mapping.Location, context, set.Name, TrackingKeyNames[mapping.Index], ContextName(other));
                    }
                }
                foreach (var mapping in set.Visemes)
                {
                    if (!merged.Visemes[mapping.Index].TryAssign(mapping, context, out var other))
                    {
                        Report(FtdiDiagnosticCode.VisemeConflict, mapping.Location, context, set.Name, VisemeNames[mapping.Index], ContextName(other));
                    }
                }
            }

            private void CheckNonBasicKeys()
            {
                foreach (var set in _sets)
                {
                    for (var i = 0; i < TrackingKeyCount; ++i)
                    {
                        var slot = set.TrackingKeys[i];
                        if (slot.Mapping.HasValue && !IsBasicInput(i))
                        {
                            Report(FtdiDiagnosticCode.NonBasicTrackingKey, slot.Mapping.Value.Location, slot.Context, TrackingKeyNames[i]);
                        }
                    }
                }
                foreach (var (context, behavior) in _behaviors)
                {
                    foreach (var input in behavior.Inputs.Where((i) => !IsBasicInput(i.Index)))
                    {
                        Report(FtdiDiagnosticCode.NonBasicTrackingKey, input.Location, context, InputName(input.Index));
                    }
                }
            }

            private void Report(FtdiDiagnosticCode code, SourceLocation location, UnityObject context, params string[] arguments)
            {
                _diagnostics.Add(new FtdiDiagnostic(code, location, arguments).WithContext(context));
            }

            private static SdkParameterBehavior ToSdk(ParameterBehavior behavior)
            {
                var custom = behavior.Combine == CombineMode.Custom;
                return new SdkParameterBehavior
                {
                    targetMode = behavior.Operation,
                    outputParameterName = behavior.Operation == TargetMode.ForwardToAnimator ? behavior.Target : "",
                    targetBlendSetName = behavior.Operation == TargetMode.ApplyBlendSetWeight ? behavior.Target : "",
                    targetBlendshapeName = behavior.Operation != TargetMode.ForwardToAnimator && behavior.Operation != TargetMode.ApplyBlendSetWeight ? behavior.Target : "",
                    weightBlendSetName = behavior.Weight ?? "",
                    inputTrackingKeys = custom ? Array.Empty<int>() : behavior.Inputs.Select((i) => i.Index).ToArray(),
                    inputTrackingKeyNames = custom ? Array.Empty<string>() : behavior.Inputs.Select((i) => InputName(i.Index)).ToArray(),
                    inputInverted = custom ? Array.Empty<bool>() : behavior.Inputs.Select((i) => i.Invert).ToArray(),
                    combineMode = behavior.Combine,
                    customExpression = custom ? behavior.CustomExpression : "",
                };
            }

            private static string ContextName(UnityObject context)
            {
                if (context is Component component) return component.gameObject.name;
                return context != null ? context.name : "?";
            }
        }

        private struct MappingSlot
        {
            internal ShapeMapping? Mapping;
            internal UnityObject Context;

            internal bool TryAssign(ShapeMapping mapping, UnityObject context, out UnityObject conflicting)
            {
                conflicting = Context;
                if (!Mapping.HasValue)
                {
                    Mapping = mapping;
                    Context = context;
                    return true;
                }
                return Mapping.Value.Shape == mapping.Shape && Mapping.Value.Multiplier == mapping.Multiplier;
            }
        }

        private sealed class MergedSet
        {
            internal readonly string Name;
            internal (float Value, UnityObject Context)? Weight;
            internal readonly MappingSlot[] TrackingKeys = new MappingSlot[TrackingKeyCount];
            internal readonly MappingSlot[] Visemes = new MappingSlot[VisemeCount];

            internal MergedSet(string name)
            {
                Name = name;
            }

            internal SdkBlendShapeSet ToSdk()
            {
                return new SdkBlendShapeSet
                {
                    setName = Name,
                    setWeight = Weight?.Value ?? 1.0f,
                    faceTrackingBlendshapeNames = TrackingKeys.Select((s) => s.Mapping?.Shape ?? "").ToArray(),
                    faceTrackingBlendshapeMultipliers = TrackingKeys.Select((s) => s.Mapping?.Multiplier ?? 1.0f).ToArray(),
                    visemeBlendshapeNames = Visemes.Select((s) => s.Mapping?.Shape ?? "").ToArray(),
                    visemeBlendshapeMultipliers = Visemes.Select((s) => s.Mapping?.Multiplier ?? 1.0f).ToArray(),
                };
            }
        }
    }

    internal enum FtdiDiagnosticCode
    {
        SyntaxError,
        RootNotObject,
        UnsupportedVersion,
        UnknownProperty,
        MissingProperty,
        TypeMismatch,
        InvalidEnumValue,
        EmptyValue,
        ValueTooLong,
        UnknownTrackingKey,
        UnknownViseme,
        UnknownInputKey,
        NoInputs,
        TooManyInputs,
        DuplicateBlendShapeSet,
        DuplicateOutput,
        OutputConflict,
        TrackingKeyConflict,
        VisemeConflict,
        DefaultWeightConflict,
        UseAllTrackingKeysConflict,
        VisemeDampenConflict,
        UnknownBlendShapeSet,
        NonBasicTrackingKey,
        TooManyMeshes,
        TooManyBlendShapeSets,
        TooManyParameterBehaviors,
    }

    internal readonly struct FtdiDiagnostic
    {
        internal readonly FtdiDiagnosticCode Code;
        internal readonly Ftdi.SourceLocation Location;
        internal readonly string[] Arguments;
        internal readonly string Suggestion;
        internal readonly UnityObject Context;

        internal FtdiDiagnostic(FtdiDiagnosticCode code, Ftdi.SourceLocation location, params string[] arguments)
            : this(code, location, arguments ?? Array.Empty<string>(), null, null)
        {
        }

        private FtdiDiagnostic(FtdiDiagnosticCode code, Ftdi.SourceLocation location, string[] arguments, string suggestion, UnityObject context)
        {
            Code = code;
            Location = location;
            Arguments = arguments;
            Suggestion = suggestion;
            Context = context;
        }

        internal FtdiDiagnostic WithSuggestion(string suggestion) => new FtdiDiagnostic(Code, Location, Arguments, suggestion, Context);
        internal FtdiDiagnostic WithContext(UnityObject context) => new FtdiDiagnostic(Code, Location, Arguments, Suggestion, context);

        internal string LocalizationKey
        {
            get
            {
                var name = Code.ToString();
                var builder = new StringBuilder("ftdi.diagnostic.");
                for (var i = 0; i < name.Length; ++i)
                {
                    if (char.IsUpper(name[i]) && i > 0) builder.Append('-');
                    builder.Append(char.ToLowerInvariant(name[i]));
                }
                return builder.ToString();
            }
        }
    }
}
#endif
