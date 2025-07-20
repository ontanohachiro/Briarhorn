using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ディープコピーを行うための静的ヘルパークラス
/// </summary>
public static class DeepCopyHelper
{
    /// <summary>
    /// int型の2次元配列(_grid)のディープコピーを作成します。
    /// </summary>
    /// <param name="originalGrid">コピー元のint[,]型の配列</param>
    /// <returns>各要素がコピーされた新しいint[,]型の配列</returns>
    public static int[,] DeepCopyGrid(int[,] originalGrid)
    {
        if (originalGrid == null)
        {
            return null;
        }

        // コピー元と同じサイズの新しい2次元配列を作成
        int rows = originalGrid.GetLength(0);
        int cols = originalGrid.GetLength(1);
        int[,] newGrid = new int[rows, cols];

        // 全ての要素を新しい配列にコピー
        // intは値型なので、単純な代入でディープコピーが実現されます
        Array.Copy(originalGrid, newGrid, originalGrid.Length);

        return newGrid;
    }

    /// <summary>
    /// RoomDefinitionのリスト(List<RoomDefinition>)のディープコピーを作成します。
    /// </summary>
    /// <param name="originalList">コピー元のList<RoomDefinition></param>
    /// <returns>各RoomDefinitionオブジェクトが新しくインスタンス化されたList<RoomDefinition></returns>
    public static List<RoomDefinition> DeepCopyRoomDefinitions(List<RoomDefinition> originalList)
    {
        if (originalList == null)
        {
            return null;
        }

        var newList = new List<RoomDefinition>();
        foreach (var originalRoom in originalList)
        {
            // RoomDefinitionのコンストラクタを使用して基本的な情報をコピー
            var newRoom = new RoomDefinition(originalRoom.ID, originalRoom.Type, originalRoom.SizeRatio);

            // コンストラクタで設定されない実行時プロパティもコピー
            // Vector2Int? と RectInt は構造体(値型)なので、直接代入でコピーされます
            newRoom.InitialSeedPosition = originalRoom.InitialSeedPosition;
            newRoom.Bounds = originalRoom.Bounds;
            newRoom.CurrentSize = originalRoom.CurrentSize;

            newList.Add(newRoom);
        }
        return newList;
    }

    /// <summary>
    /// Doorのリスト(List<Door>)のディープコピーを作成します。
    /// </summary>
    /// <param name="originalList">コピー元のList<Door></param>
    /// <returns>各Doorオブジェクトが新しくインスタンス化されたList<Door></returns>
    public static List<Door> DeepCopyDoors(List<Door> originalList)
    {
        if (originalList == null)
        {
            return null;
        }

        var newList = new List<Door>();
        foreach (var originalDoor in originalList)
        {
            var newDoor = new Door
            {
                // Vector2Intは構造体(値型)なので、直接代入でコピーされます
                Cell1 = originalDoor.Cell1,
                Cell2 = originalDoor.Cell2,

                // Tupleは参照型なので、新しいインスタンスを作成して値をコピーする必要があります
                edge = new Tuple<int, int>(originalDoor.edge.Item1, originalDoor.edge.Item2)
            };
            newList.Add(newDoor);
        }
        return newList;
    }
}