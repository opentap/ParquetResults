﻿using OpenTap;
using Parquet;
using Parquet.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParquetResultListener
{
    internal sealed class ParquetFile : IDisposable
    {
        private Dictionary<DataField, ArrayList> _cachedData = new Dictionary<DataField, ArrayList>();
        private readonly Schema _schema;
        private readonly Stream _stream;
        private readonly ParquetWriter _writer;

        public string Path { get; }

        internal ParquetFile(Schema schema, string path)
        {
            _schema = schema;
            _stream = File.OpenWrite(path);
            _writer = new ParquetWriter(schema, _stream);
            _cachedData = schema.GetDataFields().ToDictionary(field => field, field => new ArrayList());
            Path = path;
        }

        internal void OnlyParameters(TestPlanRun planRun)
        {
            Dictionary<string, IConvertible> parameters = GetParameters(planRun);

            foreach (DataField field in _schema.GetDataFields())
            {
                ArrayList column = _cachedData[field];
                switch (SchemaBuilder.GetColumnType(field, out string name))
                {
                    case ColumnType.Plan:
                        column.Add(parameters[name]);
                        break;
                    case ColumnType.Step:
                        column.Add(null);
                        break;
                    case ColumnType.Result:
                        column.Add(null);
                        break;
                    case ColumnType.Guid:
                        column.Add(planRun.Id);
                        break;
                    case ColumnType.Parent:
                        column.Add(null);
                        break;
                }
            }
        }

        internal void OnlyParameters(TestStepRun stepRun)
        {
            Dictionary<string, IConvertible> parameters = GetParameters(stepRun);

            foreach (DataField field in _schema.GetDataFields())
            {
                ArrayList column = _cachedData[field];
                switch (SchemaBuilder.GetColumnType(field, out string name))
                {
                    case ColumnType.Plan:
                        column.Add(null);
                        break;
                    case ColumnType.Step:
                        column.Add(parameters[name]);
                        break;
                    case ColumnType.Result:
                        column.Add(null);
                        break;
                    case ColumnType.Guid:
                        column.Add(stepRun.Id);
                        break;
                    case ColumnType.Parent:
                        column.Add(stepRun.Parent);
                        break;
                }
            }
        }

        internal void Results(TestStepRun stepRun, ResultTable table)
        {
            Dictionary<string, IConvertible> parameters = GetParameters(stepRun);
            Dictionary<string, Array> results = table.Columns.ToDictionary(c => c.Name, c => c.Data);
            int count = table.Columns.Max(c => c.Data.Length);

            foreach (DataField field in _schema.GetDataFields())
            {
                ArrayList column = _cachedData[field];
                switch (SchemaBuilder.GetColumnType(field, out string name))
                {
                    case ColumnType.Plan:
                        column.AddRange(Enumerable.Repeat<object?>(null, count).ToArray());
                        break;
                    case ColumnType.Step:
                        column.AddRange(Enumerable.Repeat(parameters[name], count).ToArray());
                        break;
                    case ColumnType.Result:
                        column.AddRange(results.Values);
                        break;
                    case ColumnType.Guid:
                        column.AddRange(Enumerable.Repeat(stepRun.Id, count).ToArray());
                        break;
                    case ColumnType.Parent:
                        column.AddRange(Enumerable.Repeat(stepRun.Parent, count).ToArray());
                        break;
                }
            }
        }

        private void WriteCache()
        {
            ParquetRowGroupWriter groupWriter = _writer.CreateRowGroup();
            foreach (KeyValuePair<DataField, ArrayList> kvp in _cachedData)
            {
                DataField field = kvp.Key;
                ArrayList list = kvp.Value;
                Array data = ConvertList(list, field.DataType);
                DataColumn column = new DataColumn(field, data);
                groupWriter.WriteColumn(column);
            }
            groupWriter.Dispose();
        }

        internal bool CanContain(Schema schema)
        {
            return _schema.Equals(schema);
        }

        public void Dispose()
        {
            WriteCache();
            _writer.Dispose();
            _stream.Dispose();
        }

        private static Array ConvertList(ArrayList list, DataType type)
        {
            switch (type)
            {
                case DataType.Boolean:
                    return list.ToArray(typeof(bool?));
                case DataType.Byte:
                    return list.ToArray(typeof(byte?));
                case DataType.SignedByte:
                    return list.ToArray(typeof(sbyte?));
                case DataType.UnsignedByte:
                    return list.ToArray(typeof(byte?));
                case DataType.Short:
                    return list.ToArray(typeof(short?));
                case DataType.UnsignedShort:
                    return list.ToArray(typeof(ushort?));
                case DataType.Int16:
                    return list.ToArray(typeof(short?));
                case DataType.UnsignedInt16:
                    return list.ToArray(typeof(ushort?));
                case DataType.Int32:
                    return list.ToArray(typeof(int?));
                case DataType.Int64:
                    return list.ToArray(typeof(long?));
                case DataType.String:
                    return list.OfType<object?>().Select(o => o?.ToString()).ToArray();
                case DataType.Float:
                    return list.ToArray(typeof(float?));
                case DataType.Double:
                    return list.ToArray(typeof(double?));
                case DataType.Decimal:
                    return list.ToArray(typeof(decimal?));
                case DataType.DateTimeOffset:
                    return list.OfType<IConvertible?>().Select<IConvertible?, DateTimeOffset?>(dt => dt is null ? null : new DateTimeOffset((DateTime)dt)).ToArray();
                default:
                    throw new NotImplementedException();
            }
        }

        private static Dictionary<string, IConvertible> GetParameters(TestRun planRun)
        {
            return planRun.Parameters
                            .ToDictionary(p => SchemaBuilder.GetValidParquetName(p.Group, p.Name), p => p.Value);
        }
    }
}
