namespace V8SignalRDebugging
{
    using System;

    public static class Extensions
    {
        /// <summary>
        /// Tries to parse string to enum type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <param name="ignoreCase">if set to <c>true</c> [ignore case].</param>
        /// <param name="defaultValue"></param>
        /// <param name="enumValue">The enum value.</param>
        /// <returns></returns>
        public static bool TryParseEnum<T>(this object value, bool ignoreCase, T defaultValue, out T enumValue)
          where T : struct
        {
            if (value is SByte)
            {
                var val = (SByte)value;
                enumValue = (T)Enum.ToObject(typeof(T), val);
                return true;
            }

            if (value is Int16)
            {
                var val = (Int16)value;
                enumValue = (T)Enum.ToObject(typeof(T), val);
                return true;
            }

            if (value is Int32)
            {
                var val = (Int32)value;
                enumValue = (T)Enum.ToObject(typeof(T), val);
                return true;
            }

            if (value is Int64)
            {
                var val = (Int64)value;
                enumValue = (T)Enum.ToObject(typeof(T), val);
                return true;
            }

            if (value is Byte)
            {
                var val = (Byte)value;
                enumValue = (T)Enum.ToObject(typeof(T), val);
                return true;
            }

            if (value is UInt16)
            {
                var val = (UInt16)value;
                enumValue = (T)Enum.ToObject(typeof(T), val);
                return true;
            }

            if (value is UInt32)
            {
                var val = (UInt32)value;
                enumValue = (T)Enum.ToObject(typeof(T), val);
                return true;
            }

            if (value is UInt64)
            {
                var val = (UInt64)value;
                enumValue = (T)Enum.ToObject(typeof(T), val);
                return true;
            }

            var stringValue = value.ToString();

            try
            {
                enumValue = (T)Enum.Parse(typeof(T), stringValue, ignoreCase);
                return true;
            }
            catch
            {
                enumValue = default(T);
                return false;
            }
        }
    }
}
