using System.Data;

namespace Tfpu.OpcUa.ClientService;

public static class SqlDbTypeExtensions
{
    public static bool IsNumeric(this SqlDbType dataType)
    {
        return dataType switch
        {
            SqlDbType.BigInt or
            SqlDbType.Decimal or
            SqlDbType.Float or
            SqlDbType.Int or
            SqlDbType.Money or
            SqlDbType.Real or
            SqlDbType.SmallInt or
            SqlDbType.SmallMoney or
            SqlDbType.TinyInt => true,
            _ => false
        };
    }
}