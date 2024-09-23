using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace KusakaFactory.Zatools.EditorExtension
{
    internal class MissingBlendShapeInserterConfirmationWindow : EditorWindow
    {
        private static List<AnimationClip> animationClips = new List<AnimationClip>();
        private static string skinnedMeshRendererPath = "Body";
        private static bool overwriteExistingFiles = true;
        private static string newFilePath = "";
        private static bool useReferenceGameObject = false;
        private static GameObject referenceGameObject;

        // スクロール位置を管理
        private Vector2 scrollPos;

        // 各アニメーションクリップに対する欠落しているBlendShapeのリスト
        private Dictionary<AnimationClip, List<string>> missingBlendShapesPerClip = new Dictionary<AnimationClip, List<string>>();

        // 各アニメーションクリップに対する適用フラグ
        private Dictionary<AnimationClip, bool> clipApplyFlags = new Dictionary<AnimationClip, bool>();

        // BlendShape名とその値の辞書
        private Dictionary<string, float> blendShapeValues = new Dictionary<string, float>();

        // 変更点が計算済みかどうかのフラグ
        private bool changesCalculated = false;

        public static void SetData(
            List<AnimationClip> clips,
            string rendererPath,
            bool overwriteFiles,
            string filePath,
            bool useRefGameObject,
            GameObject refGameObject
        )
        {
            animationClips = new List<AnimationClip>(clips);
            skinnedMeshRendererPath = rendererPath;
            overwriteExistingFiles = overwriteFiles;
            newFilePath = filePath;
            useReferenceGameObject = useRefGameObject;
            referenceGameObject = refGameObject;
        }

        public static void ShowWindow()
        {
            var window = GetWindow<MissingBlendShapeInserterConfirmationWindow>("変更内容の確認");
            window.minSize = new Vector2(600, 400);
            window.CalculateMissingBlendShapes(); // 変更点の計算を開始
        }

        private void OnGUI()
        {
            if (!changesCalculated)
            {
                // 変更点がまだ計算中の場合
                EditorGUILayout.LabelField("変更点を計算中...", EditorStyles.boldLabel);
                return;
            }

            if (missingBlendShapesPerClip.Count == 0)
            {
                // 変更が必要なBlendShapeがない場合
                EditorGUILayout.LabelField("すべてのアニメーションクリップに必要なBlendShapeが含まれています。", EditorStyles.boldLabel);
                return;
            }

            DrawConfirmationHeader(); // ヘッダー部分の描画
            DrawMissingBlendShapesList(); // 欠落しているBlendShapeのリストを描画
            DrawApplyButton(); // 適用ボタンの描画
        }

        // 確認ヘッダーの描画
        private void DrawConfirmationHeader()
        {
            EditorGUILayout.LabelField("変更内容の確認", EditorStyles.boldLabel);

            // オプションに応じたメッセージを表示
            string message = useReferenceGameObject
                ? "以下のアニメーションファイルにBlendShape操作を指定したSkinnedMeshの値で追加します。"
                : "以下のアニメーションファイルにBlendShape操作を0で追加します。";

            EditorGUILayout.LabelField(message);
        }

        // 欠落しているBlendShapeのリストを描画
        private void DrawMissingBlendShapesList()
        {
            // スクロールビューの開始
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            foreach (var kvp in missingBlendShapesPerClip)
            {
                var clip = kvp.Key;
                var missingBlendShapes = kvp.Value;
                missingBlendShapes.Sort(); // BlendShape名を昇順にソート

                EditorGUILayout.BeginVertical("box");

                // チェックボックス付きのアニメーションクリップ名を表示
                clipApplyFlags[clip] = EditorGUILayout.ToggleLeft($"アニメーション: {clip.name}", clipApplyFlags[clip], EditorStyles.boldLabel);

                if (clipApplyFlags[clip])
                    DrawBlendShapeDetails(missingBlendShapes); // 詳細を描画

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView(); // スクロールビューの終了
        }

        // BlendShapeの詳細を描画
        private void DrawBlendShapeDetails(List<string> blendShapes)
        {
            foreach (var blendShape in blendShapes)
            {
                // BlendShapeの値を取得
                float value = blendShapeValues.ContainsKey(blendShape) ? blendShapeValues[blendShape] : 0f;
                // BlendShape名と値を表示
                EditorGUILayout.LabelField($"  - {blendShape} : {value}");
            }
        }

        // 適用ボタンの描画
        private void DrawApplyButton()
        {
            if (GUILayout.Button("変更を適用"))
                ApplyChanges();
        }

        // 変更点の計算
        private void CalculateMissingBlendShapes()
        {
            // 初期化
            missingBlendShapesPerClip.Clear();
            clipApplyFlags.Clear();
            blendShapeValues.Clear();
            changesCalculated = false;

            // すべてのBlendShape名を取得
            HashSet<string> allBlendShapeNames = GetAllBlendShapeNames();

            // 各アニメーションクリップについて欠落しているBlendShapeを調べる
            foreach (var clip in animationClips)
            {
                if (clip == null)
                    continue;

                // アニメーションクリップであることを確認
                if (!(clip is AnimationClip))
                {
                    ShowInvalidClipError(clip);
                    continue;
                }

                // 欠落しているBlendShapeを取得
                List<string> missingBlendShapes = GetMissingBlendShapes(clip, allBlendShapeNames);

                if (missingBlendShapes.Count > 0)
                {
                    // 欠落しているBlendShapeがある場合はリストに追加
                    missingBlendShapesPerClip[clip] = missingBlendShapes;
                    clipApplyFlags[clip] = true; // デフォルトで適用するように設定
                }
            }

            // 参照GameObjectからBlendShapeの値を取得
            if (useReferenceGameObject)
            {
                if (!RetrieveBlendShapeValues(allBlendShapeNames))
                    return; // 取得に失敗した場合は処理を中断
            }

            changesCalculated = true; // 変更点の計算が完了
        }

        // 無効なクリップのエラー表示
        private void ShowInvalidClipError(AnimationClip clip)
        {
            EditorUtility.DisplayDialog("エラー", $"指定されたオブジェクトはAnimationClipではありません。オブジェクト名: {clip.name}", "OK");
        }

        // BlendShapeの値を取得
        private bool RetrieveBlendShapeValues(HashSet<string> blendShapeNames)
        {
            // 参照GameObjectからSkinnedMeshRendererを取得
            SkinnedMeshRenderer smr = GetSkinnedMeshRendererFromGameObject(referenceGameObject, skinnedMeshRendererPath);

            if (smr == null)
            {
                ShowSMRNotFoundError();
                Close(); // ウィンドウを閉じる
                return false;
            }

            if (smr.sharedMesh == null)
            {
                ShowSharedMeshNullError();
                Close(); // ウィンドウを閉じる
                return false;
            }

            // 各BlendShapeの値を取得
            foreach (var blendShapeName in blendShapeNames)
            {
                string name = blendShapeName.Substring("blendShape.".Length);
                int index = smr.sharedMesh.GetBlendShapeIndex(name);
                float value = index != -1 ? smr.GetBlendShapeWeight(index) : 0f;
                blendShapeValues[blendShapeName] = value;
            }

            return true;
        }

        // SkinnedMeshRendererが見つからないエラー表示
        private void ShowSMRNotFoundError()
        {
            EditorUtility.DisplayDialog("エラー", $"指定されたGameObjectにSkinnedMeshRendererが見つかりません。パス: {skinnedMeshRendererPath}", "OK");
        }

        // sharedMeshがnullのエラー表示
        private void ShowSharedMeshNullError()
        {
            EditorUtility.DisplayDialog("エラー", $"指定されたSkinnedMeshRendererのsharedMeshがありません。パス: {skinnedMeshRendererPath}", "OK");
        }

        // GameObjectからSkinnedMeshRendererを取得
        private SkinnedMeshRenderer GetSkinnedMeshRendererFromGameObject(GameObject go, string path)
        {
            // 指定したパスで子オブジェクトを検索
            Transform child = go.transform.Find(path);

            if (child != null)
                return child.GetComponent<SkinnedMeshRenderer>();

            // 自身の名前がパスと一致する場合
            if (go.name == path)
                return go.GetComponent<SkinnedMeshRenderer>();

            return null; // 見つからなかった場合
        }

        // すべてのBlendShape名を取得
        private HashSet<string> GetAllBlendShapeNames()
        {
            var blendShapeNames = new HashSet<string>();

            // 各アニメーションクリップからBlendShape名を収集
            foreach (var clip in animationClips)
            {
                if (clip == null)
                    continue;

                var bindings = AnimationUtility.GetCurveBindings(clip);

                foreach (var binding in bindings)
                {
                    if (IsTargetBlendShape(binding))
                        blendShapeNames.Add(binding.propertyName);
                }
            }

            return blendShapeNames;
        }

        // クリップから欠落しているBlendShapeを取得
        private List<string> GetMissingBlendShapes(AnimationClip clip, HashSet<string> allBlendShapeNames)
        {
            var clipBlendShapes = new HashSet<string>();

            var bindings = AnimationUtility.GetCurveBindings(clip);

            // クリップが持っているBlendShape名を収集
            foreach (var binding in bindings)
            {
                if (IsTargetBlendShape(binding))
                    clipBlendShapes.Add(binding.propertyName);
            }

            var missingBlendShapes = new List<string>();

            // すべてのBlendShape名と比較して欠落しているものを抽出
            foreach (var blendShape in allBlendShapeNames)
            {
                if (!clipBlendShapes.Contains(blendShape))
                    missingBlendShapes.Add(blendShape);
            }

            return missingBlendShapes;
        }

        // 対象のBlendShapeかどうかを判定
        private bool IsTargetBlendShape(EditorCurveBinding binding)
        {
            return binding.type == typeof(SkinnedMeshRenderer) &&
                   binding.path.Equals(skinnedMeshRendererPath) &&
                   binding.propertyName.StartsWith("blendShape.") &&
                   !binding.isPPtrCurve;
        }

        // 変更の適用
        private void ApplyChanges()
        {
            bool anyApplied = false; // 変更が適用されたかどうかのフラグ

            foreach (var kvp in missingBlendShapesPerClip)
            {
                var clip = kvp.Key;

                if (!clipApplyFlags[clip])
                    continue; // 適用しない場合はスキップ

                // ターゲットクリップを準備
                AnimationClip targetClip = PrepareTargetClip(clip);

                if (targetClip == null)
                    continue; // 準備に失敗した場合はスキップ

                // BlendShapeをクリップに追加
                AddBlendShapesToClip(targetClip, kvp.Value);
                anyApplied = true; // 変更が適用された
            }

            if (anyApplied)
            {
                // アセットデータベースを更新
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("完了", "変更が適用されました。", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("情報", "適用するアニメーションクリップが選択されていません。", "OK");
            }

            Close(); // ウィンドウを閉じる
        }

        // ターゲットクリップの準備
        private AnimationClip PrepareTargetClip(AnimationClip clip)
        {
            if (overwriteExistingFiles)
            {
                // 上書き保存の場合
                if (IsClipReadOnly(clip))
                {
                    ShowReadOnlyError(clip);
                    return null;
                }
                return clip; // 既存のクリップを使用
            }
            else
            {
                // 新しいファイルに保存する場合
                return CreateNewClip(clip);
            }
        }

        // クリップが読み取り専用かどうかを確認
        private bool IsClipReadOnly(AnimationClip clip)
        {
            string assetPath = AssetDatabase.GetAssetPath(clip);
            string fullPath = Path.GetFullPath(assetPath);

            return (File.GetAttributes(fullPath) & FileAttributes.ReadOnly) != 0;
        }

        // 読み取り専用エラーの表示
        private void ShowReadOnlyError(AnimationClip clip)
        {
            string assetPath = AssetDatabase.GetAssetPath(clip);
            EditorUtility.DisplayDialog("エラー", $"ファイルが読み取り専用のため、上書きできません。ファイルパス: {assetPath}", "OK");
        }

        // 新しいクリップの作成
        private AnimationClip CreateNewClip(AnimationClip originalClip)
        {
            // クリップをコピー
            AnimationClip newClip = Instantiate(originalClip);
            string assetPath = GetNewAssetPath(originalClip);

            if (string.IsNullOrEmpty(assetPath))
                return null;

            // 新しいアセットとして保存
            AssetDatabase.CreateAsset(newClip, assetPath);

            // 保存が成功したか確認
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath) == null)
            {
                EditorUtility.DisplayDialog("エラー", $"アセットの作成に失敗しました。パス: {assetPath}", "OK");
                return null;
            }

            return newClip;
        }

        // 新しいアセットパスを取得
        private string GetNewAssetPath(AnimationClip originalClip)
        {
            string originalPath = AssetDatabase.GetAssetPath(originalClip);
            string fileName = Path.GetFileNameWithoutExtension(originalPath);
            string newFileName = fileName + "_modified.anim";
            string directory = string.IsNullOrEmpty(newFilePath) ? Path.GetDirectoryName(originalPath) : newFilePath;

            // 保存先のディレクトリが存在するか確認
            if (!Directory.Exists(directory))
            {
                EditorUtility.DisplayDialog("エラー", $"保存先フォルダが存在しません。パス: {directory}", "OK");
                return null;
            }

            // ユニークなアセットパスを生成
            return AssetDatabase.GenerateUniqueAssetPath(Path.Combine(directory, newFileName));
        }

        // BlendShapeをクリップに追加
        private void AddBlendShapesToClip(AnimationClip clip, List<string> blendShapes)
        {
            foreach (var blendShape in blendShapes)
            {
                // BlendShapeの値を取得
                float value = blendShapeValues.ContainsKey(blendShape) ? blendShapeValues[blendShape] : 0f;

                // カーブバインディングを設定
                var binding = new EditorCurveBinding
                {
                    type = typeof(SkinnedMeshRenderer),
                    path = skinnedMeshRendererPath,
                    propertyName = blendShape
                };

                // アニメーションカーブを作成
                var curve = new AnimationCurve(new Keyframe(0f, value));

                try
                {
                    // カーブをクリップに設定
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
                catch (System.Exception ex)
                {
                    EditorUtility.DisplayDialog("エラー", $"カーブの設定に失敗しました。BlendShape: {blendShape}, エラー: {ex.Message}", "OK");
                }
            }
        }
    }
}
