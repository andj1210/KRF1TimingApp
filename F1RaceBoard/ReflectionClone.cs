// Copyright 2018-2021 Andreas Jung
// SPDX-License-Identifier: GPL-3.0-only

using System;
using System.Reflection;

namespace F1GameSessionDisplay
{
    // a simple utility to make a deep copy of an managed object
    public class ReflectionCloner
    {
        public static T DeepCopy<T>(T obj)
        {
            if (obj == null)
                throw new ArgumentNullException("Object cannot be null");

            return (T)Process(obj);
        }

        private static object Process(object obj)
        {
            if (obj == null)
                return null;

            Type type = obj.GetType();
            if (type.Name == "PropertyChangedEventHandler") // skip event handler
                return null;

            if (type.IsValueType || type == typeof(string))
                return obj; // its copyable

            else if (type.IsArray)
            {
                // deep copy each element
                Type elementType = type.GetElementType();

                var array = obj as Array;
                Array copied = Array.CreateInstance(elementType, array.Length);
                for (int i = 0; i < array.Length; i++)
                    copied.SetValue(Process(array.GetValue(i)), i);

                return Convert.ChangeType(copied, obj.GetType());
            }
            else if (type.IsClass)
            {
                object toret = Activator.CreateInstance(obj.GetType());
                FieldInfo[] fields = type.GetFields(BindingFlags.Public |
                            BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    object fieldValue = field.GetValue(obj);
                    if (fieldValue == null)
                        continue;
                    field.SetValue(toret, Process(fieldValue));
                }
                return toret;
            }
            else
                throw new ArgumentException("Unknown type");
        }

    }
}
