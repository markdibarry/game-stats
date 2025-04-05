namespace GameCore.Statistics;

public enum CompareOp
{
    None,
    LessEquals,
    GreaterEquals,
    Less,
    Greater,
    Equals,
    NotEquals
}

public static class CompareExtensions
{
    public static bool Compare(this CompareOp op, int a, int b)
    {
        return op switch
        {
            CompareOp.Equals => a == b,
            CompareOp.NotEquals => a != b,
            CompareOp.LessEquals => a <= b,
            CompareOp.GreaterEquals => a >= b,
            CompareOp.Less => a < b,
            CompareOp.Greater => a > b,
            CompareOp.None or
            _ => false
        };
    }

    public static string ToSymbol(this CompareOp compareOp)
    {
        return compareOp switch
        {
            CompareOp.LessEquals => "<=",
            CompareOp.Less => "<",
            CompareOp.GreaterEquals => ">=",
            CompareOp.Greater => ">",
            CompareOp.Equals => "=",
            CompareOp.NotEquals => "!=",
            _ => "",
        };
    }
}