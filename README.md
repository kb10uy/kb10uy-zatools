# Zatools: kb10uy's Various Tools

## インストール
2. [kb10uy VRChat Package Repository](https://kb10uy.github.io/vrc-repository/) を追加
3. その中の kb10uy's Various Tools をインストール


## public クラスについて
Unity プロジェクト内での in-house なコンポーネントの実装に供するため、一部が public クラスとしてアセンブリ外に公開されます。
リリースサイクルにおいては、これらのクラスのうち `[PublicAPI]` が付与されているものだけが実際の API surface の対象となります。
そうでないクラスはマイナー・パッチリリースにおいても予告なくシグネチャ等が変更される場合がありますので、第三者に提供するツールで使用する際はご注意ください。


## ライセンス
* Resources/Embedded 以下のアセット群には zlib License が適用されます。
* それ以外のファイルは Apache License 2.0 / MIT License のデュアルライセンスが適用されます。
