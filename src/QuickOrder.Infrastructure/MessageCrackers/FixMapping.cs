namespace QuickOrder.Infrastructure.MessageCrackers;

using QuickOrder.Core.Domain.ValueObjects;

public static class FixMapping
{
    public static bool TryParseSymbol(string value, out Symbol symbol)
        => Enum.TryParse(value, out symbol) && Enum.IsDefined(symbol);

    public static bool TryParseSide(char value, out Side side)
    {
        switch (value)
        {
            case '1': side = Side.Buy; return true;
            case '2': side = Side.Sell; return true;
            default:  side = default;  return false;
        }
    }

    public static char ToFixChar(Side side) => side switch
    {
        Side.Buy  => '1',
        Side.Sell => '2',
        _ => '?'
    };

    public static string ToFixString(Symbol symbol) => symbol.ToString();
}
