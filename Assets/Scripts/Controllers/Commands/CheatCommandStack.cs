using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Play-mode undo stack for cheats and committed chess moves. Not Unity Editor Undo.
/// </summary>
public class CheatCommandStack
{
    public const int MaxDepth = 16;

    readonly Stack<BoardSnapshot> _stack = new();

    public int Count => _stack.Count;

    public void Clear() => _stack.Clear();

    public void Push(BoardSnapshot snapshot)
    {
        if (snapshot == null)
            return;
        if (_stack.Count >= MaxDepth)
        {
            var keep = _stack.ToArray();
            _stack.Clear();
            for (int i = keep.Length - 2; i >= 0; i--)
                _stack.Push(keep[i]);
        }

        _stack.Push(snapshot);
    }

    public bool TryPop(out BoardSnapshot snapshot)
    {
        if (_stack.Count == 0)
        {
            snapshot = null;
            return false;
        }

        snapshot = _stack.Pop();
        return snapshot != null;
    }
}

public sealed class BoardSnapshot
{
    public Team CurrentTeam;
    public bool GameOver;
    public int SelectedX = -1;
    public int SelectedY = -1;
    public bool EnPassantAvailable;
    public Unit EnPassantVictim;
    public int EnPassantLandingX = -1;
    public int EnPassantLandingY = -1;
    public readonly List<UnitRecord> Units = new();

    public struct UnitRecord
    {
        public Unit Unit;
        public int CellX;
        public int CellY;
        public bool HasCell;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public bool HasMoved;
        public bool Active;
        public bool Dead;
    }
}
