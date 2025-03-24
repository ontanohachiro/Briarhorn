Shader "Custom/TexNormalAtlas"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Texture", 2D) = "white" {}
        _BumpMap("Bumpmap", 2D) = "bump" {}
        _AtlasX("Atlas X Count", Int) = 1
        _AtlasY("Atlas Y Count", Int) = 1
        _AtlasRec("Atlas Record", Vector) = (1, 1, 0, 0)//アトラス内の各テクスチャの幅と高さを定義.
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 200
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert // 頂点シェーダー関数名,フラグメントシェーダー関数名の指定
            #pragma fragment frag
            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE シェーダーバリアントを増やすための記述.
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            
            struct Attributes// メッシュから受け取るデータ（頂点シェーダーの入力).
            {
                //これらの変数はセマンティクスがついているから、メッシュから自動的に取得される
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv : TEXCOORD0;  // 四次元UV
            };
            // TEXCOORDn は、もともと UV座標（テクスチャ座標）を格納するためのセマンティクス だが、 一般的なカスタムデータの受け渡しにも使われる.
            //TEXCOORDn を使うことで、シェーダーが GPU にデータを効率よく渡せる.
            struct Varyings//頂点シェーダーからフラグメントシェーダーに渡すデータ.
            {
                float4 positionCS : SV_POSITION;//クリップ空間での座標
                float4 custom_uv : TEXCOORD0;//通常 UV 座標は二次元.今回はテクスチャアトラスを用いるためアトラス上でのx,y座標の二つの要素が加えられている.
                float3 normalWS : TEXCOORD1;//ワールド座標系の方向ベクトルなので x, y, z の3成分.
                float3 tangentWS : TEXCOORD2;//ワールド座標系の方向ベクトルなので x, y, z の3成分.
                float3 bitangentWS : TEXCOORD3;//ワールド座標系の方向ベクトルなので x, y, z の3成分.
                float3 positionWS : TEXCOORD4;//ワールド空間の頂点座標.
            };
            
            // TEXTURE2D:テクスチャデータ（画像のピクセル情報）を格納するオブジェクト.
            //SAMPLER:テクスチャのサンプリング方法（フィルタリング、ラッピング設定など）を定義するオブジェクト.
            //Unity では、TEXTURE2D(_MainTex); のように定義すると、シェーダーに対応するマテリアルの _MainTex に設定されたテクスチャが自動的に適用 される.
            //恐らく_MainTex限定.
            //TEXTURE2D だけでは フィルタリングやラッピング設定は適用されない ため、sampler_MainTex は Unity の内部で適切なものを選んでいる.
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            
            //定数バッファ（Constant Buffer） と呼ばれる構造を定義.
            //Unity が提供するマクロで、プロパティが一つのバッファ内にまとめられ、GPU へのデータ転送効率が向上.
            //定数バッファ（Constant Buffer）内で宣言された変数は、シェーダープログラム内で他の変数と同様に使用.
            CBUFFER_START(UnityPerMaterial)//UnityPerMaterialは定数バッファの名前.
                float4 _Color;
                int _AtlasX;
                int _AtlasY;
                float4 _AtlasRec;
            CBUFFER_END
            
            Varyings vert(Attributes input) //Attributes型の情報を受け取り、Varyings型の情報をフラグメントシェーダーに出力する頂点シェーダー.
            {
                Varyings output;
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);//頂点の座標のクリップ空間への座標変換.
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);//頂点の座標のワールド空間への座標変換.
                output.custom_uv = input.uv;  // 四次元UVをそのまま渡す
                
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;//cross - 外積計算（二つのベクトルに垂直なベクトルを求める）.
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target//ピクセルごとの色を計算して返す.SV_TARGET は、フラグメントシェーダーの出力が「描画ターゲット」（通常は画面上のピクセル）であることを示すセマンティクス.
            {
                float2 atlasOffset = input.custom_uv.zw;  // UVの3,4次元目を使用
                float2 scaledUV = input.custom_uv.xy;     // UVの1,2次元目を使用
                float2 atlasUV = scaledUV;
                
                //frac 関数:引数として与えられた数値の小数部分を取得.
                atlasUV.x = (atlasOffset.x * _AtlasRec.x) + frac(atlasUV.x) * _AtlasRec.x;
                atlasUV.y = (((_AtlasY - 1) - atlasOffset.y) * _AtlasRec.y) + frac(atlasUV.y) * _AtlasRec.y;
                
                // テクスチャサンプリング
                //SAMPLE_TEXTURE2D(テクスチャ名, サンプラー名, UV座標);で与えられた UV 座標から色データを取得.
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV) * _Color;
                
                // 法線マップの処理
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, atlasUV));
                float3x3 tangentToWorld = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                normalWS = normalize(normalWS);
                
                // ラッピングされたランバート照明の計算（元のシェーダーの効果を再現）
                Light mainLight = GetMainLight();
                half NdotL = dot(normalWS, mainLight.direction);
                half diff = NdotL * 0.5 + 0.5; // ハーフランバート
                
                half3 color = albedo.rgb * mainLight.color * (diff * mainLight.shadowAttenuation);
                
                // 環境光を追加
                half3 ambient = SampleSH(normalWS) * albedo.rgb;
                color += ambient;
                
                return half4(color, albedo.a);
            }
            ENDHLSL
        }
        
        // シャドウキャスターパス
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };
            
            float3 _LightDirection;
            float4 _ShadowBias; // x: depth bias, y: normal bias
            
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                int _AtlasX;
                int _AtlasY;
                float4 _AtlasRec;
            CBUFFER_END
            
            // ApplyShadowBiasの自前実装
            float3 ApplyShadowBiasImpl(float3 positionWS, float3 normalWS, float3 lightDirection)
            {
                float invNdotL = 1.0 - saturate(dot(normalWS, lightDirection));
                float scale = invNdotL * 0.01; // 通常のバイアス係数
                
                // 位置をライト方向に少しだけずらして影の自己交差を防ぐ
                positionWS = lightDirection * scale + positionWS;
                return positionWS;
            }
            
            float4 GetShadowPositionHClip(float3 positionOS, float3 normalOS)
            {
                // ワールド空間に変換
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                
                // シャドウバイアスの適用
                positionWS = ApplyShadowBiasImpl(positionWS, normalWS, _LightDirection);
                
                // クリップ空間に変換
                float4 positionCS = TransformWorldToHClip(positionWS);
                
                // 深度クランプ処理
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                return positionCS;
            }
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = GetShadowPositionHClip(input.positionOS.xyz, input.normalOS);
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}