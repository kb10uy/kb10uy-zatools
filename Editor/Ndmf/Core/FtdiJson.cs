#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TargetMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorTargetMode;
using CombineMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorCombineMode;
using VisemeDampenMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.VisemeDampenMode;

namespace KusakaFactory.Zatools.Ndmf.Core
{
    internal static class FtdiJson
    {
        internal const int CurrentVersion = 1;

        private static readonly ImmutableArray<string> RootProperties = ImmutableArray.Create(
            "$schema", "Version", "UseAllTrackingKeys", "VisemeDampen", "BlendShapeSets", "ParameterBehaviors");
        private static readonly ImmutableArray<string> VisemeDampenProperties = ImmutableArray.Create("Mode", "Amount");
        private static readonly ImmutableArray<string> SetProperties = ImmutableArray.Create("Name", "DefaultWeight", "TrackingKeys", "Visemes");
        private static readonly ImmutableArray<string> MappingProperties = ImmutableArray.Create("Shape", "Multiplier");
        private static readonly ImmutableArray<string> BehaviorProperties = ImmutableArray.Create("Operation", "Target", "Weight", "Expression");
        private static readonly ImmutableArray<string> CombineExpressionProperties = ImmutableArray.Create("Type", "TrackingKeys");
        private static readonly ImmutableArray<string> CustomExpressionProperties = ImmutableArray.Create("Type", "Expression");
        private static readonly ImmutableArray<string> InputProperties = ImmutableArray.Create("Key", "Invert");

        internal static Ftdi.Definition Parse(string text, List<FtdiDiagnostic> diagnostics)
        {
            JToken root;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(text ?? "")))
                {
                    var settings = new JsonLoadSettings
                    {
                        CommentHandling = CommentHandling.Ignore,
                        LineInfoHandling = LineInfoHandling.Load,
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    };
                    root = JToken.ReadFrom(reader, settings);
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.Comment) continue;
                        throw new JsonReaderException("Additional text found after the root value.", reader.Path, reader.LineNumber, reader.LinePosition, null);
                    }
                }
            }
            catch (JsonReaderException e)
            {
                diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.SyntaxError, new Ftdi.SourceLocation(e.LineNumber, e.LinePosition), e.Message));
                return null;
            }

            if (!(root is JObject rootObject))
            {
                diagnostics.Add(new FtdiDiagnostic(FtdiDiagnosticCode.RootNotObject, LocationOf(root)));
                return null;
            }

            var parser = new Parser(diagnostics);
            return parser.ParseRoot(rootObject);
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

        private static Ftdi.SourceLocation LocationOf(JToken token)
        {
            if (token is IJsonLineInfo info && info.HasLineInfo()) return new Ftdi.SourceLocation(info.LineNumber, info.LinePosition);
            return Ftdi.SourceLocation.None;
        }

        private sealed class Parser
        {
            private readonly List<FtdiDiagnostic> _diagnostics;

            internal Parser(List<FtdiDiagnostic> diagnostics)
            {
                _diagnostics = diagnostics;
            }

            internal Ftdi.Definition ParseRoot(JObject root)
            {
                CheckProperties(root, RootProperties);

                if (!TryRequired(root, "Version", out var versionToken)) return null;
                if (!(versionToken is JValue versionValue && versionValue.Value is long version && version == CurrentVersion))
                {
                    Report(FtdiDiagnosticCode.UnsupportedVersion, versionToken, versionToken.ToString(Formatting.None));
                    return null;
                }

                var definition = new Ftdi.Definition();
                if (root.TryGetValue("UseAllTrackingKeys", out var useAllToken) && TryBoolean(useAllToken, "UseAllTrackingKeys", out var useAll))
                {
                    definition.UseAllTrackingKeys = useAll;
                    definition.UseAllTrackingKeysLocation = LocationOf(useAllToken);
                }
                if (root.TryGetValue("VisemeDampen", out var dampenToken) && TryVisemeDampen(dampenToken, out var dampen))
                {
                    definition.VisemeDampen = dampen;
                    definition.VisemeDampenLocation = LocationOf(dampenToken);
                }

                if (root.TryGetValue("BlendShapeSets", out var setsToken) && TryArray(setsToken, "BlendShapeSets", out var sets))
                {
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var setToken in sets)
                    {
                        var set = ParseSet(setToken);
                        if (set == null) continue;
                        if (!names.Add(set.Name))
                        {
                            Report(FtdiDiagnosticCode.DuplicateBlendShapeSet, setToken, set.Name);
                            continue;
                        }
                        definition.BlendShapeSets.Add(set);
                    }
                }

                if (root.TryGetValue("ParameterBehaviors", out var behaviorsToken) && TryArray(behaviorsToken, "ParameterBehaviors", out var behaviors))
                {
                    var outputs = new HashSet<(TargetMode, string)>();
                    foreach (var behaviorToken in behaviors)
                    {
                        var behavior = ParseBehavior(behaviorToken);
                        if (behavior == null) continue;
                        if (Ftdi.HasExclusiveOutput(behavior.Operation) && !outputs.Add((behavior.Operation, behavior.Target)))
                        {
                            _diagnostics.Add(new FtdiDiagnostic(
                                FtdiDiagnosticCode.DuplicateOutput,
                                behavior.TargetLocation,
                                behavior.Operation.ToString(),
                                behavior.Target));
                        }
                        definition.ParameterBehaviors.Add(behavior);
                    }
                }

                return definition;
            }

            private bool TryVisemeDampen(JToken token, out Ftdi.VisemeDampen dampen)
            {
                dampen = default;
                if (!TryObject(token, "VisemeDampen", out var obj)) return false;
                CheckProperties(obj, VisemeDampenProperties);

                var succeeded = TryRequired(obj, "Mode", out var modeToken) & TryRequired(obj, "Amount", out var amountToken);
                if (!succeeded) return false;
                succeeded = TryEnum<VisemeDampenMode>(modeToken, "Mode", out var mode) & TryNumber(amountToken, "Amount", out var amount);
                if (!succeeded) return false;

                dampen = new Ftdi.VisemeDampen(mode, amount);
                return true;
            }

            private Ftdi.BlendShapeSet ParseSet(JToken token)
            {
                if (!TryObject(token, "BlendShapeSets", out var obj)) return null;
                CheckProperties(obj, SetProperties);

                var set = new Ftdi.BlendShapeSet { Location = LocationOf(obj) };
                var succeeded = TryRequired(obj, "Name", out var nameToken) && TryName(nameToken, "Name", Ftdi.MaxSetNameLength, out set.Name);

                if (obj.TryGetValue("DefaultWeight", out var weightToken))
                {
                    if (TryNumber(weightToken, "DefaultWeight", out var weight)) set.DefaultWeight = weight;
                    else succeeded = false;
                }

                if (obj.TryGetValue("TrackingKeys", out var keysToken))
                {
                    succeeded &= TryMappings(keysToken, "TrackingKeys", Ftdi.TrackingKeyIndices, FtdiDiagnosticCode.UnknownTrackingKey, set.TrackingKeys);
                }
                if (obj.TryGetValue("Visemes", out var visemesToken))
                {
                    succeeded &= TryMappings(visemesToken, "Visemes", Ftdi.VisemeIndices, FtdiDiagnosticCode.UnknownViseme, set.Visemes);
                }

                return succeeded ? set : null;
            }

            private bool TryMappings(
                JToken token,
                string name,
                ImmutableDictionary<string, int> indices,
                FtdiDiagnosticCode unknownCode,
                List<Ftdi.ShapeMapping> mappings)
            {
                if (!TryObject(token, name, out var obj)) return false;

                var succeeded = true;
                foreach (var property in obj.Properties())
                {
                    if (!indices.TryGetValue(property.Name, out var index))
                    {
                        ReportUnknown(unknownCode, property, property.Name, indices.Keys);
                        succeeded = false;
                        continue;
                    }

                    if (TryMapping(property.Value, out var shape, out var multiplier))
                    {
                        mappings.Add(new Ftdi.ShapeMapping(index, shape, multiplier, LocationOf(property)));
                    }
                    else
                    {
                        succeeded = false;
                    }
                }
                return succeeded;
            }

            private bool TryMapping(JToken token, out string shape, out float multiplier)
            {
                shape = null;
                multiplier = 1.0f;
                if (token.Type == JTokenType.String) return TryName(token, "Shape", int.MaxValue, out shape);
                if (token.Type != JTokenType.Object)
                {
                    Report(FtdiDiagnosticCode.TypeMismatch, token, "Shape", "string | object");
                    return false;
                }

                var obj = (JObject)token;
                CheckProperties(obj, MappingProperties);
                var succeeded = TryRequired(obj, "Shape", out var shapeToken) && TryName(shapeToken, "Shape", int.MaxValue, out shape);
                if (obj.TryGetValue("Multiplier", out var multiplierToken)) succeeded &= TryNumber(multiplierToken, "Multiplier", out multiplier);
                return succeeded;
            }

            private Ftdi.ParameterBehavior ParseBehavior(JToken token)
            {
                if (!TryObject(token, "ParameterBehaviors", out var obj)) return null;
                CheckProperties(obj, BehaviorProperties);

                var behavior = new Ftdi.ParameterBehavior { Location = LocationOf(obj) };
                var succeeded = TryRequired(obj, "Operation", out var operationToken) && TryEnum(operationToken, "Operation", out behavior.Operation);

                if (TryRequired(obj, "Target", out var targetToken))
                {
                    var maxLength = succeeded && behavior.Operation == TargetMode.ForwardToAnimator ? Ftdi.MaxParameterNameLength : int.MaxValue;
                    succeeded &= TryName(targetToken, "Target", maxLength, out behavior.Target);
                    behavior.TargetLocation = LocationOf(targetToken);
                }
                else
                {
                    succeeded = false;
                }

                if (obj.TryGetValue("Weight", out var weightToken))
                {
                    succeeded &= TryName(weightToken, "Weight", Ftdi.MaxSetNameLength, out behavior.Weight);
                    behavior.WeightLocation = LocationOf(weightToken);
                }

                succeeded &= TryRequired(obj, "Expression", out var expressionToken) && TryExpression(expressionToken, behavior);
                return succeeded ? behavior : null;
            }

            private bool TryExpression(JToken token, Ftdi.ParameterBehavior behavior)
            {
                if (!TryObject(token, "Expression", out var obj)) return false;
                if (!TryRequired(obj, "Type", out var typeToken) || !TryEnum(typeToken, "Type", out behavior.Combine))
                {
                    CheckProperties(obj, CombineExpressionProperties.AddRange(CustomExpressionProperties));
                    return false;
                }

                if (behavior.Combine == CombineMode.Custom)
                {
                    CheckProperties(obj, CustomExpressionProperties);
                    if (!TryRequired(obj, "Expression", out var sourceToken)) return false;
                    if (!TryName(sourceToken, "Expression", Ftdi.MaxExpressionLength, out behavior.CustomExpression)) return false;
                    return true;
                }

                CheckProperties(obj, CombineExpressionProperties);
                if (!TryRequired(obj, "TrackingKeys", out var keysToken) || !TryArray(keysToken, "TrackingKeys", out var keys)) return false;
                if (keys.Count == 0)
                {
                    Report(FtdiDiagnosticCode.NoInputs, keys);
                    return false;
                }
                if (keys.Count > Ftdi.MaxInputs)
                {
                    Report(FtdiDiagnosticCode.TooManyInputs, keys, Ftdi.MaxInputs.ToString());
                    return false;
                }

                var succeeded = true;
                foreach (var keyToken in keys)
                {
                    if (TryInput(keyToken, out var input)) behavior.Inputs.Add(input);
                    else succeeded = false;
                }
                return succeeded;
            }

            private bool TryInput(JToken token, out Ftdi.BehaviorInput input)
            {
                input = default;
                JToken keyToken;
                var invert = false;
                if (token.Type == JTokenType.String)
                {
                    keyToken = token;
                }
                else if (token.Type == JTokenType.Object)
                {
                    var obj = (JObject)token;
                    CheckProperties(obj, InputProperties);
                    if (!TryRequired(obj, "Key", out keyToken)) return false;
                    if (obj.TryGetValue("Invert", out var invertToken) && !TryBoolean(invertToken, "Invert", out invert)) return false;
                }
                else
                {
                    Report(FtdiDiagnosticCode.TypeMismatch, token, "TrackingKeys", "string | object");
                    return false;
                }

                if (!TryString(keyToken, "Key", out var key)) return false;
                if (!Ftdi.InputIndices.TryGetValue(key, out var index))
                {
                    ReportUnknown(FtdiDiagnosticCode.UnknownInputKey, keyToken, key, Ftdi.InputIndices.Keys);
                    return false;
                }

                input = new Ftdi.BehaviorInput(index, invert, LocationOf(keyToken));
                return true;
            }

            private void CheckProperties(JObject obj, ImmutableArray<string> allowed)
            {
                foreach (var property in obj.Properties())
                {
                    if (allowed.Contains(property.Name)) continue;
                    ReportUnknown(FtdiDiagnosticCode.UnknownProperty, property, property.Name, allowed);
                }
            }

            private bool TryRequired(JObject obj, string name, out JToken token)
            {
                if (obj.TryGetValue(name, out token)) return true;
                Report(FtdiDiagnosticCode.MissingProperty, obj, name);
                return false;
            }

            private bool TryObject(JToken token, string name, out JObject obj)
            {
                obj = token as JObject;
                if (obj != null) return true;
                Report(FtdiDiagnosticCode.TypeMismatch, token, name, "object");
                return false;
            }

            private bool TryArray(JToken token, string name, out JArray array)
            {
                array = token as JArray;
                if (array != null) return true;
                Report(FtdiDiagnosticCode.TypeMismatch, token, name, "array");
                return false;
            }

            private bool TryString(JToken token, string name, out string value)
            {
                value = null;
                if (token.Type != JTokenType.String)
                {
                    Report(FtdiDiagnosticCode.TypeMismatch, token, name, "string");
                    return false;
                }
                value = token.Value<string>();
                return true;
            }

            private bool TryName(JToken token, string name, int maxLength, out string value)
            {
                if (!TryString(token, name, out value)) return false;
                if (value.Length == 0)
                {
                    Report(FtdiDiagnosticCode.EmptyValue, token, name);
                    return false;
                }
                if (value.Length > maxLength)
                {
                    Report(FtdiDiagnosticCode.ValueTooLong, token, name, maxLength.ToString());
                    return false;
                }
                return true;
            }

            private bool TryNumber(JToken token, string name, out float value)
            {
                value = 0.0f;
                if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
                {
                    Report(FtdiDiagnosticCode.TypeMismatch, token, name, "number");
                    return false;
                }
                value = token.Value<float>();
                return true;
            }

            private bool TryBoolean(JToken token, string name, out bool value)
            {
                value = false;
                if (token.Type != JTokenType.Boolean)
                {
                    Report(FtdiDiagnosticCode.TypeMismatch, token, name, "boolean");
                    return false;
                }
                value = token.Value<bool>();
                return true;
            }

            private bool TryEnum<T>(JToken token, string name, out T value) where T : struct, Enum
            {
                value = default;
                if (!TryString(token, name, out var text)) return false;

                var names = Enum.GetNames(typeof(T));
                if (!names.Contains(text, StringComparer.Ordinal))
                {
                    ReportUnknown(FtdiDiagnosticCode.InvalidEnumValue, token, text, names, name);
                    return false;
                }
                value = (T)Enum.Parse(typeof(T), text);
                return true;
            }

            private void Report(FtdiDiagnosticCode code, JToken token, params string[] arguments)
            {
                _diagnostics.Add(new FtdiDiagnostic(code, LocationOf(token), arguments));
            }

            private void ReportUnknown(FtdiDiagnosticCode code, JToken token, string value, IEnumerable<string> candidates, params string[] leadingArguments)
            {
                var arguments = leadingArguments.Append(value).ToArray();
                _diagnostics.Add(new FtdiDiagnostic(code, LocationOf(token), arguments).WithSuggestion(Ftdi.Suggest(value, candidates)));
            }
        }
    }
}
#endif
