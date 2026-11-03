#if ZATOOLS_HAS_VRCSDK_FACE_TRACKING
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using KusakaFactory.Zatools.Ndmf.Core;
using UnityObject = UnityEngine.Object;
using FaceTrackingKey = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingKey;
using TargetMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorTargetMode;
using CombineMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.ParameterBehaviorCombineMode;
using VisemeDampenMode = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.VisemeDampenMode;
using SdkBlendShapeSet = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingBlendshapesSet;
using SdkParameterBehavior = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingParameterBehavior;

namespace KusakaFactory.Zatools.Tests.Ndmf.Core
{
    public sealed class FtdiTests
    {
        private readonly List<UnityObject> _createdObjects = new List<UnityObject>();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (var obj in _createdObjects) UnityObject.DestroyImmediate(obj);
            _createdObjects.Clear();
        }

        private static string Json(string text)
        {
            return text.Replace('\'', '"');
        }

        private static Ftdi.Definition Parse(string text, out List<FtdiDiagnostic> diagnostics)
        {
            diagnostics = new List<FtdiDiagnostic>();
            return FtdiJson.Parse(Json(text), diagnostics);
        }

        private static Ftdi.Definition ParseValid(string text)
        {
            var definition = Parse(text, out var diagnostics);
            Assert.That(Codes(diagnostics), Is.Empty);
            return definition;
        }

        private static FtdiDiagnosticCode[] Codes(IEnumerable<FtdiDiagnostic> diagnostics)
        {
            return diagnostics.Select((d) => d.Code).ToArray();
        }

        private static Ftdi.Source Source(string text, params SkinnedMeshRenderer[] meshes)
        {
            return new Ftdi.Source { Context = null, Definition = ParseValid(text), Meshes = meshes };
        }

        private static Ftdi.Configuration Merge(out List<FtdiDiagnostic> diagnostics, params Ftdi.Source[] sources)
        {
            diagnostics = new List<FtdiDiagnostic>();
            return Ftdi.Merge(sources, false, diagnostics);
        }

        private SkinnedMeshRenderer CreateRenderer()
        {
            var gameObject = new GameObject("FtdiTestsRenderer");
            _createdObjects.Add(gameObject);
            return gameObject.AddComponent<SkinnedMeshRenderer>();
        }

        [Test]
        public void ParsesSetsAndBehaviorsByName()
        {
            var definition = ParseValid(@"{
                'Version': 1,
                'UseAllTrackingKeys': true,
                'VisemeDampen': { 'Mode': 'DampenVisemes', 'Amount': 0.5 },
                'BlendShapeSets': [
                    {
                        'Name': 'Eyebrows',
                        'DefaultWeight': 0.75,
                        'TrackingKeys': {
                            'BrowPinchLeft': 'pinch_l',
                            'BrowPinchRight': { 'Shape': 'pinch_r', 'Multiplier': 0.5 }
                        },
                        'Visemes': { 'aa': 'vrc.v_aa' }
                    }
                ],
                'ParameterBehaviors': [
                    {
                        'Operation': 'ApplyBlendSetWeight',
                        'Target': 'Eyebrows',
                        'Expression': { 'Type': 'Max', 'TrackingKeys': ['EyeClosedLeft', { 'Key': 'Voice', 'Invert': true }, 'Viseme_aa'] }
                    },
                    {
                        'Operation': 'ForwardToAnimator',
                        'Target': 'Param',
                        'Weight': 'Eyebrows',
                        'Expression': { 'Type': 'Custom', 'Expression': 'EyeClosedLeft + 1.0' }
                    }
                ]
            }");

            Assert.That(definition.UseAllTrackingKeys, Is.True);
            Assert.That(definition.VisemeDampen, Is.EqualTo(new Ftdi.VisemeDampen(VisemeDampenMode.DampenVisemes, 0.5f)));

            var set = definition.BlendShapeSets.Single();
            Assert.That(set.Name, Is.EqualTo("Eyebrows"));
            Assert.That(set.DefaultWeight, Is.EqualTo(0.75f));
            Assert.That(set.TrackingKeys.Select((m) => (m.Index, m.Shape, m.Multiplier)), Is.EqualTo(new[]
            {
                ((int)FaceTrackingKey.BrowPinchLeft, "pinch_l", 1.0f),
                ((int)FaceTrackingKey.BrowPinchRight, "pinch_r", 0.5f),
            }));
            Assert.That(set.Visemes.Select((m) => (m.Index, m.Shape)), Is.EqualTo(new[] { (10, "vrc.v_aa") }));

            var combined = definition.ParameterBehaviors[0];
            Assert.That(combined.Operation, Is.EqualTo(TargetMode.ApplyBlendSetWeight));
            Assert.That(combined.Combine, Is.EqualTo(CombineMode.Max));
            Assert.That(combined.Inputs.Select((i) => (i.Index, i.Invert)), Is.EqualTo(new[]
            {
                ((int)FaceTrackingKey.EyeClosedLeft, false),
                (Ftdi.VoiceInputIndex, true),
                (113, false),
            }));

            var custom = definition.ParameterBehaviors[1];
            Assert.That(custom.Combine, Is.EqualTo(CombineMode.Custom));
            Assert.That(custom.CustomExpression, Is.EqualTo("EyeClosedLeft + 1.0"));
            Assert.That(custom.Weight, Is.EqualTo("Eyebrows"));
        }

        [Test]
        public void ReportsUnknownKeysWithSuggestionAndLocation()
        {
            Parse(@"{
                'Version': 1,
                'BlendShapeSets': [{ 'Name': 'Eyes', 'TrackingKeys': { 'eyeclosedleft': 'blink_l' } }],
                'ParameterBehaviors': [
                    { 'Operation': 'ForwardToAnimator', 'Target': 'P', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['EyeClosedLeftt'] } }
                ]
            }", out var diagnostics);

            Assert.That(Codes(diagnostics), Is.EqualTo(new[] { FtdiDiagnosticCode.UnknownTrackingKey, FtdiDiagnosticCode.UnknownInputKey }));
            Assert.That(diagnostics.Select((d) => d.Suggestion), Is.EqualTo(new[] { "EyeClosedLeft", "EyeClosedLeft" }));
            Assert.That(diagnostics.Select((d) => d.Location.Line), Is.EqualTo(new[] { 3, 5 }));
        }

        [Test]
        public void ReportsInvalidEnumValueWithSuggestion()
        {
            Parse(@"{
                'Version': 1,
                'ParameterBehaviors': [
                    { 'Operation': 'ApplyBlendShapeSetWeight', 'Target': 'S', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['JawOpen'] } }
                ]
            }", out var diagnostics);

            Assert.That(Codes(diagnostics), Is.EqualTo(new[] { FtdiDiagnosticCode.InvalidEnumValue }));
            Assert.That(diagnostics[0].Arguments, Is.EqualTo(new[] { "Operation", "ApplyBlendShapeSetWeight" }));
            Assert.That(diagnostics[0].Suggestion, Is.EqualTo("ApplyBlendSetWeight"));
        }

        [Test]
        public void RejectsUnknownPropertiesAndMissingRequiredOnes()
        {
            Parse(@"{
                'Version': 1,
                'Extra': 0,
                'BlendShapeSets': [{ 'TrackingKeys': {} }],
                'ParameterBehaviors': [
                    { 'Operation': 'ForwardToAnimator', 'Target': 'P', 'Expression': { 'Type': 'Custom', 'Expression': 'JawOpen', 'TrackingKeys': [] } }
                ]
            }", out var diagnostics);

            Assert.That(Codes(diagnostics), Is.EquivalentTo(new[]
            {
                FtdiDiagnosticCode.UnknownProperty,
                FtdiDiagnosticCode.MissingProperty,
                FtdiDiagnosticCode.UnknownProperty,
            }));
        }

        [Test]
        public void RejectsSyntaxErrorsDuplicatePropertiesAndUnsupportedVersions()
        {
            Assert.That(Parse("{ 'Version': 1,", out var truncated), Is.Null);
            Assert.That(Codes(truncated), Is.EqualTo(new[] { FtdiDiagnosticCode.SyntaxError }));

            Assert.That(Parse("{ 'Version': 1, 'Version': 1 }", out var duplicated), Is.Null);
            Assert.That(Codes(duplicated), Is.EqualTo(new[] { FtdiDiagnosticCode.SyntaxError }));

            Assert.That(Parse("{ 'Version': 1 } {}", out var trailing), Is.Null);
            Assert.That(Codes(trailing), Is.EqualTo(new[] { FtdiDiagnosticCode.SyntaxError }));

            Assert.That(Parse("{ 'Version': 2 }", out var version), Is.Null);
            Assert.That(Codes(version), Is.EqualTo(new[] { FtdiDiagnosticCode.UnsupportedVersion }));

            Assert.That(Parse("{ 'Version': 99999999999999999999 }", out var huge), Is.Null);
            Assert.That(Codes(huge), Is.EqualTo(new[] { FtdiDiagnosticCode.UnsupportedVersion }));

            Assert.That(Parse("{ 'Version': 1.0 }", out var fractional), Is.Null);
            Assert.That(Codes(fractional), Is.EqualTo(new[] { FtdiDiagnosticCode.UnsupportedVersion }));

            Assert.That(Parse("[]", out var array), Is.Null);
            Assert.That(Codes(array), Is.EqualTo(new[] { FtdiDiagnosticCode.RootNotObject }));
        }

        [Test]
        public void RejectsEmptyAndOversizedInputs()
        {
            var keys = string.Join(", ", Enumerable.Repeat("'JawOpen'", Ftdi.MaxInputs + 1));
            Parse(@"{
                'Version': 1,
                'ParameterBehaviors': [
                    { 'Operation': 'ForwardToAnimator', 'Target': 'A', 'Expression': { 'Type': 'Max', 'TrackingKeys': [] } },
                    { 'Operation': 'ForwardToAnimator', 'Target': 'B', 'Expression': { 'Type': 'Max', 'TrackingKeys': [" + keys + @"] } },
                    { 'Operation': 'ForwardToAnimator', 'Target': '', 'Expression': { 'Type': 'Custom', 'Expression': '' } }
                ]
            }", out var diagnostics);

            Assert.That(Codes(diagnostics), Is.EqualTo(new[]
            {
                FtdiDiagnosticCode.NoInputs,
                FtdiDiagnosticCode.TooManyInputs,
                FtdiDiagnosticCode.EmptyValue,
                FtdiDiagnosticCode.EmptyValue,
            }));
        }

        [Test]
        public void ReportsDuplicateSetsAndExclusiveOutputsInOneDefinition()
        {
            Parse(@"{
                'Version': 1,
                'BlendShapeSets': [{ 'Name': 'S' }, { 'Name': 'S' }],
                'ParameterBehaviors': [
                    { 'Operation': 'ForwardToAnimator', 'Target': 'P', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['JawOpen'] } },
                    { 'Operation': 'ForwardToAnimator', 'Target': 'P', 'Expression': { 'Type': 'Min', 'TrackingKeys': ['JawOpen'] } },
                    { 'Operation': 'LimitBlendshapeMax', 'Target': 'shape', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['JawOpen'] } },
                    { 'Operation': 'LimitBlendshapeMax', 'Target': 'shape', 'Expression': { 'Type': 'Min', 'TrackingKeys': ['JawOpen'] } }
                ]
            }", out var diagnostics);

            Assert.That(Codes(diagnostics), Is.EqualTo(new[] { FtdiDiagnosticCode.DuplicateBlendShapeSet, FtdiDiagnosticCode.DuplicateOutput }));
        }

        [Test]
        public void MergesSameNamedSetsByKey()
        {
            var configuration = Merge(
                out var diagnostics,
                Source("{ 'Version': 1, 'BlendShapeSets': [{ 'Name': 'Eyes', 'DefaultWeight': 0.5, 'TrackingKeys': { 'EyeClosedLeft': 'blink_l' } }] }"),
                Source("{ 'Version': 1, 'BlendShapeSets': [{ 'Name': 'Eyes', 'TrackingKeys': { 'EyeClosedLeft': 'blink_l', 'EyeClosedRight': 'blink_r' } }] }"),
                Source("{ 'Version': 1, 'BlendShapeSets': [{ 'Name': 'Other' }] }"));

            Assert.That(Codes(diagnostics), Is.Empty);
            Assert.That(configuration.BlendShapeSets.Select((s) => s.setName), Is.EqualTo(new[] { "Eyes", "Other" }));

            var eyes = configuration.BlendShapeSets[0];
            Assert.That(eyes.setWeight, Is.EqualTo(0.5f));
            Assert.That(eyes.faceTrackingBlendshapeNames, Has.Length.EqualTo(Ftdi.TrackingKeyCount));
            Assert.That(eyes.faceTrackingBlendshapeNames[(int)FaceTrackingKey.EyeClosedLeft], Is.EqualTo("blink_l"));
            Assert.That(eyes.faceTrackingBlendshapeNames[(int)FaceTrackingKey.EyeClosedRight], Is.EqualTo("blink_r"));
            Assert.That(eyes.faceTrackingBlendshapeNames.Count((n) => n != ""), Is.EqualTo(2));
            Assert.That(eyes.faceTrackingBlendshapeMultipliers, Has.All.EqualTo(1.0f));
            Assert.That(eyes.visemeBlendshapeNames, Has.Length.EqualTo(Ftdi.VisemeCount));
            Assert.That(configuration.BlendShapeSets[1].setWeight, Is.EqualTo(1.0f));
        }

        [Test]
        public void ReportsConflictingSetsAcrossSources()
        {
            Merge(
                out var diagnostics,
                Source("{ 'Version': 1, 'BlendShapeSets': [{ 'Name': 'Eyes', 'DefaultWeight': 0.5, 'TrackingKeys': { 'EyeClosedLeft': 'blink_l' }, 'Visemes': { 'aa': 'a' } }] }"),
                Source("{ 'Version': 1, 'BlendShapeSets': [{ 'Name': 'Eyes', 'DefaultWeight': 1.0, 'TrackingKeys': { 'EyeClosedLeft': { 'Shape': 'blink_l', 'Multiplier': 0.5 } }, 'Visemes': { 'aa': 'b' } }] }"));

            Assert.That(Codes(diagnostics), Is.EqualTo(new[]
            {
                FtdiDiagnosticCode.DefaultWeightConflict,
                FtdiDiagnosticCode.TrackingKeyConflict,
                FtdiDiagnosticCode.VisemeConflict,
            }));
        }

        [Test]
        public void RecordsBehaviorOriginsInMergeOrder()
        {
            const string two = "{ 'Version': 1, 'ParameterBehaviors': [{ 'Operation': 'ForwardToAnimator', 'Target': 'A', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['EyeClosedLeft'] } }, { 'Operation': 'ForwardToAnimator', 'Target': 'B', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['EyeClosedLeft'] } }] }";
            const string one = "{ 'Version': 1, 'ParameterBehaviors': [{ 'Operation': 'ForwardToAnimator', 'Target': 'C', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['EyeClosedLeft'] } }] }";
            var first = CreateRenderer();
            var second = CreateRenderer();

            var configuration = Merge(
                out var diagnostics,
                new Ftdi.Source { Context = first, Definition = ParseValid(two) },
                new Ftdi.Source { Context = second, Definition = ParseValid(one) });

            Assert.That(Codes(diagnostics), Is.Empty);
            Assert.That(configuration.ParameterBehaviors.Select((b) => b.outputParameterName), Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(configuration.ParameterBehaviorOrigins.Select((o) => (o.Context, o.Index)), Is.EqualTo(new[]
            {
                ((UnityObject)first, 0),
                ((UnityObject)first, 1),
                ((UnityObject)second, 0),
            }));
        }

        [Test]
        public void ReportsExclusiveOutputConflictsAcrossSources()
        {
            const string behaviors = "{ 'Version': 1, 'ParameterBehaviors': [{ 'Operation': '{0}', 'Target': 'X', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['EyeClosedLeft'] } }] }";
            string For(TargetMode mode) => behaviors.Replace("{0}", mode.ToString());

            Merge(
                out var diagnostics,
                Source("{ 'Version': 1, 'BlendShapeSets': [{ 'Name': 'X' }] }"),
                Source(For(TargetMode.ForwardToAnimator)),
                Source(For(TargetMode.ForwardToAnimator)),
                Source(For(TargetMode.ApplyBlendSetWeight)),
                Source(For(TargetMode.ApplyBlendSetWeight)),
                Source(For(TargetMode.ApplyToBlendshape)),
                Source(For(TargetMode.ApplyToBlendshape)),
                Source(For(TargetMode.LimitBlendshapeMin)),
                Source(For(TargetMode.LimitBlendshapeMin)));

            Assert.That(Codes(diagnostics), Is.EqualTo(Enumerable.Repeat(FtdiDiagnosticCode.OutputConflict, 3)));
        }

        [Test]
        public void ResolvesSetReferencesAcrossSources()
        {
            const string referencing = @"{
                'Version': 1,
                'ParameterBehaviors': [
                    { 'Operation': 'ApplyBlendSetWeight', 'Target': 'Defined', 'Weight': 'Missing', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['EyeClosedLeft'] } }
                ]
            }";
            Merge(
                out var diagnostics,
                Source(referencing),
                Source("{ 'Version': 1, 'BlendShapeSets': [{ 'Name': 'Defined' }] }"));

            Assert.That(Codes(diagnostics), Is.EqualTo(new[] { FtdiDiagnosticCode.UnknownBlendShapeSet }));
            Assert.That(diagnostics[0].Arguments, Is.EqualTo(new[] { "Missing" }));
        }

        [Test]
        public void RequiresUseAllTrackingKeysForNonBasicKeys()
        {
            const string nonBasic = @"{
                'Version': 1,
                'BlendShapeSets': [{ 'Name': 'S', 'TrackingKeys': { 'EyeClosedLeft': 'a', 'JawOpen': 'b' } }],
                'ParameterBehaviors': [
                    { 'Operation': 'ForwardToAnimator', 'Target': 'P', 'Expression': { 'Type': 'Max', 'TrackingKeys': ['EyeLookUpLeft', 'TongueOut', 'Voice', 'Viseme_aa'] } }
                ]
            }";

            Merge(out var disabled, Source(nonBasic));
            Assert.That(Codes(disabled), Is.EqualTo(new[] { FtdiDiagnosticCode.NonBasicTrackingKey, FtdiDiagnosticCode.NonBasicTrackingKey }));
            Assert.That(disabled.Select((d) => d.Arguments[0]), Is.EqualTo(new[] { "JawOpen", "TongueOut" }));

            Merge(out var enabled, Source(nonBasic), Source("{ 'Version': 1, 'UseAllTrackingKeys': true }"));
            Assert.That(Codes(enabled), Is.Empty);

            var fallback = new List<FtdiDiagnostic>();
            Ftdi.Merge(new[] { Source(nonBasic) }, true, fallback);
            Assert.That(Codes(fallback), Is.Empty);
        }

        [Test]
        public void ReportsGlobalSettingConflicts()
        {
            var configuration = Merge(
                out var diagnostics,
                Source("{ 'Version': 1, 'UseAllTrackingKeys': true, 'VisemeDampen': { 'Mode': 'DampenTracking', 'Amount': 0.5 } }"),
                Source("{ 'Version': 1 }"),
                Source("{ 'Version': 1, 'UseAllTrackingKeys': false, 'VisemeDampen': { 'Mode': 'DampenTracking', 'Amount': 0.25 } }"));

            Assert.That(Codes(diagnostics), Is.EqualTo(new[] { FtdiDiagnosticCode.UseAllTrackingKeysConflict, FtdiDiagnosticCode.VisemeDampenConflict }));
            Assert.That(configuration.UseAllTrackingKeys, Is.True);
        }

        [Test]
        public void DeduplicatesMeshesAndEnforcesLimits()
        {
            var shared = CreateRenderer();
            var other = CreateRenderer();
            var configuration = Merge(
                out var diagnostics,
                Source("{ 'Version': 1 }", shared, null, other),
                Source("{ 'Version': 1 }", other, shared));
            Assert.That(Codes(diagnostics), Is.Empty);
            Assert.That(configuration.Meshes, Is.EqualTo(new[] { shared, other }));

            var many = Enumerable.Range(0, Ftdi.MaxMeshes + 1).Select((_) => CreateRenderer()).ToArray();
            Merge(out var tooMany, Source("{ 'Version': 1 }", many));
            Assert.That(Codes(tooMany), Is.EqualTo(new[] { FtdiDiagnosticCode.TooManyMeshes }));
        }

        [Test]
        public void ConvertsBehaviorsToSdkFields()
        {
            var configuration = Merge(out var diagnostics, Source(@"{
                'Version': 1,
                'UseAllTrackingKeys': true,
                'BlendShapeSets': [{ 'Name': 'S' }],
                'ParameterBehaviors': [
                    { 'Operation': 'ForwardToAnimator', 'Target': 'Param', 'Expression': { 'Type': 'Add', 'TrackingKeys': [{ 'Key': 'JawOpen', 'Invert': true }, 'Viseme_sil'] } },
                    { 'Operation': 'ApplyBlendSetWeight', 'Target': 'S', 'Weight': 'S', 'Expression': { 'Type': 'Custom', 'Expression': 'JawOpen' } },
                    { 'Operation': 'LimitBlendshapeMin', 'Target': 'shape', 'Expression': { 'Type': 'Multiply', 'TrackingKeys': ['Voice'] } }
                ]
            }"));
            Assert.That(Codes(diagnostics), Is.Empty);

            var forward = configuration.ParameterBehaviors[0];
            Assert.That((forward.outputParameterName, forward.targetBlendSetName, forward.targetBlendshapeName), Is.EqualTo(("Param", "", "")));
            Assert.That(forward.inputTrackingKeys, Is.EqualTo(new[] { (int)FaceTrackingKey.JawOpen, 103 }));
            Assert.That(forward.inputTrackingKeyNames, Is.EqualTo(new[] { "JawOpen", "Viseme_sil" }));
            Assert.That(forward.inputInverted, Is.EqualTo(new[] { true, false }));
            Assert.That(forward.combineMode, Is.EqualTo(CombineMode.Add));
            Assert.That(forward.weightBlendSetName, Is.EqualTo(""));

            var apply = configuration.ParameterBehaviors[1];
            Assert.That((apply.outputParameterName, apply.targetBlendSetName, apply.targetBlendshapeName), Is.EqualTo(("", "S", "")));
            Assert.That(apply.weightBlendSetName, Is.EqualTo("S"));
            Assert.That(apply.customExpression, Is.EqualTo("JawOpen"));
            Assert.That(apply.inputTrackingKeys, Is.Empty);

            var limit = configuration.ParameterBehaviors[2];
            Assert.That((limit.outputParameterName, limit.targetBlendSetName, limit.targetBlendshapeName), Is.EqualTo(("", "", "shape")));
            Assert.That(limit.inputTrackingKeyNames, Is.EqualTo(new[] { "Voice" }));
        }

        [Test]
        public void ExportedDefinitionRoundTrips()
        {
            var names = new string[Ftdi.TrackingKeyCount];
            var multipliers = Enumerable.Repeat(1.0f, Ftdi.TrackingKeyCount).ToArray();
            names[(int)FaceTrackingKey.EyeClosedLeft] = "blink_l";
            names[(int)FaceTrackingKey.JawOpen] = "jaw";
            multipliers[(int)FaceTrackingKey.JawOpen] = 0.25f;
            var visemeNames = new string[Ftdi.VisemeCount];
            visemeNames[3] = "th";

            var sets = new[]
            {
                new SdkBlendShapeSet
                {
                    setName = "Face",
                    setWeight = 0.5f,
                    faceTrackingBlendshapeNames = names,
                    faceTrackingBlendshapeMultipliers = multipliers,
                    visemeBlendshapeNames = visemeNames,
                    visemeBlendshapeMultipliers = Enumerable.Repeat(1.0f, Ftdi.VisemeCount).ToArray(),
                },
            };
            var behaviors = new[]
            {
                new SdkParameterBehavior
                {
                    targetMode = TargetMode.ApplyBlendSetWeight,
                    outputParameterName = "",
                    targetBlendSetName = "Face",
                    targetBlendshapeName = "",
                    weightBlendSetName = "",
                    inputTrackingKeys = new[] { (int)FaceTrackingKey.JawOpen, 999 },
                    inputTrackingKeyNames = new[] { "JawOpen", "Voice" },
                    inputInverted = new[] { true, false },
                    combineMode = CombineMode.Min,
                    customExpression = "",
                },
                new SdkParameterBehavior
                {
                    targetMode = TargetMode.ForwardToAnimator,
                    outputParameterName = "Param",
                    targetBlendSetName = "",
                    targetBlendshapeName = "",
                    weightBlendSetName = "Face",
                    inputTrackingKeys = new[] { 0 },
                    inputTrackingKeyNames = new[] { "EyeLookOutRight" },
                    inputInverted = new[] { false },
                    combineMode = CombineMode.Custom,
                    customExpression = "JawOpen * 2",
                },
            };

            var diagnostics = new List<FtdiDiagnostic>();
            var exported = FtdiJson.Write(Ftdi.Export(sets, behaviors, true, new Ftdi.VisemeDampen(VisemeDampenMode.DampenTracking, 0.5f), diagnostics));
            Assert.That(Codes(diagnostics), Is.Empty);
            Assert.That(exported, Does.Not.Contain("\r"));
            Assert.That(exported, Does.Contain("\"JawOpen\""));

            var definition = FtdiJson.Parse(exported, diagnostics);
            Assert.That(Codes(diagnostics), Is.Empty);
            var configuration = Ftdi.Merge(new[] { new Ftdi.Source { Definition = definition } }, false, diagnostics);
            Assert.That(Codes(diagnostics), Is.Empty);

            Assert.That(configuration.UseAllTrackingKeys, Is.True);
            Assert.That(configuration.VisemeDampen, Is.EqualTo(new Ftdi.VisemeDampen(VisemeDampenMode.DampenTracking, 0.5f)));

            var set = configuration.BlendShapeSets.Single();
            Assert.That(set.setWeight, Is.EqualTo(0.5f));
            Assert.That(set.faceTrackingBlendshapeNames, Is.EqualTo(names.Select((n) => n ?? "")));
            Assert.That(set.faceTrackingBlendshapeMultipliers, Is.EqualTo(multipliers));
            Assert.That(set.visemeBlendshapeNames, Is.EqualTo(visemeNames.Select((n) => n ?? "")));

            var apply = configuration.ParameterBehaviors[0];
            Assert.That(apply.inputTrackingKeys, Is.EqualTo(new[] { (int)FaceTrackingKey.JawOpen, Ftdi.VoiceInputIndex }));
            Assert.That(apply.inputInverted, Is.EqualTo(new[] { true, false }));
            Assert.That(apply.combineMode, Is.EqualTo(CombineMode.Min));

            var forward = configuration.ParameterBehaviors[1];
            Assert.That(forward.outputParameterName, Is.EqualTo("Param"));
            Assert.That(forward.weightBlendSetName, Is.EqualTo("Face"));
            Assert.That(forward.customExpression, Is.EqualTo("JawOpen * 2"));
            Assert.That(forward.inputTrackingKeys, Is.Empty);
        }

        [Test]
        public void ExportReportsUnresolvableInputs()
        {
            var behaviors = new[]
            {
                new SdkParameterBehavior
                {
                    targetMode = TargetMode.ForwardToAnimator,
                    outputParameterName = "Param",
                    inputTrackingKeys = new[] { 999, 998, (int)FaceTrackingKey.JawOpen },
                    inputTrackingKeyNames = new[] { "Voicee", "" },
                    inputInverted = new[] { false, false, true },
                    combineMode = CombineMode.Max,
                },
            };

            var diagnostics = new List<FtdiDiagnostic>();
            var definition = Ftdi.Export(new SdkBlendShapeSet[0], behaviors, true, new Ftdi.VisemeDampen(VisemeDampenMode.None, 0.0f), diagnostics);

            Assert.That(Codes(diagnostics), Is.EqualTo(new[] { FtdiDiagnosticCode.UnknownInputKey, FtdiDiagnosticCode.UnknownInputKey }));
            Assert.That(diagnostics.Select((d) => d.Arguments[0]), Is.EqualTo(new[] { "Voicee", "998" }));
            Assert.That(diagnostics.Select((d) => d.Suggestion), Is.EqualTo(new[] { "Voice", null }));
            Assert.That(definition.ParameterBehaviors.Single().Inputs.Select((i) => (i.Index, i.Invert)), Is.EqualTo(new[] { ((int)FaceTrackingKey.JawOpen, true) }));
        }

        [Test]
        public void ExportedIncompleteEntriesFailToParse()
        {
            var sets = new[] { new SdkBlendShapeSet { setName = "" } };
            var behaviors = new[]
            {
                new SdkParameterBehavior
                {
                    targetMode = TargetMode.ApplyToBlendshape,
                    targetBlendshapeName = "",
                    inputTrackingKeys = new[] { (int)FaceTrackingKey.JawOpen },
                    inputTrackingKeyNames = new[] { "JawOpen" },
                    inputInverted = new[] { false },
                    combineMode = CombineMode.Max,
                },
                new SdkParameterBehavior
                {
                    targetMode = TargetMode.ForwardToAnimator,
                    outputParameterName = "Param",
                    combineMode = CombineMode.Custom,
                    customExpression = "",
                },
            };

            var diagnostics = new List<FtdiDiagnostic>();
            var exported = FtdiJson.Write(Ftdi.Export(sets, behaviors, true, new Ftdi.VisemeDampen(VisemeDampenMode.None, 0.0f), diagnostics));
            Assert.That(Codes(diagnostics), Is.Empty);

            FtdiJson.Parse(exported, diagnostics);
            Assert.That(Codes(diagnostics), Is.EqualTo(Enumerable.Repeat(FtdiDiagnosticCode.EmptyValue, 3)));
            Assert.That(diagnostics.Select((d) => d.Arguments[0]), Is.EqualTo(new[] { "Name", "Target", "Expression" }));
            Assert.That(diagnostics.Select((d) => d.Location.IsKnown), Has.All.True);
        }
    }
}
#endif
