using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace eVision.QueryHost.Specs.Queries
{
    [ApiName("examplewithserializable")]
    public class ExampleWithSerializableQuery : IQuery<ExampleWithSerializableQuery.Result>
    {
        [Serializable]
        public class Result
        {
            public int Count { get; set; }
            public List<string> ConvertedValues { get; set; }
        }
        public List<string> Values { get; set; }
    }

    [ApiName("examplewithserializableinterface")]
    public class ExampleWithSerializableInterfaceQuery : IQuery<ExampleWithSerializableInterfaceQuery.Result>
    {
        [Serializable]
        public class Result
        {
            public int Count { get; set; }
            public ComplexKeySerializableDictionary<SiteCode, string[]> ConvertedValues { get; set; }
        }
        public List<string> Values { get; set; }
    }

    [Serializable]
    [XmlSchemaProvider("GetSerializationSchema")]
    public class SiteCode : GuidKey
    {
        public SiteCode() { }

        public SiteCode(Guid key) : base(key) { }

        protected SiteCode(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public override object Clone()
        {
            return new SiteCode(Key);
        }

        public static XmlQualifiedName GetSerializationSchema(XmlSchemaSet schemaSet)
        {
            return new XmlQualifiedName("string", "http://www.w3.org/2001/XMLSchema");
        }
    }

    [Serializable]
    public abstract class GuidKey : IXmlSerializable, ISerializable, ICloneable, IComparable
    {
        private Guid key;

        protected GuidKey() { }

        protected GuidKey(SerializationInfo info, StreamingContext context)
        {
            key = Guid.Parse(info.GetString("Key"));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuidKey" /> struct.
        /// </summary>
        /// <param name="key">The key.</param>
        protected GuidKey(Guid key)
        {
            if (key == Guid.Empty)
            {
                throw new ArgumentException("The key of a GuidKey must have a value.");
            }

            this.key = key;
        }

        public Guid Key
        {
            get { return key; }
            protected set { key = value; }
        }

        protected bool Equals(GuidKey other)
        {
            return Equals(key, other.key);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((GuidKey)obj);
        }

        public int CompareTo(object obj)
        {
            var other = obj as GuidKey;
            return other == null ? -1 : key.CompareTo(other.key);
        }

        public override int GetHashCode()
        {
            return (key != null ? key.GetHashCode() : 0);
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Key", Key.ToString());
        }

        public static bool operator ==(GuidKey left, GuidKey right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(GuidKey left, GuidKey right)
        {
            return !Equals(left, right);
        }

        public abstract object Clone();

        public override string ToString()
        {
            return Key.ToString();
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public Guid ToGuid()
        {
            return key;
        }

        public void ReadXml(XmlReader reader)
        {
            key = Guid.Parse(reader.ReadElementContentAsString());
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteValue(key.ToString());
        }
    }
}