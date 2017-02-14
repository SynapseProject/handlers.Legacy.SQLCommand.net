using System;
using System.ComponentModel;
using System.Data;

namespace Synapse.Handlers.Legacy.SQLCommand
{
    public enum DatabaseTypes
    {
        None,
        Oracle,
        SqlServer
    }

    public enum SqlParamterTypes
    {
        AnsiString = 0,
        Binary = 1,                 
        Byte = 2,
        Boolean = 3,
        Currency = 4,
        Date = 5,
        DateTime = 6,
        Decimal = 7,
        Double = 8,
        Guid = 9,
        Int16 = 10,
        Int32 = 11,
        Int64 = 12,
        Object = 13,
        SByte = 14,
        Single = 15,
        String = 16,
        Time = 17,
        UInt16 = 18,
        UInt32 = 19,
        UInt64 = 20,
        VarNumeric = 21,
        AnsiStringFixedLength = 22,
        StringFixedLength = 23,         
        Xml = 25,
        DateTime2 = 26,
        DateTimeOffset = 27,

        // Oracle Specific DataTypes
        BFile = 101,
        Blob = 102,
        Char = 104,
        Clob = 105,
        Long = 109,
        LongRaw = 110,
        IntervalDS = 114,
        IntervalYM = 115,
        NClob = 116,
        NChar = 117,
        NVarchar2 = 119,
        Raw = 120,
        RefCursor = 121,
        TimeStamp = 123,
        TimeStampLTZ = 124,
        TimeStampTZ = 125,
        Varchar2 = 126,
        XmlType = 127,
        Array = 128,
        Ref = 130,
        BinaryDouble = 132,
        BinaryFloat = 133,
    }
}