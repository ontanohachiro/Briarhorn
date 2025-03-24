Shader "Custom/TexNormalAtlas"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Texture", 2D) = "white" {}
        _BumpMap("Bumpmap", 2D) = "bump" {}
        _AtlasX("Atlas X Count", Int) = 1
        _AtlasY("Atlas Y Count", Int) = 1
        _AtlasRec("Atlas Record", Vector) = (1, 1, 0, 0)//�A�g���X���̊e�e�N�X�`���̕��ƍ������`.
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
            #pragma vertex vert // ���_�V�F�[�_�[�֐���,�t���O�����g�V�F�[�_�[�֐����̎w��
            #pragma fragment frag
            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE �V�F�[�_�[�o���A���g�𑝂₷���߂̋L�q.
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            
            struct Attributes// ���b�V������󂯎��f�[�^�i���_�V�F�[�_�[�̓���).
            {
                //�����̕ϐ��̓Z�}���e�B�N�X�����Ă��邩��A���b�V�����玩���I�Ɏ擾�����
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv : TEXCOORD0;  // �l����UV
            };
            // TEXCOORDn �́A���Ƃ��� UV���W�i�e�N�X�`�����W�j���i�[���邽�߂̃Z�}���e�B�N�X �����A ��ʓI�ȃJ�X�^���f�[�^�̎󂯓n���ɂ��g����.
            //TEXCOORDn ���g�����ƂŁA�V�F�[�_�[�� GPU �Ƀf�[�^�������悭�n����.
            struct Varyings//���_�V�F�[�_�[����t���O�����g�V�F�[�_�[�ɓn���f�[�^.
            {
                float4 positionCS : SV_POSITION;//�N���b�v��Ԃł̍��W
                float4 custom_uv : TEXCOORD0;//�ʏ� UV ���W�͓񎟌�.����̓e�N�X�`���A�g���X��p���邽�߃A�g���X��ł�x,y���W�̓�̗v�f���������Ă���.
                float3 normalWS : TEXCOORD1;//���[���h���W�n�̕����x�N�g���Ȃ̂� x, y, z ��3����.
                float3 tangentWS : TEXCOORD2;//���[���h���W�n�̕����x�N�g���Ȃ̂� x, y, z ��3����.
                float3 bitangentWS : TEXCOORD3;//���[���h���W�n�̕����x�N�g���Ȃ̂� x, y, z ��3����.
                float3 positionWS : TEXCOORD4;//���[���h��Ԃ̒��_���W.
            };
            
            // TEXTURE2D:�e�N�X�`���f�[�^�i�摜�̃s�N�Z�����j���i�[����I�u�W�F�N�g.
            //SAMPLER:�e�N�X�`���̃T���v�����O���@�i�t�B���^�����O�A���b�s���O�ݒ�Ȃǁj���`����I�u�W�F�N�g.
            //Unity �ł́ATEXTURE2D(_MainTex); �̂悤�ɒ�`����ƁA�V�F�[�_�[�ɑΉ�����}�e���A���� _MainTex �ɐݒ肳�ꂽ�e�N�X�`���������I�ɓK�p �����.
            //���炭_MainTex����.
            //TEXTURE2D �����ł� �t�B���^�����O�⃉�b�s���O�ݒ�͓K�p����Ȃ� ���߁Asampler_MainTex �� Unity �̓����œK�؂Ȃ��̂�I��ł���.
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            
            //�萔�o�b�t�@�iConstant Buffer�j �ƌĂ΂��\�����`.
            //Unity ���񋟂���}�N���ŁA�v���p�e�B����̃o�b�t�@���ɂ܂Ƃ߂��AGPU �ւ̃f�[�^�]������������.
            //�萔�o�b�t�@�iConstant Buffer�j���Ő錾���ꂽ�ϐ��́A�V�F�[�_�[�v���O�������ő��̕ϐ��Ɠ��l�Ɏg�p.
            CBUFFER_START(UnityPerMaterial)//UnityPerMaterial�͒萔�o�b�t�@�̖��O.
                float4 _Color;
                int _AtlasX;
                int _AtlasY;
                float4 _AtlasRec;
            CBUFFER_END
            
            Varyings vert(Attributes input) //Attributes�^�̏����󂯎��AVaryings�^�̏����t���O�����g�V�F�[�_�[�ɏo�͂��钸�_�V�F�[�_�[.
            {
                Varyings output;
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);//���_�̍��W�̃N���b�v��Ԃւ̍��W�ϊ�.
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);//���_�̍��W�̃��[���h��Ԃւ̍��W�ϊ�.
                output.custom_uv = input.uv;  // �l����UV�����̂܂ܓn��
                
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;//cross - �O�όv�Z�i��̃x�N�g���ɐ����ȃx�N�g�������߂�j.
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target//�s�N�Z�����Ƃ̐F���v�Z���ĕԂ�.SV_TARGET �́A�t���O�����g�V�F�[�_�[�̏o�͂��u�`��^�[�Q�b�g�v�i�ʏ�͉�ʏ�̃s�N�Z���j�ł��邱�Ƃ������Z�}���e�B�N�X.
            {
                float2 atlasOffset = input.custom_uv.zw;  // UV��3,4�����ڂ��g�p
                float2 scaledUV = input.custom_uv.xy;     // UV��1,2�����ڂ��g�p
                float2 atlasUV = scaledUV;
                
                //frac �֐�:�����Ƃ��ė^����ꂽ���l�̏����������擾.
                atlasUV.x = (atlasOffset.x * _AtlasRec.x) + frac(atlasUV.x) * _AtlasRec.x;
                atlasUV.y = (((_AtlasY - 1) - atlasOffset.y) * _AtlasRec.y) + frac(atlasUV.y) * _AtlasRec.y;
                
                // �e�N�X�`���T���v�����O
                //SAMPLE_TEXTURE2D(�e�N�X�`����, �T���v���[��, UV���W);�ŗ^����ꂽ UV ���W����F�f�[�^���擾.
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV) * _Color;
                
                // �@���}�b�v�̏���
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, atlasUV));
                float3x3 tangentToWorld = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                normalWS = normalize(normalWS);
                
                // ���b�s���O���ꂽ�����o�[�g�Ɩ��̌v�Z�i���̃V�F�[�_�[�̌��ʂ��Č��j
                Light mainLight = GetMainLight();
                half NdotL = dot(normalWS, mainLight.direction);
                half diff = NdotL * 0.5 + 0.5; // �n�[�t�����o�[�g
                
                half3 color = albedo.rgb * mainLight.color * (diff * mainLight.shadowAttenuation);
                
                // ������ǉ�
                half3 ambient = SampleSH(normalWS) * albedo.rgb;
                color += ambient;
                
                return half4(color, albedo.a);
            }
            ENDHLSL
        }
        
        // �V���h�E�L���X�^�[�p�X
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
            
            // ApplyShadowBias�̎��O����
            float3 ApplyShadowBiasImpl(float3 positionWS, float3 normalWS, float3 lightDirection)
            {
                float invNdotL = 1.0 - saturate(dot(normalWS, lightDirection));
                float scale = invNdotL * 0.01; // �ʏ�̃o�C�A�X�W��
                
                // �ʒu�����C�g�����ɏ����������炵�ĉe�̎��Ȍ�����h��
                positionWS = lightDirection * scale + positionWS;
                return positionWS;
            }
            
            float4 GetShadowPositionHClip(float3 positionOS, float3 normalOS)
            {
                // ���[���h��Ԃɕϊ�
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                
                // �V���h�E�o�C�A�X�̓K�p
                positionWS = ApplyShadowBiasImpl(positionWS, normalWS, _LightDirection);
                
                // �N���b�v��Ԃɕϊ�
                float4 positionCS = TransformWorldToHClip(positionWS);
                
                // �[�x�N�����v����
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