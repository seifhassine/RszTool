using System.Drawing;
using System.Numerics;
using System.Text;
using RszTool.Common;

namespace RszTool
{
    /// <summary>
    /// 存放RszClass的数据
    /// </summary>
    public class RszInstance : BaseModel, ICloneable
    {
        public RszClass RszClass { get; set; }
        public object[] Values { get; set; }
        public int Index { get; set; }
        // 在ObjectTable中的序号，-1表示不在
        public int ObjectTableIndex { get; set; } = -1;
        public string Name { get; set; }
        public IRSZUserDataInfo? RSZUserData { get; set; }

        public RszInstance(RszClass rszClass, int index, IRSZUserDataInfo? userData = null)
        {
            RszClass = rszClass;
            Values = userData == null ? new object[rszClass.fields.Length] : Array.Empty<object>();
            Index = index;
            RSZUserData = userData;
            Name = $"{rszClass.name}[{index}]";
        }

        private RszInstance()
        {
            RszClass = RszClass.Empty;
            Values = Array.Empty<object>();
            Name = "";
        }

        /// <summary>
        /// 一般InstanceList第一个对象是NULL实例
        /// </summary>
        public static readonly RszInstance NULL = new();

        private void AlignFirstField(FileHandler handler)
        {
            handler.Align(RszClass.fields[0].array ? 4 : RszClass.fields[0].align);
        }

        /// <summary>
        /// 读取字段
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        protected override bool DoRead(FileHandler handler)
        {
            // if has RSZUserData, it is external
            if (RSZUserData != null || RszClass.fields.Length == 0) return true;

            AlignFirstField(handler);
            // Console.WriteLine($"read {Name} at: {handler.Position:X}");
            for (int i = 0; i < RszClass.fields.Length; i++)
            {
                Values[i] = ReadRszField(handler, i);
            }
            return true;
        }

        public object ReadRszField(FileHandler handler, int index)
        {
            RszField field = RszClass.fields[index];
            handler.Align(field.array ? 4 : field.align);
            // Console.WriteLine($"    read at: {handler.Position:X} {field.original_type} {field.name}");
            if (field.array)
            {
                int count = handler.ReadInt();
                if (count < 0)
                {
                    throw new InvalidDataException($"{field.name} count {count} < 0");
                }
                if (count > 1024)
                {
                    throw new InvalidDataException($"{field.name} count {count} too large");
                }
                List<object> arrayItems = new(count);
                if (count > 0) handler.Align(field.align);
                for (int i = 0; i < count; i++)
                {
                    arrayItems.Add(ReadNormalField(handler, field));
                }
                return arrayItems;
            }
            else
            {
                return ReadNormalField(handler, field);
            }
        }

        public static object ReadNormalField(FileHandler handler, RszField field)
        {
            if (field.type == RszFieldType.String || field.type == RszFieldType.Resource)
            {
                int charCount = handler.ReadInt();
                long stringStart = handler.Tell();
                string value = charCount <= 1 ? "" : handler.ReadWString(charCount: charCount);
                handler.Seek(stringStart + charCount * 2);
                // TODO checkOpenResource
                return value;
            }
            else
            {
                long startPos = handler.Tell();
                object value = field.type switch
                {
                    RszFieldType.S32 or RszFieldType.Object or RszFieldType.UserData => handler.ReadInt(),
                    RszFieldType.U32 => handler.ReadUInt(),
                    RszFieldType.S64 => handler.ReadInt64(),
                    RszFieldType.U64 => handler.ReadUInt64(),
                    RszFieldType.F32 => handler.ReadFloat(),
                    RszFieldType.F64 => handler.ReadDouble(),
                    RszFieldType.Bool => handler.ReadBoolean(),
                    RszFieldType.S8 => handler.ReadSByte(),
                    RszFieldType.U8 => handler.ReadByte(),
                    RszFieldType.S16 => handler.ReadShort(),
                    RszFieldType.U16 => handler.ReadUShort(),
                    RszFieldType.Data => handler.ReadBytes(field.size),
                    RszFieldType.Mat4 => handler.Read<via.mat4>(),
                    RszFieldType.Vec2 or RszFieldType.Float2 => handler.Read<Vector2>(),
                    RszFieldType.Vec3 or RszFieldType.Float3 => handler.Read<Vector3>(),
                    RszFieldType.Vec4 or RszFieldType.Float4 => handler.Read<Vector4>(),
                    RszFieldType.OBB => handler.ReadArray<float>(20),
                    RszFieldType.Guid or RszFieldType.GameObjectRef => handler.Read<Guid>(),
                    RszFieldType.Color => handler.Read<Color>(),
                    RszFieldType.Range => handler.Read<via.Range>(),
                    RszFieldType.Quaternion => handler.Read<Quaternion>(),
                    RszFieldType.Sphere => handler.Read<via.Sphere>(),
                    _ => throw new InvalidDataException($"Not support type {field.type}"),
                };
                /* if (field.size != handler.Tell() - startPos)
                {
                    throw new InvalidDataException($"{field.name} size not match");
                } */
                handler.Seek(startPos + field.size);
                return value;
            }
        }

        protected override bool DoWrite(FileHandler handler)
        {
            // if has RSZUserData, it is external
            if (RSZUserData != null || RszClass.fields.Length == 0) return true;
            AlignFirstField(handler);
            // Console.WriteLine($"write {Name} at: {(handler.Offset + handler.Tell()):X}");
            for (int i = 0; i < RszClass.fields.Length; i++)
            {
                WriteRszField(handler, i);
            }
            return true;
        }

        public bool WriteRszField(FileHandler handler, int index)
        {
            RszField field = RszClass.fields[index];
            handler.Align(field.array ? 4 : field.align);
            // Console.WriteLine($"    write at: {handler.Position:X} {field.original_type} {field.name}");
            if (field.array)
            {
                List<object> list = (List<object>)Values[index];
                handler.Write(list.Count);
                if (list.Count > 0)
                {
                    handler.Align(field.align);
                    foreach (var value in list)
                    {
                        WriteNormalField(handler, field, value);
                    }
                }
                return true;
            }
            else
            {
                return WriteNormalField(handler, field, Values[index]);
            }
        }

        public static bool WriteNormalField(FileHandler handler, RszField field, object value)
        {
            if (field.type == RszFieldType.String || field.type == RszFieldType.Resource)
            {
                string valueStr = (string)value;
                return handler.Write(valueStr.Length + 1) && handler.WriteWString(valueStr);
            }
            else if (field.type == RszFieldType.Object || field.type == RszFieldType.UserData)
            {
                return handler.Write(value is RszInstance instance ? instance.Index : (int)value);
            }
            else
            {
                long startPos = handler.Tell();
                _ = field.type switch
                {
                    RszFieldType.S32 => handler.Write((int)value),
                    RszFieldType.U32 => handler.Write((uint)value),
                    RszFieldType.S64 => handler.Write((long)value),
                    RszFieldType.U64 => handler.Write((ulong)value),
                    RszFieldType.F32 => handler.Write((float)value),
                    RszFieldType.F64 => handler.Write((double)value),
                    RszFieldType.Bool => handler.Write((bool)value),
                    RszFieldType.S8 => handler.Write((sbyte)value),
                    RszFieldType.U8 => handler.Write((byte)value),
                    RszFieldType.S16 => handler.Write((short)value),
                    RszFieldType.U16 => handler.Write((ushort)value),
                    RszFieldType.Data => handler.WriteBytes((byte[])value),
                    RszFieldType.Mat4 => handler.Write((via.mat4)value),
                    RszFieldType.Vec2 or RszFieldType.Float2 => handler.Write((Vector2)value),
                    RszFieldType.Vec3 or RszFieldType.Float3 => handler.Write((Vector3)value),
                    RszFieldType.Vec4 or RszFieldType.Float4 => handler.Write((Vector4)value),
                    RszFieldType.OBB => handler.WriteArray((float[])value),
                    RszFieldType.Guid or RszFieldType.GameObjectRef => handler.Write((Guid)value),
                    RszFieldType.Color => handler.Write((Color)value),
                    RszFieldType.Range => handler.Write((via.Range)value),
                    RszFieldType.Quaternion => handler.Write((Quaternion)value),
                    RszFieldType.Sphere => handler.Write((via.Sphere)value),
                    _ => throw new InvalidDataException($"Not support type {field.type}"),
                };
                handler.Seek(startPos + field.size);
                return true;
            }
        }

        public static Type FieldTypeToSharpType(RszField field)
        {
            return field.type switch
            {
                RszFieldType.S32 or RszFieldType.Object or RszFieldType.UserData => typeof(int),
                RszFieldType.U32 => typeof(uint),
                RszFieldType.S64 => typeof(long),
                RszFieldType.U64 => typeof(ulong),
                RszFieldType.F32 => typeof(float),
                RszFieldType.F64 => typeof(double),
                RszFieldType.Bool => typeof(bool),
                RszFieldType.S8 => typeof(sbyte),
                RszFieldType.U8 => typeof(byte),
                RszFieldType.S16 => typeof(short),
                RszFieldType.U16 => typeof(ushort),
                RszFieldType.Data => typeof(byte[]),
                RszFieldType.Mat4 => typeof(via.mat4),
                RszFieldType.Vec2 or RszFieldType.Float2 => typeof(Vector2),
                RszFieldType.Vec3 or RszFieldType.Float3 => typeof(Vector3),
                RszFieldType.Vec4 or RszFieldType.Float4 => typeof(Vector4),
                RszFieldType.OBB => typeof(float[]),
                RszFieldType.Guid or RszFieldType.GameObjectRef => typeof(Guid),
                RszFieldType.Color => typeof(Color),
                RszFieldType.Range => typeof(via.Range),
                RszFieldType.Quaternion => typeof(Quaternion),
                RszFieldType.Sphere => typeof(via.Sphere),
                RszFieldType.String or RszFieldType.Resource => typeof(string),
                _ => throw new InvalidDataException($"Not support type {field.type}"),
            };
        }

        /// <summary>
        /// 获取字段值
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object? GetFieldValue(string name)
        {
            int index = RszClass.IndexOfField(name);
            if (index == -1) return null;
            return Values[index];
        }

        /// <summary>
        /// 设置字段值
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetFieldValue(string name, object value)
        {
            int index = RszClass.IndexOfField(name);
            if (index == -1) return;
            Values[index] = value;
        }

        /// <summary>
        /// 拷贝自身，如果字段值是RszInstance，则递归拷贝
        /// </summary>
        /// <returns></returns>
        public override object Clone()
        {
            RszInstance copy = (RszInstance)MemberwiseClone();
            if (RSZUserData != null)
            {
                copy.RSZUserData = (IRSZUserDataInfo)RSZUserData.Clone();
            }
            else
            {
                for (int i = 0; i < RszClass.fields.Length; i++)
                {
                    var field = RszClass.fields[i];
                    if (field.IsReference)
                    {
                        if (field.array)
                        {
                            var newArray = new List<object>((List<object>)copy.Values[i]);
                            copy.Values[i] = newArray;
                            for (int j = 0; j < newArray.Count; j++)
                            {
                                if (newArray[j] is RszInstance item)
                                {
                                    newArray[j] = item.Clone();
                                }
                            }
                        }
                        if (copy.Values[i] is RszInstance instance)
                        {
                            copy.Values[i] = instance.Clone();
                        }
                    }
                }
            }
            return copy;
        }

        public IEnumerable<RszInstance> Flatten()
        {
            if (RSZUserData == null)
            {
                var fields = RszClass.fields;
                for (int i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (field.IsReference)
                    {
                        if (field.array)
                        {
                            var items = (List<object>)Values[i];
                            for (int j = 0; j < items.Count; j++)
                            {
                                if (items[j] is RszInstance instanceValue)
                                {
                                    foreach (var item in instanceValue.Flatten())
                                    {
                                        yield return item;
                                    }
                                }
                                else
                                {
                                    throw new InvalidDataException("Instance should unflatten first");
                                }
                            }
                        }
                        else if (Values[i] is RszInstance instanceValue)
                        {
                            foreach (var item in instanceValue.Flatten())
                            {
                                yield return item;
                            }
                        }
                        else
                        {
                            throw new InvalidDataException("Instance should unflatten first");
                        }
                    }
                }
            }
            yield return this;
        }

        public void Stringify(StringBuilder sb, IList<RszInstance>? instances = null, int indent = 0)
        {
            if (RszClass.crc == 0)
            {
                sb.Append("NULL");
                return;
            }
            sb.Append(Name);
            sb.AppendLine(" {");

            void ValueStringify(RszField field, object value)
            {
                if (field.IsReference)
                {
                    if (field.array)
                    {
                        sb.AppendLine();
                        sb.AppendIndent(indent + 2);
                    }
                    if (value is int index && instances != null)
                    {
                        instances[(int)value].Stringify(sb, instances, indent + 2);
                    }
                    else if (value is RszInstance instance)
                    {
                        instance.Stringify(sb, instances, indent + 2);
                    }
                    else
                    {
                        sb.Append(value);
                    }
                }
                else
                {
                    sb.Append(value);
                }
            }

            if (RSZUserData != null)
            {
                if (RSZUserData is RSZUserDataInfo info)
                {
                    sb.AppendIndent(indent + 1);
                    sb.AppendLine($"RSZUserDataPath = {info.path}");
                }
            }
            else
            {
                for (int i = 0; i < RszClass.fields.Length; i++)
                {
                    RszField field = RszClass.fields[i];
                    string type = field.original_type != "" ? field.original_type : field.type.ToString();
                    sb.AppendIndent(indent + 1);
                    sb.Append($"{type} {field.name} = ");
                    if (field.array)
                    {
                        sb.Append('[');
                        var items = (List<object>)Values[i];
                        if (items.Count > 0)
                        {
                            foreach (var item in items)
                            {
                                ValueStringify(field, item);
                                sb.Append(", ");
                            }
                            sb.Length -= 2;
                        }
                        sb.AppendLine("];");
                    }
                    else
                    {
                        ValueStringify(field, Values[i]);
                        sb.AppendLine(";");
                    }
                }
            }
            sb.AppendIndent(indent);
            sb.Append('}');
        }

        public string Stringify(IList<RszInstance>? instances = null, int indent = 0)
        {
            StringBuilder sb = new();
            Stringify(sb, instances, indent);
            return sb.ToString();
        }
    }
}
