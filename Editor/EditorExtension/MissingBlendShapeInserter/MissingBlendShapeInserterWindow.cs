using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace KusakaFactory.Zatools.EditorExtension
{
  internal class MissingBlendShapeInserterWindow : EditorWindow
  {
    // ユーザーが指定するアニメーションクリップのリスト
    public List<AnimationClip> animationClips = new List<AnimationClip>();

    // スクロール位置を管理
    private Vector2 scrollPos;
    private Vector2 mainScrollPos;

    // 保存オプション関連
    public bool overwriteExistingFiles = true;
    public string newFilePath = "";

    // SkinnedMeshRendererのパス（名前）
    public string skinnedMeshRendererPath = "Body";

    // BlendShapeの値を取得するためのオプションと参照GameObject
    public bool useReferenceGameObject = false;
    public GameObject referenceGameObject;

    [MenuItem("Window/kb10uy/Missing BlendShape Inserter")]
    public static void ShowWindow()
    {
      var window = GetWindow<MissingBlendShapeInserterWindow>("Missing BlendShape Inserter");
      window.minSize = new Vector2(600, 400);
    }

    private void OnGUI()
    {
      DrawHeader(); // ヘッダー部分の描画

      // ウィンドウをスクロール可能にする
      mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

      DrawDragAndDropArea();          // ドラッグ＆ドロップエリアの描画
      DrawAnimationClipList();        // アニメーションクリップリストの描画
      DrawSkinnedMeshRendererPathField(); // SkinnedMeshRendererパスフィールドの描画
      DrawReferenceGameObjectOption();    // 参照GameObjectオプションの描画
      DrawSaveOptions();                  // 保存オプションの描画

      EditorGUILayout.EndScrollView(); // スクロールビューの終了

      DrawExecuteButton(); // 実行ボタンの描画
    }

    // ヘッダー部分の描画
    private void DrawHeader()
    {
      EditorGUILayout.LabelField("複数のアニメーションファイル間で足りないBlendShape操作を補完し、表情が破綻しないように修正できます。", EditorStyles.wordWrappedLabel);
    }

    // ドラッグ＆ドロップエリアの描画
    private void DrawDragAndDropArea()
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("アニメーションファイルをドラッグ＆ドロップで指定", EditorStyles.boldLabel);

      // ドラッグ＆ドロップエリアを作成
      Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
      GUI.Box(dropArea, "ここにアニメーションファイルをドラッグ＆ドロップ");

      // ドラッグ＆ドロップの処理を行う
      HandleDragAndDrop(dropArea);
    }

    // ドラッグ＆ドロップの処理
    private void HandleDragAndDrop(Rect dropArea)
    {
      Event evt = Event.current;

      // マウスがドロップエリア内にあるか確認
      if (!dropArea.Contains(evt.mousePosition))
        return;

      // イベントタイプによって処理を分岐
      switch (evt.type)
      {
        case EventType.DragUpdated:
        case EventType.DragPerform:
          // ドラッグ＆ドロップの見た目を変更
          DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

          if (evt.type == EventType.DragPerform)
          {
            DragAndDrop.AcceptDrag();
            AddAnimationClips(DragAndDrop.objectReferences);
            evt.Use(); // イベントを使用済みにする
          }
          break;
      }
    }

    // アニメーションクリップの追加
    private void AddAnimationClips(Object[] objects)
    {
      foreach (var obj in objects)
      {
        if (obj is AnimationClip clip)
        {
          if (!animationClips.Contains(clip))
            animationClips.Add(clip);
          // Noop
          // else
          //   ShowDuplicateClipWarning(clip);
        }
      }
    }

    private void ShowDuplicateClipWarning(AnimationClip clip)
    {
      EditorUtility.DisplayDialog("注意", $"アニメーションクリップ '{clip.name}' は既にリストに追加されています。", "OK");
    }

    // アニメーションクリップリストの描画
    private void DrawAnimationClipList()
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("アニメーションリスト", EditorStyles.boldLabel);

      // リストの高さを計算
      int itemCount = animationClips.Count;
      float itemHeight = EditorGUIUtility.singleLineHeight + 4f;
      float maxListHeight = 200f;
      float listHeight = Mathf.Min(itemCount * itemHeight, maxListHeight);

      // スクロールビューの開始
      scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(listHeight + 10f));

      // 各アニメーションクリップを描画
      for (int i = 0; i < animationClips.Count; i++)
      {
        DrawAnimationClipItem(i);
      }

      EditorGUILayout.EndScrollView(); // スクロールビューの終了
    }

    // 個々のアニメーションクリップアイテムの描画
    private void DrawAnimationClipItem(int index)
    {
      EditorGUILayout.BeginHorizontal(); // 水平レイアウトの開始

      // アニメーションクリップのフィールドを描画
      AnimationClip oldClip = animationClips[index];
      AnimationClip newClip = (AnimationClip)EditorGUILayout.ObjectField(animationClips[index], typeof(AnimationClip), false);

      // クリップが変更された場合の処理
      if (newClip != oldClip)
        UpdateAnimationClipAt(index, newClip);

      // 削除ボタン
      if (GUILayout.Button("-", GUILayout.Width(30)))
      {
        animationClips.RemoveAt(index);
        return;
      }

      EditorGUILayout.EndHorizontal(); // 水平レイアウトの終了

      // 直前に描画した領域を取得し、ドラッグ＆ドロップ処理を設定
      Rect lastRect = GUILayoutUtility.GetLastRect();
      HandleDragAndDropOnItem(lastRect, index);
    }

    // アニメーションクリップの更新と重複チェック
    private void UpdateAnimationClipAt(int index, AnimationClip newClip)
    {
      if (newClip != null && animationClips.Contains(newClip))
      {
        // 重複している場合は注意を表示
        ShowDuplicateClipWarning(newClip);
      }
      else
      {
        // 重複していない場合はリストを更新
        animationClips[index] = newClip;
      }
    }

    // 個々のアイテムへのドラッグ＆ドロップ処理
    private void HandleDragAndDropOnItem(Rect dropArea, int index)
    {
      Event evt = Event.current;

      // マウスがアイテム領域内にあるか確認
      if (!dropArea.Contains(evt.mousePosition))
        return;

      // イベントタイプによって処理を分岐
      switch (evt.type)
      {
        case EventType.DragUpdated:
        case EventType.DragPerform:
          // ドラッグ＆ドロップの見た目を変更
          DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

          if (evt.type == EventType.DragPerform)
          {
            DragAndDrop.AcceptDrag();
            ReplaceAnimationClipAt(index, DragAndDrop.objectReferences);
            evt.Use(); // イベントを使用済みにする
          }
          break;
      }
    }

    // アニメーションクリップの置き換え
    private void ReplaceAnimationClipAt(int index, Object[] objects)
    {
      foreach (var obj in objects)
      {
        if (obj is AnimationClip clip)
        {
          if (!animationClips.Contains(clip))
            animationClips[index] = clip; // 新しいクリップで置き換え
          else if (animationClips[index] != clip)
            ShowDuplicateClipWarning(clip); // 重複注意
        }
      }
    }

    // SkinnedMeshRendererのパスフィールドの描画
    private void DrawSkinnedMeshRendererPathField()
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("SkinnedMeshRendererのパス（名前）を指定", EditorStyles.boldLabel);

      // パスを入力するテキストフィールドを描画
      skinnedMeshRendererPath = EditorGUILayout.TextField("パス（名前）:", skinnedMeshRendererPath);
    }

    // BlendShapeの値取得オプションの描画
    private void DrawReferenceGameObjectOption()
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("BlendShapeの値の設定", EditorStyles.boldLabel);

      // オプションのトグルを描画
      useReferenceGameObject = EditorGUILayout.ToggleLeft("指定したGameObjectのBlendShapeの値を使用する", useReferenceGameObject);

      // オプションが有効な場合はGameObjectのフィールドを表示
      if (useReferenceGameObject)
        referenceGameObject = (GameObject)EditorGUILayout.ObjectField("GameObject:", referenceGameObject, typeof(GameObject), true);
    }

    // 保存オプションの描画
    private void DrawSaveOptions()
    {
      EditorGUILayout.Space();
      EditorGUILayout.LabelField("保存オプション", EditorStyles.boldLabel);

      // 上書き保存のオプションを描画
      overwriteExistingFiles = EditorGUILayout.ToggleLeft("元のファイルを上書きする", overwriteExistingFiles);

      // 上書きしない場合は新しい保存先を指定するフィールドを表示
      if (!overwriteExistingFiles)
        DrawNewFilePathField();
    }

    // 新しいファイルパスフィールドの描画
    private void DrawNewFilePathField()
    {
      EditorGUILayout.BeginHorizontal();

      // ラベルとテキストフィールドを水平に配置
      EditorGUILayout.LabelField("新しいファイルの保存先:", GUILayout.Width(150));
      newFilePath = EditorGUILayout.TextField(newFilePath);

      // フォルダ選択ボタン
      if (GUILayout.Button("選択", GUILayout.Width(50)))
        SelectNewFilePath();

      EditorGUILayout.EndHorizontal();
    }

    // 新しいファイルパスの選択ダイアログを表示
    private void SelectNewFilePath()
    {
      string selectedPath = EditorUtility.OpenFolderPanel("保存先を選択", "", "");

      if (string.IsNullOrEmpty(selectedPath))
        return;

      // 選択されたパスがプロジェクト内か確認
      if (selectedPath.StartsWith(Application.dataPath))
        newFilePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
      else
        EditorUtility.DisplayDialog("エラー", "保存先はプロジェクトのAssetsフォルダ内を指定してください。", "OK");
    }

    // 実行ボタンの描画
    private void DrawExecuteButton()
    {
      EditorGUILayout.Space();

      // アニメーションクリップが指定されている場合のみボタンを有効化
      GUI.enabled = animationClips.Count > 0 && animationClips.Exists(clip => clip != null);

      // 実行ボタン
      if (GUILayout.Button("実行"))
        CalculateMissingBlendShapes();

      GUI.enabled = true; // GUIの状態を元に戻す
    }

    // 変更点の計算と確認ウィンドウの表示
    private void CalculateMissingBlendShapes()
    {
      // 入力の検証
      if (!ValidateInputs())
        return;

      // データを確認ウィンドウに渡す
      MissingBlendShapeInserterConfirmationWindow.SetData(
          animationClips,
          skinnedMeshRendererPath,
          overwriteExistingFiles,
          newFilePath,
          useReferenceGameObject,
          referenceGameObject);

      // 確認ウィンドウを表示
      MissingBlendShapeInserterConfirmationWindow.ShowWindow();
    }

    // 入力の検証
    private bool ValidateInputs()
    {
      // SkinnedMeshRendererのパスが指定されているか確認
      if (string.IsNullOrEmpty(skinnedMeshRendererPath))
      {
        EditorUtility.DisplayDialog("エラー", "SkinnedMeshRendererのパス（名前）を指定してください。", "OK");
        return false;
      }

      // 参照GameObjectが必要な場合の確認
      if (useReferenceGameObject && referenceGameObject == null)
      {
        EditorUtility.DisplayDialog("エラー", "BlendShapeの値を取得するGameObjectを指定してください。", "OK");
        return false;
      }

      // 新しいファイルパスが必要な場合の確認
      if (!overwriteExistingFiles && string.IsNullOrEmpty(newFilePath))
      {
        EditorUtility.DisplayDialog("エラー", "新しいファイルの保存先を指定してください。", "OK");
        return false;
      }

      return true;
    }
  }
}
