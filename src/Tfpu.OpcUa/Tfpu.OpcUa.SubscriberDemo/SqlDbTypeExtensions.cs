using Opc.Ua;
using System.Data;

namespace Tfpu.OpcUa.SubscriberDemo;

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

    public static NodeId ToOpcUaDataType(this SqlDbType dataType)
    {
        return dataType switch
        {
            SqlDbType.BigInt => DataTypeIds.Int64,
            SqlDbType.Binary => DataTypeIds.ByteString,
            SqlDbType.Bit => DataTypeIds.Boolean,
            SqlDbType.Char => DataTypeIds.String,
            SqlDbType.Date => DataTypeIds.DateTime,
            SqlDbType.DateTime => DataTypeIds.DateTime,
            SqlDbType.DateTime2 => DataTypeIds.DateTime,
            SqlDbType.DateTimeOffset => DataTypeIds.DateTime,
            SqlDbType.Decimal => DataTypeIds.Decimal,
            SqlDbType.Float => DataTypeIds.Double,
            SqlDbType.Image => DataTypeIds.ByteString,
            SqlDbType.Int => DataTypeIds.Int32,
            SqlDbType.Money => DataTypeIds.Decimal,
            SqlDbType.NChar => DataTypeIds.String,
            SqlDbType.NText => DataTypeIds.String,
            SqlDbType.NVarChar => DataTypeIds.String,
            SqlDbType.Real => DataTypeIds.Float,
            SqlDbType.SmallDateTime => DataTypeIds.DateTime,
            SqlDbType.SmallInt => DataTypeIds.Int16,
            SqlDbType.SmallMoney => DataTypeIds.Decimal,
            SqlDbType.Text => DataTypeIds.String,
            SqlDbType.Time => DataTypeIds.String,
            SqlDbType.Timestamp => DataTypeIds.ByteString,
            SqlDbType.TinyInt => DataTypeIds.Byte,
            SqlDbType.UniqueIdentifier => DataTypeIds.Guid,
            SqlDbType.VarBinary => DataTypeIds.ByteString,
            SqlDbType.VarChar => DataTypeIds.String,
            SqlDbType.Xml => DataTypeIds.XmlElement,
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported SQL data type.")
        };
    }

    public static BuiltInType ToOpcUaBuiltInType(this SqlDbType dataType)
    {
        return dataType switch
        {
            SqlDbType.BigInt => BuiltInType.Int64,
            SqlDbType.Binary => BuiltInType.ByteString,
            SqlDbType.Bit => BuiltInType.Boolean,
            SqlDbType.Char => BuiltInType.String,
            SqlDbType.Date => BuiltInType.DateTime,
            SqlDbType.DateTime => BuiltInType.DateTime,
            SqlDbType.DateTime2 => BuiltInType.DateTime,
            SqlDbType.DateTimeOffset => BuiltInType.DateTime,
            SqlDbType.Decimal => BuiltInType.Double,
            SqlDbType.Float => BuiltInType.Double,
            SqlDbType.Image => BuiltInType.ByteString,
            SqlDbType.Int => BuiltInType.Int32,
            SqlDbType.Money => BuiltInType.Double,
            SqlDbType.NChar => BuiltInType.String,
            SqlDbType.NText => BuiltInType.String,
            SqlDbType.NVarChar => BuiltInType.String,
            SqlDbType.Real => BuiltInType.Float,
            SqlDbType.SmallDateTime => BuiltInType.DateTime,
            SqlDbType.SmallInt => BuiltInType.Int16,
            SqlDbType.SmallMoney => BuiltInType.Double,
            SqlDbType.Text => BuiltInType.String,
            SqlDbType.Time => BuiltInType.String,
            SqlDbType.Timestamp => BuiltInType.ByteString,
            SqlDbType.TinyInt => BuiltInType.Byte,
            SqlDbType.UniqueIdentifier => BuiltInType.Guid,
            SqlDbType.VarBinary => BuiltInType.ByteString,
            SqlDbType.VarChar => BuiltInType.String,
            SqlDbType.Xml => BuiltInType.XmlElement,
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported SQL data type.")
        };
    }
}