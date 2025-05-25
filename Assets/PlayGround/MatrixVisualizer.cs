using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MatrixVisualizer : MonoBehaviour
{
    private GameObject Parent = null;
    public FloorPlanSettings inputSettings;

    public FloorPlanGenerator FPG_instance;
    public enum OutputTarget { };
    public int xsize, ysize;
    
    
    

    private int[,] CreateFootprint(int x, int y)
    {
        // �z��T�C�Y������������ꍇ�i�O���ȊO�𖄂߂邽�߂ɍŒ�3x3���K�v�j�̃G���[����
        if (x <= 2 || y <= 2)
        {
            Debug.LogError("Footprint size must be at least 3x3 for this generation method.");
            return null; // �����ȏꍇ��null��Ԃ�
        }

        int[,] footprint = new int[x, y];

        // �@ �z���������(�����΂�O���̕����ȊO��1�Ŗ��߂�)
        for (int i = 0; i < x; i++)
        {
            for (int j = 0; j < y; j++)
            {
                // i��0�i��ԍ��j�A�܂���x-1�i��ԉE�j�A
                // �܂��� j��0�i��ԏ�j�A�܂���y-1�i��ԉ��j�̏ꍇ��0�̂܂�
                // ����ȊO�̓����̃}�X��1�Ŗ��߂�
                if (i > 0 && i < x - 1 && j > 0 && j < y - 1)
                {
                    footprint[i, j] = 1; // 1 = ������z�u�\�ȃG���A
                }
                // �O���̃}�X�� int �̃f�t�H���g�l�ł��� 0 �̂܂܂ɂȂ�܂��B
                // 0 = �����O/��/�g�p�s��
            }
        }

        // �A ��񂾂�4�`9���x��0�̒����`�������_���ȃ}�X�ɐ�������
        // �����`�̕��ƍ��������� (���ꂼ��2�܂���3�ɂȂ�悤�ɑI��)
        // Random.Range(min, max) ��min���܂�max���܂܂Ȃ����߁A[2, 3] �͈̔͂ɂ���ɂ� 2, 4 ���w�肵�܂��B
        int rectWidth = UnityEngine.Random.Range(2, 4);
        int rectHeight = UnityEngine.Random.Range(2, 4);

        // �����`�̍�����̊J�n�ʒu�������_���Ɍ���
        // �����`���z����Ɋ��S�Ɏ��܂�悤�ɁA�J�n�ʒu�̃����_���Ȕ͈͂𒲐����܂��B
        // x�����̊J�n�ʒu: 0 ���� (�z��̕� - �����`�̕�) �܂ł͈̔�
        // y�����̊J�n�ʒu: 0 ���� (�z��̍��� - �����`�̍���) �܂ł͈̔�
        int rectStartX = UnityEngine.Random.Range(0, x - rectWidth + 1);
        int rectStartY = UnityEngine.Random.Range(0, y - rectHeight + 1);

        // ���肵�������`�͈̔͂�0�Ŗ��߂� (���Ƃ��Đݒ�)
        for (int i = 0; i < rectWidth; i++)
        {
            for (int j = 0; j < rectHeight; j++)
            {
                // �v�Z�����ʒu (rectStartX + i, rectStartY + j) ���z��͈͓̔��ł��邱�Ƃ��m�F���Ă��܂����A
                // rectStartX �� rectStartY �͈̔͂�K�؂Ɍv�Z���Ă��邽�߁A��ɔ͈͓��ɂȂ�܂��B
                footprint[rectStartX + i, rectStartY + j] = 0; // 0 = �����O/��/�g�p�s�� (���̃C���[�W)
            }
        }

        // �B return
        return footprint;
    }
    private void PlaceCube(Vector3 Position, float weight)
    {
        // �L���[�u�̍쐬
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(Parent.transform);
        cube.transform.position = Position + new Vector3 (0.5f, 0f, 0.5f);
        // �L���[�u��Renderer�R���|�[�l���g���擾
        // PrimitiveType.Cube �ō쐬���ꂽGameObject�ɂ�Renderer�R���|�[�l���g�������I�ɃA�^�b�`����܂�
        Renderer renderer = cube.GetComponent<Renderer>();

        // weight�̒l�Ɋ�Â��ĐF�����肵�A�}�e���A���ɐݒ�
        // weight�̎�肤���̓I�Ȓl�͈̔͂��s���Ȃ��߁A�����ł͊ȒP�̂��߂�0����1�͈̔͂ɃN�����v���Ďg�p���܂��B
        // ����weight������͈̔� (��: minWeight ���� maxWeight) �����ꍇ�́A
        // float t = Mathf.InverseLerp(minWeight, maxWeight, weight); �̂悤�ɐ��K�����Ă���Color.Lerp���g���Ɨǂ��ł��傤�B
        float normalizedWeight = Mathf.Clamp01(weight); // weight��0����1�͈̔͂ɃN�����v

        // weight��0�ɋ߂��ق� Color.blue, weight��1�ɋ߂��ق� Color.red �ɂȂ�܂�
        Color cubeColor = Color.Lerp(Color.black, Color.white, normalizedWeight);

        // �L���[�u�̃}�e���A���̐F��ݒ�
        // material �͐V�����C���X�^���X�𐶐����邽�߁A���̃I�u�W�F�N�g�ɉe�����܂���B
        renderer.material.color = cubeColor;

        string text = weight.ToString();
        // TextMeshPro��3D�e�L�X�g�I�u�W�F�N�g���쐬
        GameObject textObj = new GameObject("CubeText");
        textObj.transform.SetParent(cube.transform);

        // �e�L�X�g�̈ʒu�Ɖ�]�𒲐��i�L���[�u�̏�ʂɔz�u�j
        textObj.transform.localPosition = new Vector3(0, 0.51f, 0);
        textObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        // TextMeshPro�R���|�[�l���g��ǉ�
        TextMeshPro textMeshPro = textObj.AddComponent<TextMeshPro>();
        textMeshPro.text = text;
        textMeshPro.fontSize =40;
        textMeshPro.alignment = TextAlignmentOptions.Center;
        textMeshPro.color = Color.black;

        // �e�L�X�g�̃X�P�[���𒲐�
        textObj.transform.localScale = Vector3.one * 0.1f;
    }
    // Start is called before the first frame update
    void Start()
    {
        int[,] footprint = CreateFootprint(xsize, ysize);
        //inputSettings = new FloorPlanSettings(footprint,);
    }
    public void execute(float[,] Matrix)
    {
        if(Parent != null)
        {
            Destroy(Parent);//Mesh�iMeshFilter.sharedMesh�j, Material�iRenderer.sharedMaterial,Texture,TextMeshPro�̃t�H���g�A�Z�b�g��}�e���A���͉������Ȃ�.
        }
        Parent = new GameObject();
        for (int i = 0; i < Matrix.GetLength(0); i++)
        {
            for (int j = 0; j < Matrix.GetLength(1); j++)
            {
                Vector3 vecPos = new Vector3((float)i, 0, (float)j);
                PlaceCube(vecPos, Matrix[i, j]);
            }
        }
    }
    // Update is called once per frame
    void Update()
    {

    }
}
