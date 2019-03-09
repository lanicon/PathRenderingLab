﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace PathRenderingLab
{
    public static class CSSEnumPicker<T> where T : struct, IConvertible
    {
        private static Dictionary<string, T> enumValues;

        static CSSEnumPicker()
        {
            if (!typeof(T).IsEnum) throw new ArgumentException("T must be an enumerated type");

            enumValues = new Dictionary<string, T>();
            foreach (T v in Enum.GetValues(typeof(T)))
                enumValues.Add(StringUtils.ConvertToCSSCase(v.ToString()), v);
        }

        public static T? Get(string name) => name != null && enumValues.ContainsKey(name) ? enumValues[name] : new T?();
    }
}
