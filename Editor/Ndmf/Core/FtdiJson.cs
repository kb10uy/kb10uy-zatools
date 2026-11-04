#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using TargetMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorTargetMode;
using CombineMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorCombineMode;
using VisemeDampenMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.VisemeDampenMode;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class FtdiJson
    {
        internal const int CurrentVersion = 1;

        private static readonly JsonSerializerSettings ParseSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { new StringEnumConverter { AllowIntegerValues = false } },
        };

        internal static Ftdi.Definition Parse(string text, List<FtdiDiagnostic> diagnostics)
        {
            RootDto root;
            try
            {
                root = JsonConvert.DeserializeObject<RootDto>(text ?? "", ParseSettings);
            }
            catch (JsonException e)
            {
                diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.InvalidJson, e.Message));
                return null;
            }

            if (root == null)
            {
                diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.RootNotObject));
                return null;
            }
            if (root.Version != CurrentVersion)
            {
                diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.UnsupportedVersion, root.Version.ToString()));
                return null;
            }

            return new DefinitionBuilder(diagnostics).Build(root);
        }

        internal static string Write(Ftdi.Definition definition)
        {
            var root = new JObject { ["Version"] = CurrentVersion };
            if (definition.UseAllTrackingKeys.HasValue) root["UseAllTrackingKeys"] = definition.UseAllTrackingKeys.Value;
            if (definition.VisemeDampen.HasValue)
            {
                root["VisemeDampen"] = new JObject
                {
                    ["Mode"] = definition.VisemeDampen.Value.Mode.ToString(),
                    ["Amount"] = definition.VisemeDampen.Value.Amount,
                };
            }

            root["BlendShapeSets"] = new JArray(definition.BlendShapeSets.Select(WriteSet));
            root["ParameterBehaviors"] = new JArray(definition.ParameterBehaviors.Select(WriteBehavior));

            using (var stringWriter = new StringWriter { NewLine = "\n" })
            {
                using (var writer = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented, Indentation = 2, CloseOutput = false })
                {
                    root.WriteTo(writer);
                }
                stringWriter.Write("\n");
                return stringWriter.ToString();
            }
        }

        private static JObject WriteSet(Ftdi.BlendShapeSet set)
        {
            var written = new JObject { ["Name"] = set.Name };
            if (set.DefaultWeight.HasValue) written["DefaultWeight"] = set.DefaultWeight.Value;
            written["TrackingKeys"] = WriteMappings(set.TrackingKeys, Ftdi.TrackingKeyNames);
            if (set.Visemes.Count > 0) written["Visemes"] = WriteMappings(set.Visemes, Ftdi.VisemeNames);
            return written;
        }

        private static JObject WriteMappings(IEnumerable<Ftdi.ShapeMapping> mappings, ImmutableArray<string> names)
        {
            var written = new JObject();
            foreach (var mapping in mappings)
            {
                written[names[mapping.Index]] = mapping.Multiplier == 1.0f
                    ? (JToken)mapping.Shape
                    : new JObject { ["Shape"] = mapping.Shape, ["Multiplier"] = mapping.Multiplier };
            }
            return written;
        }

        private static JObject WriteBehavior(Ftdi.ParameterBehavior behavior)
        {
            var written = new JObject
            {
                ["Operation"] = behavior.Operation.ToString(),
                ["Target"] = behavior.Target,
            };
            if (behavior.Weight != null) written["Weight"] = behavior.Weight;

            var expression = new JObject { ["Type"] = behavior.Combine.ToString() };
            if (behavior.Combine == CombineMode.Custom)
            {
                expression["Expression"] = behavior.CustomExpression ?? "";
            }
            else
            {
                expression["TrackingKeys"] = new JArray(behavior.Inputs.Select((i) => i.Invert
                    ? new JObject { ["Key"] = Ftdi.InputName(i.Index), ["Invert"] = true }
                    : (JToken)Ftdi.InputName(i.Index)));
            }
            written["Expression"] = expression;
            return written;
        }

        private sealed class DefinitionBuilder
        {
            private readonly List<FtdiDiagnostic> _diagnostics;

            internal DefinitionBuilder(List<FtdiDiagnostic> diagnostics)
            {
                _diagnostics = diagnostics;
            }

            internal Ftdi.Definition Build(RootDto root)
            {
                var definition = new Ftdi.Definition { UseAllTrackingKeys = root.UseAllTrackingKeys };
                if (root.VisemeDampen != null) definition.VisemeDampen = new Ftdi.VisemeDampen(root.VisemeDampen.Mode, root.VisemeDampen.Amount);
                definition.BlendShapeSets.AddRange(root.BlendShapeSets.Where((s) => s != null).Select(BuildSet));
                definition.ParameterBehaviors.AddRange(root.ParameterBehaviors.Where((b) => b != null).Select(BuildBehavior));
                return definition;
            }

            private Ftdi.BlendShapeSet BuildSet(SetDto dto)
            {
                CheckName(dto.Name, "Name", Ftdi.MaxSetNameLength);

                var set = new Ftdi.BlendShapeSet { Name = dto.Name, DefaultWeight = dto.DefaultWeight };
                BuildMappings(dto.TrackingKeys, Ftdi.TrackingKeyIndices, FtdiDiagnosticCode.UnknownTrackingKey, set.TrackingKeys);
                BuildMappings(dto.Visemes, Ftdi.VisemeIndices, FtdiDiagnosticCode.UnknownViseme, set.Visemes);
                return set;
            }

            private void BuildMappings(
                Dictionary<string, MappingDto> source,
                ImmutableDictionary<string, int> indices,
                FtdiDiagnosticCode unknownCode,
                List<Ftdi.ShapeMapping> mappings)
            {
                foreach (var pair in source)
                {
                    CheckName(pair.Value.Shape, "Shape", int.MaxValue);
                    if (indices.TryGetValue(pair.Key, out var index))
                    {
                        mappings.Add(new Ftdi.ShapeMapping(index, pair.Value.Shape, pair.Value.Multiplier));
                    }
                    else
                    {
                        _diagnostics.Add(new FtdiDiagnostic(unknownCode, pair.Key));
                    }
                }
            }

            private Ftdi.ParameterBehavior BuildBehavior(BehaviorDto dto)
            {
                CheckName(dto.Target, "Target", dto.Operation == TargetMode.ForwardToAnimator ? Ftdi.MaxParameterNameLength : int.MaxValue);
                if (dto.Weight != null) CheckName(dto.Weight, "Weight", Ftdi.MaxSetNameLength);

                var expression = dto.Expression;
                var behavior = new Ftdi.ParameterBehavior { Operation = dto.Operation, Target = dto.Target, Weight = dto.Weight, Combine = expression.Type };
                if (expression.Type == CombineMode.Custom)
                {
                    CheckName(expression.Expression, "Expression", Ftdi.MaxExpressionLength);
                    behavior.CustomExpression = expression.Expression;
                }
                else if (expression.TrackingKeys.Count == 0)
                {
                    _diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.NoInputs));
                }
                else if (expression.TrackingKeys.Count > Ftdi.MaxInputs)
                {
                    _diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.TooManyInputs, Ftdi.MaxInputs.ToString()));
                }
                else
                {
                    foreach (var input in expression.TrackingKeys)
                    {
                        if (Ftdi.InputIndices.TryGetValue(input.Key, out var index))
                        {
                            behavior.Inputs.Add(new Ftdi.BehaviorInput(index, input.Invert));
                        }
                        else
                        {
                            _diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.UnknownInputKey, input.Key));
                        }
                    }
                }
                return behavior;
            }

            private void CheckName(string value, string name, int maxLength)
            {
                if (string.IsNullOrEmpty(value))
                {
                    _diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.EmptyValue, name));
                }
                else if (value.Length > maxLength)
                {
                    _diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.ValueTooLong, name, maxLength.ToString()));
                }
            }
        }

        private sealed class RootDto
        {
            [JsonProperty("$schema")] public string Schema;
            [JsonProperty(Required = Required.Always)] public int Version;
            public bool? UseAllTrackingKeys;
            public VisemeDampenDto VisemeDampen;
            public List<SetDto> BlendShapeSets = new List<SetDto>();
            public List<BehaviorDto> ParameterBehaviors = new List<BehaviorDto>();
        }

        private sealed class VisemeDampenDto
        {
            [JsonProperty(Required = Required.Always)] public VisemeDampenMode Mode;
            [JsonProperty(Required = Required.Always)] public float Amount;
        }

        private sealed class SetDto
        {
            [JsonProperty(Required = Required.Always)] public string Name;
            public float? DefaultWeight;
            public Dictionary<string, MappingDto> TrackingKeys = new Dictionary<string, MappingDto>();
            public Dictionary<string, MappingDto> Visemes = new Dictionary<string, MappingDto>();
        }

        [JsonConverter(typeof(ShorthandConverter<MappingDto>))]
        private sealed class MappingDto : IShorthand
        {
            [JsonProperty(Required = Required.Always)] public string Shape;
            public float Multiplier = 1.0f;

            void IShorthand.Assign(string value) => Shape = value;
        }

        private sealed class BehaviorDto
        {
            [JsonProperty(Required = Required.Always)] public TargetMode Operation;
            [JsonProperty(Required = Required.Always)] public string Target;
            public string Weight;
            [JsonProperty(Required = Required.Always)] public ExpressionDto Expression;
        }

        private sealed class ExpressionDto
        {
            [JsonProperty(Required = Required.Always)] public CombineMode Type;
            public List<InputDto> TrackingKeys = new List<InputDto>();
            public string Expression;
        }

        [JsonConverter(typeof(ShorthandConverter<InputDto>))]
        private sealed class InputDto : IShorthand
        {
            [JsonProperty(Required = Required.Always)] public string Key;
            public bool Invert;

            void IShorthand.Assign(string value) => Key = value;
        }

        private interface IShorthand
        {
            void Assign(string value);
        }

        private sealed class ShorthandConverter<T> : JsonConverter<T> where T : IShorthand, new()
        {
            public override bool CanWrite => false;

            public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var value = new T();
                if (reader.TokenType == JsonToken.String) value.Assign((string)reader.Value);
                else serializer.Populate(reader, value);
                return value;
            }

            public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
    }
}
#endif
