using System;
using System.Collections;
using System.Text;

namespace Brunet.Dht
{	
	/// <summary>
	/// An entry of the array returned by Get(...) in Dht interfaces.
	/// </summary>
    [Serializable]
    public class DhtGetResultItem
	{
        /**
         * Const fileds cannot be public
         */
        const string HT_KEY_DATA = "value";
        const string HT_KEY_AGE = "age";
		
		/*
         * Fields kept public for Serialization
         */
        public int age;
        //Filed name remained "data" because of the possible confusion with the keyword "value" in C#
		public byte[] data;

        /// <summary>
        /// A String representation of the field byte[] data for convenience
        /// </summary>
        public string DataString
        {
            get { return Encoding.UTF8.GetString(data); }
            set { this.data = Encoding.UTF8.GetBytes(value); }
        }

        public DhtGetResultItem()
        {
        }

        public DhtGetResultItem(string data, int age)
        {
            this.data = Encoding.UTF8.GetBytes(data);
            this.age = age;
        }

		public DhtGetResultItem(Hashtable ht)
		{
			this.age = (int)ht[HT_KEY_AGE];
			this.data = (byte[])ht[HT_KEY_DATA];
		}

        /// <summary>
        /// All Explicit conversion
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        /// <example>Hashtable ht = (Hashtable)item</example>
        public static explicit operator Hashtable(DhtGetResultItem item)
        {
            Hashtable ht = new Hashtable();
            ht.Add(HT_KEY_DATA, item.data);
            ht.Add(HT_KEY_AGE, item.age);
            //value_string, a field added by David
            ht.Add("value_string",item.DataString);
            return ht;
        }
		
		public override string ToString()
		{
			return string.Format("{0}={1}, {2}={3}", HT_KEY_DATA, Encoding.UTF8.GetString(data),  HT_KEY_AGE, age);
		}
	}
}
