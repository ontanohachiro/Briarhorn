using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// �X�e�b�v1: �����̏����V�[�h�ʒu��z�u 
    /// </summary>
    private bool PlaceInitialSeeds(List<RoomDefinition> roomsToPlace)
    {
        Debug.Log("Placing initial seeds...");
        Dictionary<int, float[,]> weightGrids = InitializeWeightGrids();//�e�����ɂ��Ă̏d�݃}�b�v.
        float[,] _distanceToWall = CalculateDistanceToWall(); // ���O�Ɍv�Z���Ă����ƌ����I.�ǂ̕����̏d�݃}�b�v�ɂ��Ă�����.
        // �e�����̃V�[�h��z�u (ID���Ŕz�u���Ă݂�)
        // �z�u���������ʂɉe����^����\������B�����_���������̏����i��F�傫�ȕ�������j�������\�B
        foreach (var kvp in _roomDefinitions.OrderBy(kv => kv.Key)) // ID���ɏ���
        {
            int roomId = kvp.Key;
            RoomDefinition room = kvp.Value;

            // 1. ���̕����p�̏d�݃O���b�h���v�Z
            CalculateWeightsForRoom(roomId, weightGrids, _distanceToWall);

            // 2. �œK�ȃV�[�h�ʒu��I��
            Vector2Int? seedPos = SelectBestSeedPosition(weightGrids[roomId]);

            if (seedPos.HasValue)
            {
                room.InitialSeedPosition = seedPos.Value;
                _grid[seedPos.Value.x, seedPos.Value.y] = roomId; // �O���b�h�ɔz�u
                room.CurrentSize = 1; // �V�[�h�Z���ŃT�C�Y1
                Debug.Log($"Placed seed for room {roomId} ({room.Type}) at {seedPos.Value}");


                // 3. �z�u�����Z���̎��͂̏d�݂𑼂̕����̃O���b�h�ŉ�����
                UpdateWeightsAroundSeed(weightGrids, seedPos.Value, roomId);
            }
            else
            {
                Debug.LogError($"Could not find suitable seed position for room {roomId} ({room.Type}). Not enough space or constraints too strict?");
                // �z�u���s�B���Z�b�g���Ă�蒼�����A���̎��s�����s�Ƃ���B
                ResetGridAndSeeds(); // �O���b�h�ƃV�[�h�������Z�b�g
                return false;
            }
        }
        Debug.Log("Initial seeds placed successfully.");
        return true;
    }


    /// <summary>
    /// �S�Ă̕���ID�ɑ΂���d�݃O���b�h������������,1-0
    /// </summary>
    private Dictionary<int, float[,]> InitializeWeightGrids()
    {
        Dictionary<int, float[,]> weightGrids = new Dictionary<int, float[,]>();
        foreach (int roomId in _roomDefinitions.Keys)
        {
            weightGrids[roomId] = new float[_gridSize.x, _gridSize.y];
            // �����l�� 0 �ł��ǂ����A��̌v�Z�ŏ㏑�������
        }
        return weightGrids;
    }

    /// <summary>
    /// ����̕���ID�ɂ��Ă̏d�݃O���b�h���v�Z����,1-1
    /// </summary>
    private void CalculateWeightsForRoom(int targetRoomId, Dictionary<int, float[,]> weightGrids, float[,] distanceToWall)
    {
        float[,] weights = weightGrids[targetRoomId];
        RoomDefinition targetRoom = _roomDefinitions[targetRoomId];


        // �ڕW�ʐς���u���z�I�ȁv�����𐄒� (�������ł����悻�̈�ӂ̒���)
        float estimatedTargetCells = _totalPlaceableCells * (targetRoom.SizeRatio / _roomDefinitions.Values.Sum(r => r.SizeRatio));
        // 0���h�~
        if (estimatedTargetCells <= 0) estimatedTargetCells = 1;
        float idealDistanceFromWall = Mathf.Sqrt(estimatedTargetCells) * settings.SeedPlacementWeightDistanceFactor; // �W���Œ���.�T�C�Y�̕�������1/2

        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                // 0. �z�u�s�\�ȏꏊ�A���ɃV�[�h���u���ꂽ�ꏊ�͏d��0
                if (_grid[x, y] != -1)
                {
                    weights[x, y] = 0f;
                    continue;
                }
                //����������_grid[x, y] == -1�̂ݓ��B�\.

                // 1. �����d�ݐݒ� (����������1) 
                float currentWeight = 1.0f; // �z�u�\�Ȃ��{1

                // 2. �O�ǂ���̋����Ɋ�Â��ďd�݂𒲐�
                float dist = distanceToWall[x, y];
                if (dist >= 0) // �����Z��
                {
                    // �ǂɋ߂��قǏd�݂����炷�B���z�����Ńs�[�N�ɂȂ�悤�ɒ������\�B
                    // ��: ���`���� (�Ǎ�0, idealDistance�ȏ��1)
                    currentWeight *= Mathf.Clamp01(dist / idealDistanceFromWall);//0����1�̒l��Ԃ�.
                    // ��: �K�E�V�A���I�ȕ��z (idealDistance�ōő�A1�ƂȂ�)
                    // currentWeight *= Mathf.Exp(-(dist - idealDistanceFromWall) * (dist - idealDistanceFromWall));
                }
                else
                {
                    currentWeight = 0f; // �O�̂��߁iInitializeGrid��0�ɂȂ��Ă���͂��j
                }


                // 3. �אڐ���Ɋ�Â��ďd�݂𒲐�
                if (targetRoom.ConnectivityConstraints != null)
                {
                    foreach (int constraintRoomId in targetRoom.ConnectivityConstraints)
                    {
                        // ���ݐڑ�����̏ꍇ�A���葤�ɂ����񂪂��邩�m�F���������ǂ����A�����ł͕Е����ōl��.�^�[�Q�b�g�̕��������݂��Ă��āA���ʒu���m�肵�Ă��邩.
                        if (_roomDefinitions.ContainsKey(constraintRoomId) && _roomDefinitions[constraintRoomId].InitialSeedPosition.HasValue)
                        {
                            Vector2Int neighborSeedPos = _roomDefinitions[constraintRoomId].InitialSeedPosition.Value;
                            // �אڕ����̃V�[�h�ʒu�ɋ߂��قǏd�݂����Z
                            float distanceToNeighborSeed = Vector2Int.Distance(new Vector2Int(x, y), neighborSeedPos);
                            // ��: ���͈͓��Ȃ�{�[�i�X��������
                            if (distanceToNeighborSeed < idealDistanceFromWall) // �͈͂͒����\
                            {
                                // �������߂��قǑ傫�ȃ{�[�i�X
                                currentWeight += settings.SeedPlacementAdjacencyBonus * (1.0f - Mathf.Clamp01(distanceToNeighborSeed / (idealDistanceFromWall)));
                            }
                        }
                    }
                }

                // �d�݂͕��ɂȂ�Ȃ��悤��.
                //weights[x, y] = Mathf.Max(0, currentWeight); 
            }
        }
    }


    /// <summary>
    /// �e�����Z������ł��߂��O���Z��(��/���E)�܂ł̋������v�Z���� (BFS�x�[�X)
    /// �O���Z���ⓞ�B�s�\�Z���� -1 ��Ԃ�,1-1-1
    /// </summary>
    public float[,] CalculateDistanceToWall()
    {
        float[,] distances = new float[_gridSize.x, _gridSize.y];
        Queue<Vector2Int> queue = new Queue<Vector2Int>();//������o��.
        //BFS:(1)�J�n���_���L���[�ɓ����.(2)�L���[�̐擪�ɂ��钸�_�ɗאڂ��Ă��ĒT���ς݂łȂ����_���A�L���[�̐擪�̒��_�̂��̂�1(�������̓����N�Ɉˑ���������)�������������蓖�āA�L���[�ɓ����.
        //(3) (2)���J��Ԃ�.
        // ������: �����Z���� MaxValue�A�O���Z���� 0
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (settings.InputFootprintGrid[x, y] == 0) // �O���Z��(��)
                {
                    distances[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                }
                else
                {
                    distances[x, y] = float.MaxValue;
                }
            }
        }

        //�΂߂��l������
        Vector2Int[] neighbors = {
            new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                                                    };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            float currentDist = distances[current.x, current.y];

            foreach (var offset in neighbors)
            {
                Vector2Int neighbor = current + offset;

                if (neighbor.x >= 0 && neighbor.x < _gridSize.x && neighbor.y >= 0 && neighbor.y < _gridSize.y)
                {
                    // �΂߂��l������ꍇ�A�����𒲐�
                    float newDist = currentDist + (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) > 1 ? 1.414f : 1.0f); // �΂߂͖�1.4�{.�񍀉��Z�q,true�Ȃ獶�Afalse�Ȃ�E.
                    //float newDist = currentDist + 1.0f; // 4�����݂̂̏ꍇ

                    if (distances[neighbor.x, neighbor.y] > newDist)
                    {
                        distances[neighbor.x, neighbor.y] = newDist;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // MaxValue�̂܂܂̃Z���i���������ǂ��瓞�B�s�\�A�܂��̓O���b�h�O�����j�� -1 �ɂ���
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (distances[x, y] == float.MaxValue)
                {
                    distances[x, y] = -1f;
                }
            }
        }


        return distances;
    }


    /// <summary>
    /// �d�݃O���b�h����ł��K�؂ȃV�[�h�ʒu��I������ ,1-2
    /// </summary>
    private Vector2Int? SelectBestSeedPosition(float[,] weightGrid)
    {
        float maxWeight = -1f;
        List<Vector2Int> bestPositions = new List<Vector2Int>();

        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                // �z�u�\(-1) ���� �d�݂��v�Z����Ă���Z���̂ݑΏ�
                if (_grid[x, y] == -1 && weightGrid[x, y] >= 0)
                {
                    if (weightGrid[x, y] > maxWeight)
                    {
                        maxWeight = weightGrid[x, y];
                        bestPositions.Clear();
                        bestPositions.Add(new Vector2Int(x, y));
                    }
                    else if (weightGrid[x, y] == maxWeight)
                    {
                        bestPositions.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        if (bestPositions.Count > 0)
        {
            // �ō��d�݂̌�₪��������΃����_���ɑI��
            return bestPositions[_random.Next(bestPositions.Count)];
        }
        else
        {
            return null; // �K�؂Ȉʒu��������Ȃ�
        }
    }

    /// <summary>
    /// �z�u���ꂽ�V�[�h�̎��͂̃Z���̏d�݂��A���̕����̃O���b�h�ŉ�����,1-3
    /// </summary>
    private void UpdateWeightsAroundSeed(Dictionary<int, float[,]> weightGrids, Vector2Int seedPos, int placedRoomId)
    {
        int radius = settings.SeedExclusionRadius; // �ݒ肩��擾

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                // �~�`�͈̔͂ɂ���ꍇ (�I�v�V����).���̂܂܂��Ǝl�p�`�ɂȂ�.
                // if (dx * dx + dy * dy > radius * radius) continue;

                Vector2Int pos = seedPos + new Vector2Int(dx, dy);

                if (pos.x >= 0 && pos.x < _gridSize.x && pos.y >= 0 && pos.y < _gridSize.y)
                {
                    // ���̑S�Ă̕����̏d�݃O���b�h�ɑ΂��ď���
                    foreach (var kvp in weightGrids)
                    {
                        int otherRoomId = kvp.Key;
                        if (otherRoomId != placedRoomId) // �z�u�����������g�̃O���b�h�͕ύX���Ȃ�
                        {
                            kvp.Value[pos.x, pos.y] = float.MinValue; // �d�݂�啝�ɉ�����.
                        }
                    }
                }
            }
        }
        // Debug.Log($"Updated weights around {seedPos} for other rooms.");
    }


    /// <summary>
    /// PlaceInitialSeeds�����s�����ꍇ�ȂǂɃO���b�h�ƃV�[�h����������Ԃɖ߂�,1-4
    /// </summary>
    private void ResetGridAndSeeds()
    {
        InitializeGrid(); // �O���b�h��-1��0�̏�Ԃɖ߂�
        foreach (var room in _roomDefinitions.Values)
        {
            room.InitialSeedPosition = null;
            room.CurrentSize = 0;
            // Bounds�����Z�b�g���K�v�Ȃ�s��
        }
        Debug.Log("Grid and seeds reset.");
    }

}
