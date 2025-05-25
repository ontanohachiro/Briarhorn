using QuikGraph; // QuikGraph �̖��O��Ԃ��g�p���܂��B
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


// --- �X�e�b�v3: DetermineConnectivity �Ƃ��̕⏕�֐� ---
public partial class FloorPlanGenerator : MonoBehaviour
{
    /// <summary>
    /// �����Ԃ̐ڑ����i�h�A�̔z�u�j�����肷��֐��B
    /// </summary>
    /// <param name="roomsToPlace">�z�u�Ώۂ̕�����`�̃��X�g�i���݂͒��ڎg�p���Ă��܂��񂪁A�����̊g�������l�����Ďc���Ă��܂��j�B</param>
    /// <param name="connectivityGraph">�����Ԃ̕K�{�ڑ��������O���t�B</param>
    /// <returns>�z�u���ꂽ�h�A�̃��X�g�B�K�{�ڑ����������Ȃ��ꍇ��null��Ԃ����Ƃ�����܂��B</returns>
    private List<Door> DetermineConnectivity(List<RoomDefinition> roomsToPlace, AdjacencyGraph<int, Edge<int>> connectivityGraph)
    {
        Debug.Log("Determining connectivity..."); // ���O�F�ڑ������菈���̊J�n�B
        List<Door> doors = new List<Door>(); // �z�u�����h�A���i�[���郊�X�g�����������܂��B
        HashSet<Tuple<int, int>> connectionsMade = new HashSet<Tuple<int, int>>(); // (id1, id2) where id1 < id2 ���ɐڑ����쐬���ꂽ�����̃y�A���L�^����HashSet�B�d���ڑ���h���܂��B

        // --- 1. ���̓O���t�Ɋ�Â��K�{�ڑ� ---
        // �񋟂��ꂽ�ڑ��O���t�����݂��A���̃G�b�W���X�g�����݂���ꍇ�ɏ������s���܂��B
        if (connectivityGraph != null && connectivityGraph.Edges != null)
        {
            foreach (var edge in connectivityGraph.Edges) // �O���t���̊e�G�b�W�i�K�{�ڑ��j�ɂ��ď������܂��B
            {
                int roomID1 = edge.Source; // �G�b�W�̎n�_�ƂȂ镔����ID�B
                int roomID2 = edge.Target; // �G�b�W�̏I�_�ƂȂ镔����ID�B

                // �����ȃG�b�W�i���ȃ��[�v�⑶�݂��Ȃ�����ID�ւ̐ڑ��j�̓X�L�b�v���܂��B
                if (roomID1 == roomID2 || !_roomDefinitions.ContainsKey(roomID1) || !_roomDefinitions.ContainsKey(roomID2))
                {
                    // Debug.LogWarning($"Skipping invalid or self-connecting edge from graph: {roomID1} -> {roomID2}"); // �f�o�b�O���O�i�R�����g�A�E�g����Ă��܂��j�B
                    continue; // ���̃G�b�W�ցB
                }

                // ����ID�̃y�A���쐬���AID������������Item1�ɂ��邱�Ƃň�Ӑ���ۂ��܂��B
                var pair = roomID1 < roomID2 ? Tuple.Create(roomID1, roomID2) : Tuple.Create(roomID2, roomID1);
                if (connectionsMade.Contains(pair)) continue; // ���ɂ��̃y�A�Ԃ̐ڑ����쐬����Ă���΃X�L�b�v���܂��B

                // 2�̕����ԂɃh�A��z�u���悤�Ǝ��݂܂��B
                if (PlaceDoorBetweenRooms(roomID1, roomID2, doors))
                {
                    connectionsMade.Add(pair); // �ڑ����������ꍇ�A�쐬�ς݃Z�b�g�ɒǉ����܂��B
                    Debug.Log($"Placed required door between {roomID1} and {roomID2}"); // ���O�F�K�{�h�A�̔z�u�����B
                }
                else
                {
                    // �K�{�ڑ����אڂ��Ă��Ȃ����ߔz�u�ł��Ȃ������ꍇ�̏����B
                    Debug.LogError($"REQUIRED connection between {roomID1} and {roomID2} FAILED. Rooms are not adjacent."); // �G���[���O�F�K�{�ڑ��̎��s�B
                    // �����Ő������s�Ƃ���ׂ����A�v���ɂ��܂��B
                    return null; // �K�{�ڑ����������Ȃ��Ȃ�null��Ԃ��Ď��s�Ƃ��܂��B
                }
            }
        }

        // --- 2. �_���A���S���Y���Ɋ�Â��ڑ����[�� ---

        // 2a. �L��(Hallway)�Ɨאڂ���S�Ẵp�u���b�N(Public)������ڑ����܂��B
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway)) // �^�C�v��Hallway�̕�����S�Ď擾���܂��Bkvp1.Key��RoomDefinition.ID�ł��B
        {
            int hallwayId = kvp1.Key; // �L���̕���ID�B
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public)) // �^�C�v��Public�̕�����S�Ď擾���܂��Bkvp2.Key��RoomDefinition.ID�ł��B
            {
                int publicId = kvp2.Key; // �p�u���b�N������ID�B
                // ����ID�̃y�A���쐬���AID������������Item1�ɂ��邱�Ƃň�Ӑ���ۂ��܂��B
                var pair = hallwayId < publicId ? Tuple.Create(hallwayId, publicId) : Tuple.Create(publicId, hallwayId);
                if (connectionsMade.Contains(pair)) continue; // ���ɐڑ����쐬����Ă���΃X�L�b�v���܂��B

                // �L���ƃp�u���b�N�������אڂ��Ă��邩�m�F���܂��B
                if (AreRoomsAdjacent(hallwayId, publicId))
                {
                    // �h�A��z�u���悤�Ǝ��݂܂��B
                    if (PlaceDoorBetweenRooms(hallwayId, publicId, doors))
                    {
                        connectionsMade.Add(pair); // �ڑ����������ꍇ�A�쐬�ς݃Z�b�g�ɒǉ����܂��B
                        Debug.Log($"Placed Hallway-Public door between {hallwayId} and {publicId}"); // ���O�F�L��-�p�u���b�N�h�A�̔z�u�����B
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to place optional door between Hallway {hallwayId} and Public {publicId} despite adjacency."); // �x�����O�F�אڂ��Ă���ɂ��ւ�炸�h�A�z�u���s�B
                    }
                }
            }
        }

        // 2b. ���ڑ��̃v���C�x�[�g(Private)�������A�אڂ���p�u���b�N�����ɐڑ����܂� (�\�ȏꍇ)�B
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Private)) // �^�C�v��Private�̕�����S�Ď擾���܂��Bkvp1.Key��RoomDefinition.ID�ł��B
        {
            int privateId = kvp1.Key; // �v���C�x�[�g������ID�B
            if (IsRoomConnected(privateId, connectionsMade)) continue; // ���ɂǂ����Ɍq�����Ă���ꍇ�̓X�L�b�v���܂��B

            // �אڂ���p�u���b�N������T���܂��B
            int connectedTo = -1; // �ڑ���̕���ID���L�^����ϐ��B
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public)) // �^�C�v��Public�̕�����S�Ď擾���܂��Bkvp2.Key��RoomDefinition.ID�ł��B
            {
                int publicId = kvp2.Key; // �p�u���b�N������ID�B
                if (AreRoomsAdjacent(privateId, publicId)) // �v���C�x�[�g�����ƃp�u���b�N�������אڂ��Ă��邩�m�F���܂��B
                {
                    var pair = privateId < publicId ? Tuple.Create(privateId, publicId) : Tuple.Create(publicId, privateId); // ����ID�̃y�A���쐬���܂��B
                    if (PlaceDoorBetweenRooms(privateId, publicId, doors)) // �h�A��z�u���悤�Ǝ��݂܂��B
                    {
                        connectionsMade.Add(pair); // �ڑ����������ꍇ�A�쐬�ς݃Z�b�g�ɒǉ����܂��B
                        connectedTo = publicId; // �ڑ����ID���L�^���܂��B
                        Debug.Log($"Connected Private {privateId} to Public {publicId}"); // ���O�F�v���C�x�[�g-�p�u���b�N�h�A�̔z�u�����B
                        break; // 1�ڑ��ł����OK�Ȃ̂Ń��[�v�𔲂��܂��B
                    }
                }
            }
            // �����p�u���b�N�ɐڑ��ł��Ȃ�������A�L���ɐڑ��ł��Ȃ��������܂� (�_���ɂ͂Ȃ����A���p�I��������܂���)�B
            if (connectedTo == -1) // �܂��ǂ̕����ɂ��ڑ�����Ă��Ȃ��ꍇ�B
            {
                foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway)) // �^�C�v��Hallway�̕�����S�Ď擾���܂��Bkvp2.Key��RoomDefinition.ID�ł��B
                {
                    int hallwayId = kvp2.Key; // �L���̕���ID�B
                    if (AreRoomsAdjacent(privateId, hallwayId)) // �v���C�x�[�g�����ƘL�����אڂ��Ă��邩�m�F���܂��B
                    {
                        var pair = privateId < hallwayId ? Tuple.Create(privateId, hallwayId) : Tuple.Create(hallwayId, privateId); // ����ID�̃y�A���쐬���܂��B
                        if (PlaceDoorBetweenRooms(privateId, hallwayId, doors)) // �h�A��z�u���悤�Ǝ��݂܂��B
                        {
                            connectionsMade.Add(pair); // �ڑ����������ꍇ�A�쐬�ς݃Z�b�g�ɒǉ����܂��B
                            connectedTo = hallwayId; // �ڑ����ID���L�^���܂��B
                            Debug.Log($"Connected Private {privateId} to Hallway {hallwayId} (fallback)"); // ���O�F�v���C�x�[�g-�L���h�A�̔z�u�����i�t�H�[���o�b�N�j�B
                            break; // 1�ڑ��ł����OK�Ȃ̂Ń��[�v�𔲂��܂��B
                        }
                    }
                }
            }

            if (connectedTo == -1) // �ŏI�I�ɂǂ̕����ɂ��ڑ��ł��Ȃ������ꍇ�B
            {
                Debug.LogWarning($"Private room {privateId} remains unconnected after trying Public/Hallway."); // �x�����O�F�v���C�x�[�g���������ڑ��B
            }
        }

        // 2c. ���ڑ��̃p�u���b�N�������A�אڂ��鑼�̃p�u���b�N�����ɐڑ����܂� (�\�ȏꍇ)�B
        // (�������A�ʏ�͘L���Ƃ̐ڑ��ŃJ�o�[����邱�Ƃ������ł�)�B
        foreach (var kvp1 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public)) // �^�C�v��Public�̕�����S�Ď擾���܂��Bkvp1.Key��RoomDefinition.ID�ł��B
        {
            int publicId1 = kvp1.Key; // �ŏ��̃p�u���b�N������ID�B
            if (IsRoomConnected(publicId1, connectionsMade)) continue; // ���ɂǂ����Ɍq�����Ă���ꍇ�̓X�L�b�v���܂��B

            int connectedTo = -1; // �ڑ���̕���ID���L�^����ϐ��B
            foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Public && r.Key != publicId1)) // �^�C�v��Public�ŁA���ŏ��̕����Ƃ͈قȂ镔����S�Ď擾���܂��Bkvp2.Key��RoomDefinition.ID�ł��B
            {
                int publicId2 = kvp2.Key; // ��Ԗڂ̃p�u���b�N������ID�B
                if (AreRoomsAdjacent(publicId1, publicId2)) // 2�̃p�u���b�N�������אڂ��Ă��邩�m�F���܂��B
                {
                    var pair = publicId1 < publicId2 ? Tuple.Create(publicId1, publicId2) : Tuple.Create(publicId2, publicId1); // ����ID�̃y�A���쐬���܂��B
                    if (!connectionsMade.Contains(pair)) // �܂��ڑ�����Ă��Ȃ���΁B
                    {
                        if (PlaceDoorBetweenRooms(publicId1, publicId2, doors)) // �h�A��z�u���悤�Ǝ��݂܂��B
                        {
                            connectionsMade.Add(pair); // �ڑ����������ꍇ�A�쐬�ς݃Z�b�g�ɒǉ����܂��B
                            connectedTo = publicId2; // �ڑ����ID���L�^���܂��B
                            Debug.Log($"Connected Public {publicId1} to Public {publicId2}"); // ���O�F�p�u���b�N-�p�u���b�N�h�A�̔z�u�����B
                            break; // 1�ڑ��ł����OK�Ȃ̂Ń��[�v�𔲂��܂��B
                        }
                    }
                    else // ���ɐڑ�������ꍇ�B
                    {
                        // ���ɐڑ�������Ȃ�A���̕������ڑ��ς݂Ƃ݂Ȃ��܂��B
                        connectedTo = publicId2;
                        break;
                    }
                }
            }
            if (connectedTo == -1) // ���̃p�u���b�N�����Ɍq����Ȃ������ꍇ�B
            {
                // ����Public�ɂ��q����Ȃ��ꍇ�A�L���ɐڑ������݂܂��i�ʏ�͂����炪�D�悳���͂��ł��j�B
                foreach (var kvp2 in _roomDefinitions.Where(r => r.Value.Type == RoomType.Hallway)) // �^�C�v��Hallway�̕�����S�Ď擾���܂��Bkvp2.Key��RoomDefinition.ID�ł��B
                {
                    int hallwayId = kvp2.Key; // �L���̕���ID�B
                    if (AreRoomsAdjacent(publicId1, hallwayId)) // �p�u���b�N�����ƘL�����אڂ��Ă��邩�m�F���܂��B
                    {
                        var pair = publicId1 < hallwayId ? Tuple.Create(publicId1, hallwayId) : Tuple.Create(hallwayId, publicId1); // ����ID�̃y�A���쐬���܂��B
                        if (!connectionsMade.Contains(pair)) // �܂��ڑ�����Ă��Ȃ���΁B
                        {
                            if (PlaceDoorBetweenRooms(publicId1, hallwayId, doors)) // �h�A��z�u���悤�Ǝ��݂܂��B
                            {
                                connectionsMade.Add(pair); // �ڑ����������ꍇ�A�쐬�ς݃Z�b�g�ɒǉ����܂��B
                                connectedTo = hallwayId; // �ڑ����ID���L�^���܂��B
                                Debug.Log($"Connected Public {publicId1} to Hallway {hallwayId} (fallback)"); // ���O�F�p�u���b�N-�L���h�A�̔z�u�����i�t�H�[���o�b�N�j�B
                                break; // 1�ڑ��ł����OK�Ȃ̂Ń��[�v�𔲂��܂��B
                            }
                        }
                        else // ���ɐڑ�������ꍇ�B
                        {
                            connectedTo = hallwayId;
                            break;
                        }
                    }
                }
            }


            if (connectedTo == -1) // �ŏI�I�ɂǂ̕����ɂ��ڑ��ł��Ȃ������ꍇ�B
            {
                Debug.LogWarning($"Public room {publicId1} remains unconnected after trying Public/Hallway."); // �x�����O�F�p�u���b�N���������ڑ��B
            }
        }


        // --- 3. ���B�\���̍ŏI�`�F�b�N�ƏC�� ---
        // (VerifyReachability���ōs���A�K�v�Ȃ炱���ŏC���h�A��ǉ����郍�W�b�N���Ăт܂�)�B
        if (!VerifyReachability(roomsToPlace, doors)) // ���B�\�������؂��܂��B
        {
            Debug.LogWarning("Reachability check failed AFTER initial door placement. Attempting to force connections..."); // �x�����O�F���B�\���`�F�b�N���s�B
            // TODO: �����I�ɐڑ���ǉ����郍�W�b�N�B
            // ��: ���B�s�\�ȕ�������BFS�œ��B�\�ȕ�����T���A���̊Ԃ̍ŒZ�o�H��̗אڃy�A�Ƀh�A��ݒu�B
            // ���̏C�����W�b�N�͕��G�ɂȂ�\��������܂��B
            // ForceReachability(roomsToPlace, doors, connectionsMade);
        }


        Debug.Log($"Connectivity determined. Placed {doors.Count} doors."); // ���O�F�ڑ������菈���̏I���Ɣz�u���ꂽ�h�A�̐��B
        return doors; // �z�u���ꂽ�h�A�̃��X�g��Ԃ��܂��B
    }

    /// <summary>
    /// �w�肳�ꂽ���������ɉ��炩�̐ڑ��������Ă��邩�m�F����֐��B
    /// </summary>
    /// <param name="roomId">�m�F�Ώۂ̕�����ID�B</param>
    /// <param name="connectionsMade">�쐬�ς݂̐ڑ��y�A���i�[����HashSet�B</param>
    /// <returns>�������ڑ��������Ă����true�A�����łȂ����false�B</returns>
    private bool IsRoomConnected(int roomId, HashSet<Tuple<int, int>> connectionsMade) // roomId��RoomDefinition.ID�ł��B
    {
        foreach (var pair in connectionsMade) // �쐬�ς݂̐ڑ��y�A�𑖍����܂��B
        {
            if (pair.Item1 == roomId || pair.Item2 == roomId) // �y�A�̂����ꂩ��ID���Ώۂ̕���ID�ƈ�v����΁B
            {
                return true; // �ڑ��������Ă���Ɣ��f��true��Ԃ��܂��B
            }
        }
        return false; // �ǂ̐ڑ��y�A�ɂ��܂܂�Ă��Ȃ����false��Ԃ��܂��B
    }


    /// <summary>
    /// �w�肳�ꂽ2�̕����̊ԂɃh�A��z�u���悤�Ǝ��݂�֐��B
    /// ����������doors���X�g�ɒǉ����Atrue��Ԃ��B
    /// </summary>
    /// <param name="roomID1">����̕�����ID�B</param>
    /// <param name="roomID2">��������̕�����ID�B</param>
    /// <param name="doors">�z�u���ꂽ�h�A���i�[���郊�X�g�B</param>
    /// <returns>�h�A�̔z�u�ɐ��������ꍇ��true�A�����łȂ��ꍇ��false�B</returns>
    private bool PlaceDoorBetweenRooms(int roomID1, int roomID2, List<Door> doors) // roomID1, roomID2��RoomDefinition.ID�ł��B
    {
        Vector2Int? cell1, cell2; // �h�A��z�u����2�̗אڃZ�����i�[����ϐ��B
        // 2�̕������אڂ���ǃZ�O�����g��T���܂��B
        if (FindAdjacentWallSegment(roomID1, roomID2, out cell1, out cell2))
        {
            if (cell1.HasValue && cell2.HasValue) // �L���ȃZ���y�A�����������ꍇ�B
            {
                // �V�����h�A�I�u�W�F�N�g���쐬���܂��B
                Door newDoor = new Door
                {
                    Cell1 = cell1.Value, // �h�A���ڑ��������̃Z���̍��W�B
                    Cell2 = cell2.Value, // �h�A���ڑ������������̃Z���̍��W�B
                };
                doors.Add(newDoor); // �h�A���X�g�ɒǉ����܂��B
                return true; // �h�A�z�u�����B
            }
        }
        return false; // �h�A�z�u���s�B
    }

    /// <summary>
    /// �w�肳�ꂽ2�̕���ID���O���b�h��ŗאڂ��Ă��邩�ǂ����𔻒肷��֐��B
    /// </summary>
    /// <param name="roomID1">����̕�����ID�B</param>
    /// <param name="roomID2">��������̕�����ID�B</param>
    /// <returns>�������אڂ��Ă����true�A�����łȂ����false�B</returns>
    private bool AreRoomsAdjacent(int roomID1, int roomID2) // roomID1, roomID2��RoomDefinition.ID�ł��B
    {
        Vector2Int? c1, c2; // �אڃZ���̍��W���i�[����ϐ��i�����ł͎g�p���܂��񂪁AFindAdjacentWallSegment�̏o�͈����Ƃ��ĕK�v�j�B
        // FindAdjacentWallSegment�͗אډӏ���������֐��Ȃ̂ŁA
        // ���ꂪtrue��Ԃ��Ηאڂ��Ă���Ɣ��f�ł��܂��B
        return FindAdjacentWallSegment(roomID1, roomID2, out c1, out c2);
    }


    // --- �⏕�֐� ---

    /// <summary>
    /// �w�肳�ꂽ2�̕���ID���אڂ���ǃZ�O�����g��T���A
    /// �h�A��ݒu����̂ɓK�����אڃZ���y�A(cell1, cell2)��Ԃ��B
    /// �����̌�₪����ꍇ�̓����_���ɑI������B
    /// </summary>
    /// <param name="roomID1">����̕�����ID�B</param>
    /// <param name="roomID2">��������̕�����ID�B</param>
    /// <param name="cell1">�h�A���\���������̃Z���̍��W�i�o�́j�B</param>
    /// <param name="cell2">�h�A���\�������������̃Z���̍��W�i�o�́j�B</param>
    /// <returns>�אډӏ����������true�A�����łȂ����false�B</returns>
    private bool FindAdjacentWallSegment(int roomID1, int roomID2, out Vector2Int? cell1, out Vector2Int? cell2) // roomID1, roomID2��RoomDefinition.ID�ł��B
    {
        cell1 = null; // �o�̓p�����[�^���������B
        cell2 = null; // �o�̓p�����[�^���������B
        List<Tuple<Vector2Int, Vector2Int>> potentialDoorLocations = new List<Tuple<Vector2Int, Vector2Int>>(); // �h�A�ݒu���n�_�̃��X�g�B

        // �O���b�h�S�̂𑖍����܂��B
        for (int x = 0; x < _gridSize.x; x++)
        {
            for (int y = 0; y < _gridSize.y; y++)
            {
                if (_grid[x, y] == roomID1) // ���݂̃Z����roomID1�ɑ�����ꍇ�B
                {
                    Vector2Int currentCell = new Vector2Int(x, y); // ���݂̃Z���̍��W�B
                    // �㉺���E�̗אڃZ�����`���܂��B
                    Vector2Int[] neighbors = {
                        new Vector2Int(x + 1, y), new Vector2Int(x - 1, y),
                        new Vector2Int(x, y + 1), new Vector2Int(x, y - 1)
                    };

                    foreach (var neighbor in neighbors) // �e�אڃZ���ɂ��ď������܂��B
                    {
                        // �אڃZ�����O���b�h�͈͓��ł��邩���m�F���܂��B
                        if (neighbor.x >= 0 && neighbor.x < _gridSize.x && neighbor.y >= 0 && neighbor.y < _gridSize.y)
                        {
                            if (_grid[neighbor.x, neighbor.y] == roomID2) // �אڃZ����roomID2�ɑ�����ꍇ�B
                            {
                                // �h�A�ݒu���Ƃ��āA���݂̃Z���ƗאڃZ���̃y�A��ǉ����܂��B
                                potentialDoorLocations.Add(Tuple.Create(currentCell, neighbor));
                            }
                        }
                    }
                }
            }
        }

        if (potentialDoorLocations.Count > 0) // �h�A�ݒu��₪���������ꍇ�B
        {
            // �����̌�₩�烉���_����1��I�����܂��B
            var chosenLocation = potentialDoorLocations[_random.Next(potentialDoorLocations.Count)];
            cell1 = chosenLocation.Item1; // �I�����ꂽ�y�A�̈���̃Z���B
            cell2 = chosenLocation.Item2; // �I�����ꂽ�y�A�̂�������̃Z���B
            return true; // �אډӏ������B
        }

        return false; // �אډӏ���������Ȃ������ꍇ�B
    }


    /// <summary>
    /// �����Ԃ̗אڐ��񂪖�������Ă��邩���؂���֐��B
    /// </summary>
    /// <param name="rooms">���ؑΏۂ̕�����`�̃��X�g�i���݂�_roomDefinitions���g�p�j�B</param>
    /// <returns>�S�Ă̗אڐ��񂪖�������Ă����true�A�����łȂ����false�B</returns>
    private bool VerifyAdjacencyConstraints(List<RoomDefinition> rooms) // rooms�p�����[�^�͌��ݒ��ڎg�p����Ă��܂���B
    {
        // TODO: ���� (PlaceInitialSeeds �� DetermineConnectivity �ŗאڂ͍l���ς݂����A�ŏI�m�F�Ƃ���)
        // �e�����ɂ��āAConnectivityConstraints �Ɋ܂܂�镔�������ۂɗאڂ��Ă��邩�`�F�b�N
        bool allConstraintsMet = true; // �S�Ă̐��񂪖�������Ă��邩�������t���O�B
        foreach (var kvp in _roomDefinitions) // _roomDefinitions���̑S�Ă̕����ɂ��ď������܂��Bkvp.Key��RoomDefinition.ID�ł��B
        {
            int roomID1 = kvp.Key; // ���݂̕�����ID�B
            RoomDefinition room1 = kvp.Value; // ���݂̕����̒�`�B
            if (room1.ConnectivityConstraints == null) continue; // �ڑ����񂪂Ȃ���΃X�L�b�v�B

            foreach (int requiredNeighborId in room1.ConnectivityConstraints) // �e�K�{�אڕ���ID�ɂ��ď������܂��B
            {
                if (!_roomDefinitions.ContainsKey(requiredNeighborId)) continue; // ����̕��������݂��Ȃ��ꍇ�̓X�L�b�v�B

                if (!AreRoomsAdjacent(roomID1, requiredNeighborId)) // 2�̕������אڂ��Ă��Ȃ��ꍇ�B
                {
                    Debug.LogWarning($"Adjacency constraint NOT MET: Room {roomID1} ({room1.Type}) and Room {requiredNeighborId} ({_roomDefinitions[requiredNeighborId].Type}) are not adjacent."); // �x�����O�F�אڐ���ᔽ�B
                    allConstraintsMet = false; // �t���O��false�ɐݒ�B
                    // ������ false ��Ԃ��Ď��s���s�ɂ��邩�A�x���ɗ��߂邩�͑I���ɂ��܂��B
                }
            }
        }
        if (allConstraintsMet) // �S�Ă̐��񂪖�������Ă���ꍇ�B
        {
            // Debug.Log("All adjacency constraints are met."); // �f�o�b�O���O�i�R�����g�A�E�g����Ă��܂��j�B
        }
        return allConstraintsMet; // ���،��ʂ�Ԃ��܂��B
    }

    /// <summary>
    /// �S�Ă̕������݂��ɓ��B�\�����؂���֐��B
    /// </summary>
    /// <param name="rooms">���ؑΏۂ̕�����`�̃��X�g�i���݂�_roomDefinitions���g�p�j�B</param>
    /// <param name="doors">�z�u���ꂽ�h�A�̃��X�g�B</param>
    /// <returns>�S�Ă̕��������B�\�ł����true�A�����łȂ����false�B</returns>
    private bool VerifyReachability(List<RoomDefinition> rooms, List<Door> doors) // rooms�p�����[�^�͌��ݒ��ڎg�p����Ă��܂���B
    {
        // ������1�ȉ��A�܂��̓h�A�����݂��Ȃ��ꍇ�́A���B�\�Ƃ݂Ȃ����A�`�F�b�N�s�v�Ƃ��܂��B
        if (_roomDefinitions == null || _roomDefinitions.Count <= 1 || doors == null)
        {
            return true;
        }

        // �h�A��񂩂瓞�B�\���𔻒肷�邽�߂̗אڃO���t���\�z���܂��B
        var reachabilityGraph = new AdjacencyGraph<int, Edge<int>>();
        foreach (int roomId in _roomDefinitions.Keys) // _roomDefinitions�̃L�[�iRoomDefinition.ID�j�𒸓_�Ƃ��ăO���t�ɒǉ����܂��B
        {
            reachabilityGraph.AddVertex(roomId); // �S�Ă̕����𒸓_�Ƃ��Ēǉ����܂��B
        }

        HashSet<Tuple<int, int>> addedEdges = new HashSet<Tuple<int, int>>(); // �ǉ��ς݂̃G�b�W���L�^���A�d����h���܂��B
        foreach (var door in doors) // �z�u���ꂽ�e�h�A�ɂ��ď������܂��B
        {
            // �h�A���ڑ�����2�̃Z���̕���ID���擾���܂��B
            int id1 = _grid[door.Cell1.x, door.Cell1.y];
            int id2 = _grid[door.Cell2.x, door.Cell2.y];

            // �����̃Z�����L���ȕ���ID�������A���قȂ镔��ID�ł���ꍇ�B
            if (id1 > 0 && id2 > 0 && id1 != id2)
            {
                // ID�̃y�A���쐬���A����������Item1�ɂ��邱�Ƃň�Ӑ���ۂ��܂��B
                var pair = id1 < id2 ? Tuple.Create(id1, id2) : Tuple.Create(id2, id1);
                if (!addedEdges.Contains(pair)) // �����h�A�ɂ��d���G�b�W��h���܂��B
                {
                    reachabilityGraph.AddEdge(new Edge<int>(id1, id2)); // �O���t�ɃG�b�W��ǉ����܂��B
                    reachabilityGraph.AddEdge(new Edge<int>(id2, id1)); // �����O���t�Ƃ��Ĉ������ߋt�������ǉ����܂��B
                    addedEdges.Add(pair); // �ǉ��ς݃G�b�W�Ƃ��ċL�^���܂��B
                }
            }
        }

        // �A���������`�F�b�N (BFS/DFS) ���܂��B
        if (reachabilityGraph.VertexCount == 0) return true; // �O���t�ɒ��_���Ȃ���Γ��B�\�Ƃ݂Ȃ��܂��B

        HashSet<int> visited = new HashSet<int>(); // �K��ς݂̕���ID���i�[����HashSet�B
        Queue<int> queue = new Queue<int>(); // BFS�̂��߂̃L���[�B

        // �J�n�_��I�����܂� (��: �ŏ��̕���ID�A�܂��͓���̕����^�C�v������΂���)�B
        int startNode = reachabilityGraph.Vertices.First(); // �O���t���̍ŏ��̒��_���J�n�_�Ƃ��܂��B
        // �܂��́AEntrance �� Hallway ������΂�������J�n���邱�Ƃ��l���ł��܂��B
        var startCandidates = _roomDefinitions.Where(kvp => kvp.Value.Type == RoomType.Hallway || kvp.Value.Type == RoomType.Public).Select(kvp => kvp.Key); // kvp.Key��RoomDefinition.ID�ł��B
        if (startCandidates.Any()) // ��₪���݂���ꍇ�B
        {
            startNode = startCandidates.First(); // �ŏ��̌����J�n�_�Ƃ��܂��B
        }


        queue.Enqueue(startNode); // �J�n�_���L���[�ɒǉ����܂��B
        visited.Add(startNode); // �J�n�_��K��ς݂Ƃ��ċL�^���܂��B

        // BFS�����s���܂��B
        while (queue.Count > 0)
        {
            int current = queue.Dequeue(); // �L���[���猻�݂̕���ID�����o���܂��B
            if (reachabilityGraph.TryGetOutEdges(current, out var outEdges)) // ���݂̕�������̏o�G�b�W���擾���܂��B
            {
                foreach (var edge in outEdges) // �e�o�G�b�W�ɂ��ď������܂��B
                {
                    int neighbor = edge.Target; // �אڂ��镔��ID���擾���܂��B
                    if (!visited.Contains(neighbor)) // �אڕ��������K��̏ꍇ�B
                    {
                        visited.Add(neighbor); // �K��ς݂Ƃ��ċL�^���܂��B
                        queue.Enqueue(neighbor); // �L���[�ɒǉ����܂��B
                    }
                }
            }
        }

        // �S�Ă̕���ID���K��ς݂��`�F�b�N���܂��B
        bool allReachable = visited.Count == reachabilityGraph.VertexCount;

        if (!allReachable) // �S�Ă̕��������B�\�łȂ��ꍇ�B
        {
            var unreachable = reachabilityGraph.Vertices.Except(visited).ToList(); // ���B�s�\�ȕ����̃��X�g���擾���܂��B
            Debug.LogWarning($"Reachability check failed. Unreachable rooms: {string.Join(", ", unreachable)} (started from {startNode})"); // �x�����O�F���B�\���`�F�b�N���s�B
        }
        else // �S�Ă̕��������B�\�ȏꍇ�B
        {
            // Debug.Log("Reachability check passed."); // �f�o�b�O���O�i�R�����g�A�E�g����Ă��܂��j�B
        }

        return allReachable; // ���B�\���̌��،��ʂ�Ԃ��܂��B
    }
}