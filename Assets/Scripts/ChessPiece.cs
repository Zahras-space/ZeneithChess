using UnityEngine;

public enum PieceType { King, Queen, Rook, Bishop, Knight, Pawn }
public enum PieceColor { White, Black }

public class ChessPiece : MonoBehaviour
{
    public PieceType pieceType;
    public PieceColor pieceColor;
    public BoardCell currentCell;
    public bool hasMoved = false;
}